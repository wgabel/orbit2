import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'distortion.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factory = await compiler.createMonoDSPFactory('DistortionGraph', fixture.source, '');

async function render(rate, overrides) {
    const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
    for (const [name, value] of Object.entries({ ...fixture.values, ...overrides })) {
        const path = dsp.getParams().find(p => p.endsWith('/' + name));
        assert.ok(path, 'Missing parameter ' + name);
        dsp.setParamValue(path, value);
    }
    const [samples] = dsp.render([], Math.ceil(rate * 2 / 128) * 128);
    return samples.slice(rate);
}

for (const rate of [44100, 48000]) {
    test(`Distortion dry and bypass agree; wet adds bounded harmonics at ${rate} Hz`, async () => {
        const dry = await render(rate, { [fixture.mix]: 0 });
        const bypass = await render(rate, { [fixture.mix]: 1, [fixture.bypass]: 1 });
        const wet = await render(rate, { [fixture.mix]: 1, [fixture.drive]: 16 });
        assert.ok(dry.some(v => Math.abs(v) > .01));
        assert.ok(dry.every((v, i) => Math.abs(v - bypass[i]) < .0001));
        assert.ok(wet.every(v => Number.isFinite(v) && Math.abs(v) <= 1.0001));
        assert.ok(wet.some((v, i) => Math.abs(v - dry[i]) > .05));
        const amplitude = (signal, hz) => {
            let re = 0, im = 0;
            for (let i = 0; i < rate; i++) {
                const phase = 2 * Math.PI * hz * i / rate;
                re += signal[i] * Math.cos(phase);
                im += signal[i] * Math.sin(phase);
            }
            return 2 * Math.hypot(re, im) / rate;
        };
        assert.ok(amplitude(wet, 1320) > amplitude(dry, 1320) + .01);
    });
    test(`Distortion silence and extreme modulation remain finite at ${rate} Hz`, async () => {
        const silence = await render(rate, { [fixture.amplitude]: 0, [fixture.mix]: 1, [fixture.drive]: 16 });
        assert.ok(silence.every(v => Math.abs(v) < .00001));
        const extreme = await render(rate, { [fixture.amplitude]: 1, [fixture.modulation]: 1, [fixture.drive]: 16, [fixture.mix]: 1 });
        assert.ok(extreme.every(v => Number.isFinite(v) && Math.abs(v) <= 1.0001));
    });
}
