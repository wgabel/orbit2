import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { RhythmicActionQueue } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/RhythmicActionQueue.js';

const fixtures = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'playable-instruments.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const rms = samples => Math.sqrt(samples.reduce((sum, x) => sum + x * x, 0) / samples.length);

for (const [index, fixture] of fixtures.entries()) {
    const factory = await compiler.createMonoDSPFactory(`Playable${index}`, fixture.source, '');
    for (const rate of [44100, 48000]) {
        test(`${fixture.key} responds to pitch, held gate, release and velocity at ${rate}`, async () => {
            assert.equal(fixture.inputs, 1);
            const block = seconds => Math.ceil(seconds * rate / 128) * 128;
            async function render(velocity) {
                const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
                for (const [name, value] of Object.entries(fixture.values)) {
                    const path = dsp.getParams().find(p => p.endsWith('/' + name));
                    assert.ok(path, name);
                    dsp.setParamValue(path, value);
                }
                const meters = new Map();
                dsp.setOutputParamHandler((p, value) => meters.set(p.split('/').at(-1), value));
                const queue = new RhythmicActionQueue([{ isNote: true }]);
                const renderSegment = (length, action) => {
                    const words = new Float32Array(length);
                    if (action !== undefined) {
                        assert.ok(queue.push(0, action));
                        queue.render([words.subarray(0, 128)], 0);
                    }
                    return dsp.render([words], length)[0];
                };
                const idle = renderSegment(block(.2));
                assert.ok(rms(idle) < 1e-7, 'No held notes must be silent');
                const held = renderSegment(block(1.5), 81 + 1 + 512 * velocity);
                for (const [i, meter] of fixture.meters.entries())
                    assert.ok(Math.abs(meters.get(meter) - fixture.frequencies[i] * 2) < .25, `Octave tracking: ${meter}`);
                const tail = renderSegment(block(7), 130 + 81);
                assert.ok(held.every(Number.isFinite) && tail.every(Number.isFinite));
                assert.ok(held.every(x => Math.abs(x) < .75) && tail.every(x => Math.abs(x) < .75));
                const sustained = rms(held.subarray(block(1)));
                assert.ok(sustained > .001, 'Held note must sustain');
                assert.ok(rms(tail.subarray(0, block(.05))) > .0001, 'Release should preserve a tail');
                assert.ok(rms(tail.subarray(tail.length - block(.5))) < sustained * .05, 'Release must decay to silence');
                return sustained;
            }
            const soft = await render(32);
            const loud = await render(127);
            assert.ok(loud / soft > 3 && loud / soft < 5, `Velocity ratio ${loud / soft}`);
        });
    }
}
