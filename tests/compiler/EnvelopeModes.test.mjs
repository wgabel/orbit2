import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'envelope-modes.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factories = {};
for (const [mode, source] of Object.entries({...fixture.direct, graph: fixture.source})) factories[mode] = await compiler.createMonoDSPFactory(mode, source, '');
const dsp = (rate, mode) => new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factories[mode], soundfiles: {} });
const lengthFor = n => Math.ceil(n / 128) * 128;
const near = (actual, expected, tolerance = .00001) => assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`);
const params = (n, values = [8, 20, 1, 1, 12, 2, .4, 5]) => values.map(value => new Float32Array(n).fill(value));
const bounded = signal => assert.ok(signal.every(value => Number.isFinite(value) && value >= 0 && value <= 1));

for (const rate of [44100, 48000]) {
    const ms = value => Math.round(rate * value / 1000);
    test(`AR holds while gate is high and releases from any stage at ${rate}`, async () => {
        const a = ms(8), r = ms(12), firstClose = Math.floor(a / 2), secondOpen = firstClose + Math.floor(r / 3), secondClose = secondOpen + a + 128;
        const n = lengthFor(secondClose + r + 128);
        const gate = new Float32Array(n); gate.fill(1, 0, firstClose); gate.fill(1, secondOpen, secondClose);
        const controls = params(n);
        controls[4].fill(50, firstClose + 1, secondOpen); // in-flight release must latch 12ms
        const [out] = (await dsp(rate, 'Ar')).render([gate, ...controls], n);
        assert.equal(out[0], 0); near(out[firstClose - 1], (firstClose - 1) / a);
        assert.equal(out[firstClose], out[firstClose - 1]);
        for (let i = 1; i < secondOpen - firstClose; i++) near(out[firstClose + i], out[firstClose] * (1 - i / r) ** 2);
        assert.equal(out[secondOpen], out[secondOpen - 1]);
        near(out[secondOpen + 1], out[secondOpen] + (1 - out[secondOpen]) / a);
        assert.equal(out[secondOpen + a], 1); assert.ok(out.slice(secondOpen + a, secondClose + 1).every(v => v === 1));
        for (let i = 1; i <= r; i++) near(out[secondClose + i], (1 - i / r) ** 2);
        assert.ok(out.slice(secondClose + r).every(v => v === 0)); bounded(out);
    });

    test(`ADSR latches decay target then slews held sustain, release and retrigger at ${rate}`, async () => {
        const a = ms(8), d = ms(20), r = ms(12), on = 32, peak = on + a, decayEnd = peak + d;
        const off = decayEnd + ms(12), restart = off + Math.floor(r / 2), earlyOff = restart + a + Math.floor(d / 2);
        const n = lengthFor(earlyOff + r + 128), gate = new Float32Array(n);
        gate.fill(1, on, off); gate.fill(1, restart, earlyOff);
        const controls = params(n);
        controls[6].fill(.8, peak + 1); // the previous .4 target remains latched through decay
        controls[1].fill(100, peak + 1, decayEnd + 1); controls[3].fill(8, peak + 1, decayEnd + 1);
        const [out] = (await dsp(rate, 'Adsr')).render([gate, ...controls], n);
        assert.ok(out.slice(0, on + 1).every(v => v === 0)); assert.equal(out[peak], 1);
        for (let i = 1; i <= d; i++) near(out[peak + i], .4 + .6 * (1 - i / d));
        near(out[decayEnd], .4); assert.ok(out[decayEnd + 1] > .4 && out[decayEnd + 1] < .41);
        for (let i = decayEnd + 1; i < off; i++) assert.ok(out[i] - out[i-1] <= 1 / (.01 * rate) + 1e-7);
        near(out[off], .8); assert.equal(out[off], out[off-1]);
        assert.equal(out[restart], out[restart-1]); assert.equal(out[restart + a], 1);
        assert.ok(out[earlyOff] > .8 && out[earlyOff] < 1); assert.equal(out[earlyOff], out[earlyOff-1]);
        for (let i = 1; i <= r; i++) near(out[earlyOff + i], out[earlyOff] * (1 - i / r) ** 2);
        assert.ok(out.slice(earlyOff + r).every(v => v === 0)); bounded(out);
    });

    test(`Gate-off wins at the attack endpoint and disconnected gated modes stay idle at ${rate}`, async () => {
        const a = ms(8), r = ms(12), n = lengthFor(a + r + 64);
        for (const mode of ['Ar', 'Adsr']) {
            const gate = new Float32Array(n); gate.fill(1, 0, a);
            const [out] = (await dsp(rate, mode)).render([gate, ...params(n)], n);
            assert.equal(out[a], out[a-1]); assert.ok(out[a] < 1); assert.equal(out[a+r], 0);
            const [idle] = (await dsp(rate, mode)).render([new Float32Array(n), ...params(n)], n);
            assert.ok(idle.every(v => v === 0));
        }
    });

    for (const rest of [0, 5]) test(`Loop repeats exact AD cycles with ${rest} ms rest at ${rate}`, async () => {
        const a = ms(8), d = ms(20), wait = ms(rest), period = a + d + wait, n = lengthFor(period * 5 + 1);
        const [out] = (await dsp(rate, 'Loop')).render([new Float32Array(n), ...params(n, [8,20,1,2,12,2,.4,rest])], n);
        for (let i = 0; i < n; i++) {
            const phase = i % period;
            near(out[i], phase <= a ? phase/a : phase <= a+d ? (1-(phase-a)/d)**2 : 0);
        }
        bounded(out);
    });

    test(`Loop latches rest and attack controls independently at ${rate}`, async () => {
        const a = ms(8), d = ms(20), wait = ms(5), nextAttack = ms(12), secondStart = a + d + wait;
        const n = lengthFor(secondStart + nextAttack + d + 128), controls = params(n, [8,20,1,1,12,2,.4,5]);
        controls[0].fill(12, 1); controls[7].fill(0, a+d+1);
        const [out] = (await dsp(rate, 'Loop')).render([new Float32Array(n), ...controls], n);
        assert.equal(out[a], 1); assert.equal(out[a+d], 0);
        assert.ok(out.slice(a+d, secondStart+1).every(v => v === 0));
        near(out[secondStart+1], 1/nextAttack);
        assert.equal(out[secondStart+nextAttack], 1); assert.equal(out[secondStart+nextAttack+d], 0);
        near(out[secondStart+nextAttack+d+1], 1/nextAttack);
    });

    test(`Real compiled graph fans a typed gate into AR and ADSR and loops independently at ${rate}`, async () => {
        const processor = await dsp(rate, 'graph');
        for (const [key, value] of Object.entries(fixture.values)) processor.setParamValue(processor.getParams().find(p => p.endsWith('/' + key)), value);
        const on = ms(500), off = on + ms(70), n = lengthFor(off + ms(40));
        const action = new Float32Array(n); action[on] = 1; action[off] = 2;
        const output = processor.render([action], n);
        const [ar, adsr, loop] = fixture.channels.map(index => output[index]);
        for (const signal of [ar, adsr, loop]) bounded(signal);
        assert.ok(ar.slice(0,on+1).every(v => v === 0) && adsr.slice(0,on+1).every(v => v === 0));
        assert.equal(output[fixture.gate][on], 1); assert.equal(output[fixture.gate][off], 0);
        assert.deepEqual(ar.slice(on, on + ms(5)+1), adsr.slice(on, on+ms(5)+1));
        near(ar[on + ms(25)], 1); near(adsr[on + ms(25)], .4, .0001);
        assert.equal(ar[off], ar[off-1]); assert.equal(adsr[off], adsr[off-1]);
        assert.equal(ar[off+ms(15)], 0); assert.equal(adsr[off+ms(15)], 0);
        assert.ok(loop.slice(ms(200),on).some(v => v === 1) && loop.slice(ms(200),on).some(v => v === 0));
        // The first loop starts from the initialized controls, not a smoother
        // ramping up from zero. The first segment and rest are exact too.
        assert.equal(loop[ms(5)], 1);
        assert.equal(loop[ms(5) + ms(10)], 0);
        assert.ok(loop.slice(ms(5)+ms(10), ms(5)+ms(10)+ms(7)+1).every(v => v === 0));
        assert.ok(loop[ms(5)+ms(10)+ms(7)+1] > 0);
    });
}
