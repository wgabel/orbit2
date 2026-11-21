import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'rhythms.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const direct = await compiler.createMonoDSPFactory('RhythmicDetector', fixture.directSource, '');
const graphs = await Promise.all(fixture.rhythms.map(async r => ({ ...r, factory: await compiler.createMonoDSPFactory('Rhythm' + r.kind, r.source, '') })));
const processor = (rate, factory) => new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factory, soundfiles: {} });
const binary = samples => assert.ok(samples.every(v => v === 0 || v === 1));

for (const rate of [44100, 48000]) {
    test(`Schmitt trigger and hysteretic gate have exact binary semantics at ${rate} Hz`, async () => {
        const dsp = await processor(rate, direct);
        const signal = new Float32Array(128);
        signal.set([1, 0.05, 1, 0, 0.05, 0.1, 1, 0.05, 0.1, -1, 0.05, 1, 1, 0]);
        const [pulse, gate] = dsp.render([signal], 128);
        assert.deepEqual([...pulse.slice(0, 14)], [0,0,0,0,0,1,0,0,0,0,0,1,0,0]);
        assert.deepEqual([...gate.slice(0, 14)], [1,1,1,0,0,1,1,1,1,0,0,1,1,0]);
        assert.ok(pulse.slice(14).every(v => v === 0) && gate.slice(14).every(v => v === 0));
    });
    for (const graph of graphs) test(`${graph.kind} fan-out across composite racks and binary mute at ${rate} Hz`, async () => {
        const dsp = await processor(rate, graph.factory);
        const set = (key, value) => dsp.setParamValue(dsp.getParams().find(p => p.endsWith('/' + key)), value);
        for (const [key, value] of Object.entries(graph.values)) set(key, value);
        const n = Math.ceil(rate * 2 / 128) * 128;
        const output = dsp.render([], n);
        const source = output[graph.adapter], nested = output[graph.route];
        binary(source); binary(nested);
        assert.deepEqual(nested, source, 'Rack route is transparent sample-for-sample');
        assert.deepEqual(output[graph.first], output[graph.second], 'Fan-out starts both envelopes on the same sample');
        let rises = 0;
        for (let i = 1; i < n; i++) if (source[i] === 1 && source[i - 1] === 0) {
            rises++;
            if (graph.kind === 'Trigger') assert.equal(source[i + 1], 0);
        }
        assert.ok(rises >= 3);
        assert.ok(output[graph.first].some(v => v > 0.99));
        set(graph.routeMute, 0);
        const mutedRoute = dsp.render([], n);
        assert.ok(mutedRoute[graph.route].every(v => v === 0));
        binary(mutedRoute[graph.adapter]); assert.ok(mutedRoute[graph.adapter].some(v => v === 1));
        set(graph.routeMute, 1); set(graph.mute, 0);
        const mutedAdapter = dsp.render([], n);
        assert.ok(mutedAdapter[graph.adapter].every(v => v === 0) && mutedAdapter[graph.route].every(v => v === 0));
        set(graph.mute, 1);
        const restored = dsp.render([], n);
        binary(restored[graph.adapter]); assert.deepEqual(restored[graph.route], restored[graph.adapter]);
        assert.ok(restored[graph.adapter].some(v => v === 1));
    });
}
