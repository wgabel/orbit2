import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixtures = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'instruments.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));

for (const [index, fixture] of fixtures.entries()) {
    const factory = await compiler.createMonoDSPFactory(`Instrument${index}`, fixture.source, '');
    for (const rate of [44100, 48000]) {
        test(`${fixture.key} renders with headroom and tracks an octave at ${rate} Hz`, async () => {
            const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
            const path = name => {
                const p = dsp.getParams().find(p => p.endsWith('/' + name));
                assert.ok(p, name);
                return p;
            };
            for (const [name, value] of Object.entries(fixture.values)) dsp.setParamValue(path(name), value);
            const meters = new Map();
            dsp.setOutputParamHandler((p, value) => meters.set(p.split('/').at(-1), value));
            const length = Math.ceil(2 * rate / 128) * 128;
            for (const semitones of [0, 12]) {
                dsp.setParamValue(path(fixture.transpose), semitones);
                const [audio] = dsp.render([], length);
                let energy = 0, peak = 0;
                for (let i = 0; i < audio.length; i++) {
                    assert.ok(Number.isFinite(audio[i]));
                    peak = Math.max(peak, Math.abs(audio[i]));
                    if (i >= rate) energy += audio[i] ** 2;
                }
                assert.ok(peak < .75, `Peak ${peak} exceeds conservative headroom`);
                assert.ok(Math.sqrt(energy / (audio.length - rate)) > .005, 'Silent or inaudibly quiet instrument');
                for (const [i, meter] of fixture.meters.entries())
                    assert.ok(Math.abs(meters.get(meter) - fixture.frequencies[i] * 2 ** (semitones / 12)) < .25,
                        `${meter}: ${meters.get(meter)} did not track ${semitones} st`);
            }
        });
    }
}
