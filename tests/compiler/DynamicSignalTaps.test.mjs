import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'dynamic-taps.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factory = await compiler.createMonoDSPFactory('dynamicTaps', fixture.source, '');
const mono = await compiler.createMonoDSPFactory('monoTaps', fixture.monoSource, '');

async function processor(rate, factory, values = fixture.values) {
    const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
    for (const [name, value] of Object.entries(values)) dsp.setParamValue(dsp.getParams().find(p => p.endsWith('/' + name)), value);
    return dsp;
}

const setSelector = (dsp, slot, token) => dsp.setParamValue(dsp.getParams().find(path => path.endsWith(`/orbit_tap_select_${slot}`)), token);

function frequency(samples, rate) {
    const crossings = [];
    for (let i = 1; i < samples.length; i++) if (samples[i-1] <= 0 && samples[i] > 0)
        crossings.push(i-1-samples[i-1]/(samples[i]-samples[i-1]));
    return (crossings.length-1)*rate/(crossings.at(-1)-crossings[0]);
}

for (const rate of [44100, 48000]) test(`All twelve live selectors switch without changing master samples at ${rate}`, async () => {
    assert.equal(fixture.master, 0);
    assert.equal(fixture.mixer, 0);
    assert.equal(fixture.disconnected, false);
    assert.equal(fixture.channels, 9);
    assert.ok(fixture.count > 8);
    const active = await processor(rate, factory), reference = await processor(rate, mono);
    const block = 8192, stride = fixture.count + 1;
    const acknowledgements = new Map();
    active.setOutputParamHandler((path, value) => acknowledgements.set(path.split('/').at(-1), value));
    const select = (slot, token) => setSelector(active, slot, token);
    const idle = active.render([], block);
    assert.deepEqual(idle[0], reference.render([], block)[0]);
    assert.ok(idle.slice(1).every(channel => channel.every(value => value === 0)));
    for (const [i, oscillator] of fixture.oscillators.entries()) {
        const token = (i + 1) * stride + oscillator.selector;
        select(1, token);
        const output = active.render([], block);
        assert.deepEqual(output[0], reference.render([], block)[0]);
        assert.ok(Math.abs(frequency(output[1], rate) - oscillator.frequency) < 0.3);
        assert.equal(acknowledgements.get('orbit_tap_ack_1'), token);
    }
    for (let slot = 1; slot <= 8; slot++) select(slot, (20 + slot) * stride + fixture.oscillators[slot-1].selector);
    const concurrent = active.render([], block);
    assert.deepEqual(concurrent[0], reference.render([], block)[0]);
    for (let slot = 1; slot <= 8; slot++) assert.ok(Math.abs(frequency(concurrent[slot], rate) - fixture.oscillators[slot-1].frequency) < 0.3);
    select(1, 0);
    const released = active.render([], block);
    assert.deepEqual(released[0], reference.render([], block)[0]);
    assert.ok(released[1].every(value => value === 0));
});

test('Nested rack aliases share weighted inputs and preserve the modulated master', async () => {
    const nested = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'dynamic-nested-taps.json'), 'utf8'));
    assert.equal(nested.weighted, nested.rackAlias);
    assert.equal(nested.masterAlias, 0);
    const factory = await compiler.createMonoDSPFactory('nestedDynamicTaps', nested.source, '');
    const mono = await compiler.createMonoDSPFactory('nestedMonoTaps', nested.monoSource, '');
    const active = await processor(48000, factory, nested.values), reference = await processor(48000, mono, nested.values);
    setSelector(active, 1, nested.count + 1 + nested.lfo);
    setSelector(active, 2, nested.count + 1 + nested.weighted);
    const output = active.render([], 8192);
    assert.deepEqual(output[0], reference.render([], 8192)[0]);
    for (let i = 0; i < output[1].length; i++) assert.ok(Math.abs(output[2][i] - output[1][i] * 100) < 0.00001);
});

test('Report offline selector overhead for the twelve-oscillator mix', async t => {
    const active = await processor(48000, factory), reference = await processor(48000, mono);
    const block = 48000;
    function medianRender(dsp) {
        dsp.render([], block);
        const timings = Array.from({length: 5}, () => {
            const start = performance.now();
            dsp.render([], block);
            return performance.now() - start;
        }).sort((a, b) => a-b);
        return timings[2];
    }
    const baseline = medianRender(reference), idle = medianRender(active);
    for (let slot = 1; slot <= 8; slot++) setSelector(active, slot, (slot + 1) * (fixture.count + 1) + fixture.oscillators[slot-1].selector);
    const used = medianRender(active);
    t.diagnostic(`Median offline rendering ms per audio second (includes copying output buffers): mono ${baseline.toFixed(2)}, idle slots ${idle.toFixed(2)}, eight active ${used.toFixed(2)}; not an AudioWorklet deadline measurement.`);
});
