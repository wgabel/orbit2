import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'keyboard-pitch.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factory = await compiler.createMonoDSPFactory('keyboard', fixture.source, '');
function frequency(samples, rate, start, end) {
    const crossings = [];
    for (let i = start + 1; i < end; i++) if (samples[i - 1] <= 0 && samples[i] > 0)
        crossings.push(i - 1 - samples[i - 1] / (samples[i] - samples[i - 1]));
    return (crossings.length - 1) * rate / (crossings.at(-1) - crossings[0]);
}

for (const rate of [44100, 48000]) test(`Live notes latch a shared exact offset and keep oscillator phase continuous at ${rate}`, async () => {
    const processor = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
    for (const [name, value] of Object.entries(fixture.values)) processor.setParamValue(processor.getParams().find(p => p.endsWith('/' + name)), value);
    const n = Math.ceil(3 * rate / 128) * 128;
    const action = new Float32Array(n);
    action[rate] = 58; // A3 = MIDI 57, +12 st
    action[rate * 2] = 129; // explicit reset, not key release
    const meters = new Set();
    processor.setOutputParamHandler((path, value) => { if (path.endsWith('/' + fixture.meter)) meters.add(value); });
    const output = processor.render([action], n);
    const pitch = output[fixture.pitch];
    assert.ok(pitch.slice(0, rate).every(v => v === 0));
    assert.ok(pitch.slice(rate, rate * 2).every(v => v === 12));
    assert.ok(pitch.slice(rate * 2).every(v => v === 0));
    assert.ok(meters.has(12) && meters.has(0));
    for (const [i, channel] of fixture.voices.entries()) {
        const signal = output[channel], base = [110,165][i];
        for (const [start, target] of [[rate/2,base],[rate*1.5,base*2],[rate*2.5,base]])
            assert.ok(Math.abs(frequency(signal, rate, Math.floor(start), Math.floor(start + rate/4)) - target) < 0.2);
        // No state restart or discontinuity at the note/reset sample.
        for (const at of [rate, rate*2]) assert.ok(Math.abs(signal[at]-signal[at-1]) < 0.02);
    }
});
