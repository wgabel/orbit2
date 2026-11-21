import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'envelope.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const direct = await compiler.createMonoDSPFactory('EnvelopeFixture', fixture.directSource, '');
const impact = await compiler.createMonoDSPFactory('ImpactFixture', fixture.source, '');
const lengthFor = n => Math.ceil(n / 128) * 128;
const processor = (rate, factory) => new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
const near = (actual, expected, tolerance = 0.00001) => assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`);
const samples = (rate, ms) => Math.round(rate * ms / 1000);

for (const rate of [44100, 48000]) {
    test(`AD edges, exact timing and segment control latching at ${rate} Hz`, async () => {
        const attack = samples(rate, 8), decay = samples(rate, 20);
        const event = 128, endAttack = event + attack, endDecay = endAttack + decay;
        const length = lengthFor(endDecay + 256);
        const x = new Float32Array(length).fill(1); // Startup high must not trigger.
        x.fill(0.05, 32, 64); // Hysteresis: not low enough to arm.
        x.fill(1, 64, 96);
        x.fill(0, 96, 112);
        x.fill(0.05, 112, event);
        x.fill(0.05, event + 2, event + 20); // Falling into the deadband does not rearm.
        x.fill(1, event + 20, endAttack + 1);
        x.fill(-1, endAttack + 1); // Falling and constant low do not interrupt AD.
        const a = new Float32Array(length).fill(8), d = new Float32Array(length).fill(20);
        const ac = new Float32Array(length).fill(1), dc = new Float32Array(length).fill(2);
        a.fill(100, event + 1); ac.fill(8, event + 1);
        d.fill(50, endAttack + 1); dc.fill(8, endAttack + 1);
        const dsp = await processor(rate, direct);
        const [out] = dsp.render([x, a, d, ac, dc], length);
        assert.ok(out.slice(0, event + 1).every(v => v === 0));
        for (let i = 1; i <= attack; i++) near(out[event + i], i / attack);
        assert.equal(out[endAttack], 1);
        for (let i = 1; i <= decay; i++) near(out[endAttack + i], (1 - i / decay) ** 2);
        assert.equal(out[endDecay], 0);
        assert.ok(out.slice(endDecay).every(v => v === 0));
        assert.ok(out.every(v => Number.isFinite(v) && v >= 0 && v <= 1));
    });

    test(`AD retriggers preserve the current sample during attack and decay at ${rate} Hz`, async () => {
        const attack = samples(rate, 8), decay = samples(rate, 20);
        const first = 16, second = first + Math.floor(attack / 2), third = second + attack + Math.floor(decay / 2);
        const length = lengthFor(third + attack + decay + 256);
        const x = new Float32Array(length);
        for (const edge of [first, second, third]) x[edge] = 1;
        const dsp = await processor(rate, direct);
        const [out] = dsp.render([x, ...[8, 20, 1, 1].map(v => new Float32Array(length).fill(v))], length);
        for (const edge of [second, third]) {
            assert.ok(out[edge - 1] > 0 && out[edge - 1] < 1);
            assert.equal(out[edge], out[edge - 1]);
            near(out[edge + 1], out[edge] + (1 - out[edge]) / attack);
            assert.equal(out[edge + attack], 1);
        }
        assert.equal(out[third + attack + decay], 0);
        assert.ok(out.slice(third + attack + decay).every(v => v === 0));
        const disconnected = await processor(rate, direct);
        const [silent] = disconnected.render([new Float32Array(length), ...[8, 20, 1, 1].map(v => new Float32Array(length).fill(v))], length);
        assert.ok(silent.every(v => v === 0));
    });

    test(`Nested impact patch aligns two independent ADs and sweeps pitch/level at ${rate} Hz`, async () => {
        const dsp = await processor(rate, impact);
        for (const [key, value] of Object.entries(fixture.values)) {
            const path = dsp.getParams().find(path => path.endsWith('/' + key));
            assert.ok(path, key);
            dsp.setParamValue(path, value);
        }
        const output = dsp.render([], lengthFor(rate * 3));
        const [audio] = output, trigger = output[fixture.trigger], pitch = output[fixture.pitch], amp = output[fixture.amplitude];
        let armed = false;
        const events = [];
        for (let i = 0; i < trigger.length; i++) {
            if (trigger[i] <= 0) armed = true;
            if (trigger[i] >= 0.1 && armed) { if (i > rate / 2) events.push(i); armed = false; }
        }
        assert.ok(events.length >= 2);
        for (const event of events.filter(i => i + rate * 0.52 < audio.length)) {
            const pitchPeak = event + samples(rate, 1), ampPeak = event + samples(rate, 8);
            near(pitch[event], 0, 0.0001); near(amp[event], 0, 0.0001);
            assert.ok(pitch[event + 1] > 0 && amp[event + 1] > 0);
            near(pitch[pitchPeak], 1, 0.0001); near(amp[ampPeak], 1, 0.0001);
            near(pitch[pitchPeak + samples(rate, 60)], 0.125, 0.0002);
            near(amp[ampPeak + samples(rate, 250)], 0.125, 0.0002);
            near(pitch[pitchPeak + samples(rate, 120)], 0, 0.0001);
            near(amp[ampPeak + samples(rate, 500)], 0, 0.0001);
            let peak = 0;
            for (let i = event; i < event + rate * 0.52; i++) {
                assert.ok(Math.abs(audio[i]) <= 0.8 * amp[i] + 0.0002, 'Audio exceeds the modulated amplitude');
                peak = Math.max(peak, Math.abs(audio[i]));
            }
            assert.ok(peak > 0.6);
            // Late in the decay, the instantaneous carrier returns to ~55 Hz.
            const crossings = [];
            for (let i = event + Math.floor(rate * 0.15); i < event + Math.floor(rate * 0.4); i++)
                if (audio[i - 1] <= 0 && audio[i] > 0) crossings.push(i);
            assert.ok(crossings.length > 8);
            near((crossings.length - 1) * rate / (crossings.at(-1) - crossings[0]), 55, 0.2);
        }
        assert.ok(audio.every(v => Number.isFinite(v) && Math.abs(v) <= 0.801));
    });
}
