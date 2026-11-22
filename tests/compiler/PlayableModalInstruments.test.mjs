import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { RhythmicActionQueue } from '../../src/Orbit.Infrastructure/wwwroot/audio/Components/RhythmicActionQueue.js';

const fixtures = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'playable-modal-instruments.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const rms = samples => Math.sqrt(samples.reduce((sum, x) => sum + x * x, 0) / samples.length);

for (const [index, fixture] of fixtures.entries()) {
    const factory = await compiler.createMonoDSPFactory(`PlayableModal${index}`, fixture.source, '');
    for (const rate of [44100, 48000]) {
        test(`${fixture.key}: keyboard velocity, repeated strikes, tails and independent audio excitation at ${rate}`, async () => {
            assert.equal(fixture.inputs, 1);
            const block = seconds => Math.ceil(seconds * rate / 128) * 128;
            const length = block(6), hit1 = block(.5), hit2 = block(2);
            async function render({ velocity = 127, note = 69, release = true, external = 0, strikes = true } = {}) {
                const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
                const path = name => {
                    const p = dsp.getParams().find(p => p.endsWith('/' + name));
                    assert.ok(p, name); return p;
                };
                for (const [name, value] of Object.entries(fixture.values)) dsp.setParamValue(path(name), value);
                dsp.setParamValue(path(fixture.external), external);
                const meters = new Map();
                dsp.setOutputParamHandler((p, value) => meters.set(p.split('/').at(-1), value));
                const queue = new RhythmicActionQueue([{ isNote: true }]);
                const words = new Float32Array(length);
                const schedule = (frame, action) => {
                    assert.ok(queue.push(0, action));
                    queue.render([words.subarray(frame, frame + 128)], 0);
                };
                if (strikes) {
                    for (const frame of [hit1, hit2]) {
                        schedule(frame, note + 1 + 512 * velocity);
                        if (release) schedule(frame + 128, 130 + note);
                    }
                }
                const [audio] = dsp.render([words], length);
                for (const x of audio) assert.ok(Number.isFinite(x) && Math.abs(x) < .75, 'Nonfinite or excessive peak');
                for (const [i, meter] of fixture.meters.entries()) {
                    const expected = fixture.frequencies[i] * 2 ** ((strikes ? note - 69 : 0) / 12);
                    assert.ok(Math.abs(meters.get(meter) - expected) < .25, `Mode ${i}: ${meters.get(meter)} != ${expected}`);
                }
                return audio;
            }
            const window = (audio, start, seconds) => audio.subarray(start, start + block(seconds));
            const loud = await render();
            assert.equal(rms(loud.subarray(0, hit1)), 0, 'Silent before first note');
            const hit = rms(window(loud, hit1, .2));
            assert.ok(hit > .003, `Strike too quiet: ${hit}`);
            assert.ok(rms(window(loud, hit2, .2)) > .003, 'Second strike missing');
            assert.ok(rms(loud.subarray(length - block(.3))) < hit * .04, 'Tail did not decay');
            const held = await render({ release: false });
            assert.deepEqual(held, loud, 'Key release must not truncate a percussion strike or tail');
            const quiet = await render({ velocity: 32 });
            assert.ok(Math.abs(rms(window(quiet, hit1, .2)) / hit - 32 / 127) < .015, 'Velocity must scale strike linearly');
            const muted = await render({ velocity: 0 });
            assert.equal(rms(muted), 0, 'Zero velocity must suppress internal strike');
            const octave = await render({ note: 81 });
            assert.ok(rms(window(octave, hit1, .2)) > .001, 'Octave strike missing');
            const driven = await render({ external: .1, strikes: false });
            const drivenWithSilentStrikes = await render({ external: .1, velocity: 0 });
            assert.ok(rms(window(driven, block(3), 1)) > .003, 'External input must ring with zero velocity');
            assert.deepEqual(driven, drivenWithSilentStrikes, 'Silent keyboard strikes must not affect external drive');
        });
    }
}
