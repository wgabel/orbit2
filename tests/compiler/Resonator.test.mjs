import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'resonator.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const direct = await compiler.createMonoDSPFactory('DampedMode', fixture.directSource, '');
const graph = await compiler.createMonoDSPFactory('ResonatorGraph', fixture.source, '');
const processor = (rate, factory) => new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
const lengthFor = n => Math.ceil(n / 128) * 128;
const fill = (length, value) => new Float32Array(length).fill(value);

for (const rate of [44100, 48000]) {
    test(`Resonator impulse follows exact frequency and T60 at ${rate} Hz`, async () => {
        for (const decay of [.2, 1]) {
            const n = lengthFor(rate * (decay + .1)), frequency = 220, drive = 16, gain = .5;
            const input = fill(n, 0); input[0] = 1;
            const dsp = await processor(rate, direct);
            const [out] = dsp.render([input, ...[frequency, decay, drive, gain, 1].map(v => fill(n, v))], n);
            const r = Math.exp(-Math.log(1000) / (decay * rate));
            const initial = (1-r)*drive*gain;
            assert.ok(Math.abs(out[0] / initial - 1) < .005);
            for (let i = 1; i < n; i += 23) {
                const expected = initial * r ** i * Math.cos(2 * Math.PI * frequency * i / rate);
                assert.ok(Math.abs(out[i] - expected) < initial * .002, `sample ${i} differs from damped cosine`);
            }
            assert.ok(Math.abs(out[Math.round(rate*decay)] / out[0] - .001) < .00008);
            assert.ok(out.every(Number.isFinite));
        }
    });

    test(`Silence, dry mix, and extreme audio-rate modulation at ${rate} Hz`, async () => {
        const n = lengthFor(rate * 2);
        const silence = await processor(rate, direct);
        const [zero] = silence.render([fill(n, 0), ...[220, 20, 32, 1, 1].map(v => fill(n,v))], n);
        assert.ok(zero.every(v => v === 0));
        const input = Float32Array.from({length:n}, (_, i) => .025 * Math.sin(i*1.7));
        const dry = await processor(rate, direct);
        const [unchanged] = dry.render([input, ...[220, 20, 32, 1, 0].map(v => fill(n,v))], n);
        assert.deepEqual(unchanged, input);
        const modulated = await processor(rate, direct);
        const frequency = Float32Array.from({length:n}, (_, i) => i % 29 < 15 ? 20 : 20000);
        const decay = Float32Array.from({length:n}, (_, i) => i % 31 < 16 ? .02 : 20);
        const [out] = modulated.render([input, frequency, decay, fill(n,32), fill(n,1), fill(n,1)], n);
        assert.ok(out.every(v => Number.isFinite(v) && Math.abs(v) <= .801));
        assert.ok(out.some(v => Math.abs(v) > .0001));
    });

    test(`Long decay remains stable and retriggers accumulate rather than restart at ${rate} Hz`, async () => {
        const n = lengthFor(rate*21+1), frequency = 220, decay = 20, drive = 16;
        const impulse = fill(n, 0); impulse[0] = 1; impulse[rate] = 1;
        const dsp = await processor(rate,direct);
        const [out] = dsp.render([impulse, ...[frequency,decay,drive,1,1].map(v=>fill(n,v))],n);
        const r = Math.exp(-Math.log(1000)/(rate*decay));
        // After one second the previous state is still positive at this frequency.
        assert.ok(Math.abs(out[rate]/out[0] - (1+r**rate)) < .02);
        // The second strike also decays by 60 dB after T60 (within float precision).
        assert.ok(Math.abs(out[rate*21]/out[rate] - .001) < .00015);
        assert.ok(out.every(Number.isFinite));
    });

    test(`Generated graph retunes resonator with shared semitones at ${rate} Hz`, async () => {
        const dsp = await processor(rate, graph);
        const path = name => dsp.getParams().find(p => p.endsWith('/' + name));
        for (const [key, value] of Object.entries(fixture.values)) dsp.setParamValue(path(key), value);
        let frequency = 0;
        dsp.setOutputParamHandler((p,v) => { if (p.endsWith('/' + fixture.frequencyMeter)) frequency = v; });
        const n = lengthFor(rate * 2);
        dsp.render([], n);
        assert.ok(Math.abs(frequency-220) < .1);
        dsp.setParamValue(path(fixture.transpose), 12);
        const [out] = dsp.render([], n);
        assert.ok(Math.abs(frequency-440) < .1);
        const rms = Math.sqrt(out.slice(rate).reduce((sum, v) => sum+v*v, 0) / (n-rate));
        assert.ok(rms > .01 && rms < .1, `Unexpected resonant tone RMS ${rms}`);
        assert.ok(out.every(Number.isFinite));
    });
}
