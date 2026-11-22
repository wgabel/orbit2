import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixtures = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'modal-instruments.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const rms = samples => Math.sqrt(samples.reduce((energy, value) => energy + value ** 2, 0) / samples.length);
const magnitude = (samples, rate, frequency) => {
    let re = 0, im = 0;
    for (let i = 0; i < samples.length; i++) {
        const w = .5 - .5 * Math.cos(2 * Math.PI * i / (samples.length - 1));
        const phase = 2 * Math.PI * frequency * i / rate;
        re += w * samples[i] * Math.cos(phase); im += w * samples[i] * Math.sin(phase);
    }
    return Math.hypot(re, im);
};

for (const [index, fixture] of fixtures.entries()) {
    const factory = await compiler.createMonoDSPFactory(`Modal${index}`, fixture.source, '');
    for (const rate of [44100, 48000]) {
        test(`${fixture.key}: typed strikes, modes, strength and external excitation at ${rate} Hz`, async () => {
            const length = Math.ceil(5 * rate / 128) * 128;
            async function render({ pitch = 0, strength = 0, external = 0, strikes = [.5, 2] } = {}) {
                const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
                const path = name => {
                    const p = dsp.getParams().find(p => p.endsWith('/' + name));
                    assert.ok(p, name); return p;
                };
                for (const [name, value] of Object.entries(fixture.values)) dsp.setParamValue(path(name), value);
                dsp.setParamValue(path(fixture.transpose), pitch);
                dsp.setParamValue(path(fixture.strength), strength);
                dsp.setParamValue(path(fixture.external), external);
                const meters = new Map();
                dsp.setOutputParamHandler((p, value) => meters.set(p.split('/').at(-1), value));
                const input = Array.from({length: fixture.phaseChannels + fixture.actionCount}, () => new Float32Array(length));
                for (const time of strikes) input[fixture.phaseChannels + fixture.actionIndex][Math.round(time * rate)] = 1;
                const [audio] = dsp.render(input, length);
                let peak = 0;
                for (const value of audio) { assert.ok(Number.isFinite(value)); peak = Math.max(peak, Math.abs(value)); }
                assert.ok(peak < .75, `Peak ${peak} exceeds headroom`);
                for (const [i, meter] of fixture.meters.entries())
                    assert.ok(Math.abs(meters.get(meter) - fixture.frequencies[i] * 2 ** (pitch / 12)) < .25,
                        `Mode ${i} did not track ${pitch} st: ${meters.get(meter)}`);
                return { audio, peak };
            }
            const window = (audio, start, end) => audio.subarray(Math.round(rate * start), Math.round(rate * end));
            const { audio, peak } = await render();
            assert.ok(peak > .015, `Strike too quiet: ${peak}`);
            assert.equal(rms(window(audio, 0, .5)), 0, 'No self-excitation before the first strike');
            const hit = rms(window(audio, .5, .7));
            assert.ok(hit > .003, `First strike RMS ${hit}`);
            assert.ok(rms(window(audio, 2, 2.2)) > .003, 'Retrigger did not excite the bank');
            assert.ok(rms(window(audio, 4.8, 5)) < hit * .04, 'Tail did not decay');
            const quiet = await render({ strength: -.375 });
            assert.ok(Math.abs(rms(window(quiet.audio, .5, .7)) / hit - .5) < .03, 'Strength should scale excitation');
            const mute = await render({ strength: -.75 });
            assert.ok(mute.peak < 1e-5, 'Zero strength must suppress excitation');
            const octave = await render({ pitch: 12 });
            const fundamental = Math.min(...fixture.frequencies);
            const start = fixture.key.endsWith('gong') ? .67 : .55;
            const end = fixture.key.endsWith('wood') ? .65 : .9;
            for (const [signal, f] of [[audio, fundamental], [octave.audio, 2 * fundamental]]) {
                const tail = window(signal, start, end);
                assert.ok(magnitude(tail, rate, f) > 2 * magnitude(tail, rate, f * 1.16), `Missing ring near ${f} Hz`);
            }
            const sustained = await render({ external: .1, strikes: [] });
            assert.ok(rms(window(sustained.audio, 3, 4)) > .003, 'External audio did not excite the bank');
            console.log(`${fixture.key} ${rate}: strike peak ${peak.toFixed(4)}, RMS ${hit.toFixed(4)}`);
        });
    }
}
