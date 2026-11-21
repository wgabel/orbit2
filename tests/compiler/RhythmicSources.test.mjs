import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixture = JSON.parse(await readFile(join(process.env.ORBIT_GRAPH_PROBE_DIR, 'rhythmic-sources.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } = await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factories = {};
for (const key of ['source', 'clockSource', 'freeSource', 'gateSource']) factories[key] = await compiler.createMonoDSPFactory(key, fixture[key], '');
const dsp = (rate, key) => new FaustMonoDspGenerator().createOfflineProcessor(rate, 128, { ...factories[key], soundfiles: {} });
const samples = (n, fn) => Float32Array.from({ length: n }, (_, i) => fn(i));
const pulses = signal => [...signal.keys()].filter(i => signal[i] === 1);
const checkBinary = signal => assert.ok(signal.every(v => v === 0 || v === 1));
const near = (actual, expected, tolerance = 1) => {
    assert.equal(actual.length, expected.length);
    for (let i = 0; i < expected.length; i++) assert.ok(Math.abs(actual[i] - expected[i]) <= tolerance, `${actual[i]} vs ${expected[i]}`);
};

for (const rate of [44100, 48000]) {
    test(`Timed gate latches duration, retriggers and closes exactly at ${rate}`, async () => {
        const processor = await dsp(rate, 'gateSource');
        const action = new Float32Array(1024);
        action[16] = action[100] = action[500] = 1; action[550] = 2;
        const ms = samples(1024, i => i < 101 ? 5 : 10);
        const [gate] = processor.render([action, ms], 1024);
        const end = 100 + Math.round(rate * .005);
        assert.deepEqual(gate, samples(1024, i => (i >= 16 && i < end) || (i >= 500 && i < 550) ? 1 : 0));
    });
    test(`Free clock starts idle and emits one pulse per period at ${rate}`, async () => {
        const n = Math.ceil(rate * 1.8 / 128) * 128;
        const [clock] = (await dsp(rate, 'freeSource')).render([], n);
        checkBinary(clock); near(pulses(clock), [rate / 2, rate, rate * 1.5], 2);
    });
    test(`Clock follows every musical division through transport wrap at ${rate}`, async () => {
        const n = Math.ceil(rate * 3 / 128) * 128;
        for (const [index, division] of [.25, .5, 1, 2, 4, 2/3, 1.5].entries()) {
            const beat = samples(n, i => (11.5 + i * 2 / rate) % 12);
            const reset = new Float32Array(n); reset[0] = 1;
            const choice = samples(n, () => index);
            const [clock] = (await dsp(rate, 'clockSource')).render([beat, reset, choice], n);
            const expected = [];
            for (let k = Math.floor(11.5 / division) + 1; k * division < 11.5 + n * 2 / rate; k++) expected.push(Math.ceil((k * division - 11.5) * rate / 2));
            checkBinary(clock); near(pulses(clock), expected);
        }
    });
    test(`Clock suppresses resets, frozen transport and division changes at ${rate}`, async () => {
        const n = Math.ceil(rate * 2 / 128) * 128;
        const beat = samples(n, i => i < rate / 2 ? i * 2 / rate : i < rate ? 0 : (i - rate) * 2 / rate);
        const reset = new Float32Array(n); reset[0] = reset[rate / 2] = reset[rate] = 1;
        const choice = samples(n, i => i < rate * 1.25 ? 2 : 1);
        const [clock] = (await dsp(rate, 'clockSource')).render([beat, reset, choice], n);
        checkBinary(clock); near(pulses(clock), [rate * 1.5, rate * 1.75, ...(n > rate * 2 ? [rate * 2] : [])]);
    });
    test(`Generated graph routes manual commands and clock gates at ${rate}`, async () => {
        const processor = await dsp(rate, 'source');
        for (const [key, value] of Object.entries(fixture.values)) processor.setParamValue(processor.getParams().find(p => p.endsWith('/' + key)), value);
        const n = Math.ceil(rate * .85 / 128) * 128;
        const input = Array.from({length: fixture.phaseChannels + fixture.actions.length}, () => new Float32Array(n));
        input[0] = samples(n, i => (11.5 + i * 2 / rate) % 12); input[1][0] = 1;
        input[fixture.phaseChannels + fixture.manualIndex][200] = input[fixture.phaseChannels + fixture.manualIndex][400] = 1;
        input[fixture.phaseChannels + fixture.gateIndex][400] = 1; input[fixture.phaseChannels + fixture.gateIndex][1100] = 2;
        const output = processor.render(input, n);
        assert.equal(output.length, fixture.channels);
        assert.deepEqual(pulses(output[fixture.manual]), [200, 400]);
        assert.deepEqual(output[fixture.manualGate], samples(n, i => i >= 400 && i < 1100 ? 1 : 0));
        const clock = output[fixture.clock], gate = output[fixture.clockGate];
        checkBinary(clock); checkBinary(gate); near(pulses(clock), [rate / 4, rate * .75]);
        for (const i of pulses(clock)) { assert.equal(gate[i-1], 0); assert.equal(gate[i], 1); assert.equal(gate[i+Math.round(rate*.05)-1], 1); assert.equal(gate[i+Math.round(rate*.05)], 0); }
    });
}
