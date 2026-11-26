import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const directory = process.env.ORBIT_GRAPH_PROBE_DIR;
const fixture = JSON.parse(await readFile(join(directory, 'static-macro-baseline.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factories = new Map();
const hash = data => createHash('sha256').update(data).digest('hex');

async function render(rate, program) {
    if (!factories.has(program.source))
        factories.set(program.source, await compiler.createMonoDSPFactory('StaticMacroReference', program.source, ''));
    const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, fixture.blockSize,
        { ...factories.get(program.source), soundfiles: {} });
    for (const [name, value] of Object.entries(program.values)) {
        const path = dsp.getParams().find(p => p.endsWith('/' + name));
        assert.ok(path, name);
        dsp.setParamValue(path, value);
    }
    const [samples] = dsp.render([], fixture.frames);
    assert.equal(samples.length, fixture.frames);
    return samples;
}

function stats(samples, rate) {
    let peak = 0, energy = 0, maxStep = 0, previous = 0;
    const crossings = [];
    for (let i = 0; i < samples.length; i++) {
        const sample = samples[i];
        assert.ok(Number.isFinite(sample));
        peak = Math.max(peak, Math.abs(sample));
        energy += sample * sample;
        maxStep = Math.max(maxStep, Math.abs(sample - previous));
        if (i > rate * 0.2 && previous <= 0 && sample > 0)
            crossings.push(i - 1 - previous / (sample - previous));
        previous = sample;
    }
    assert.ok(crossings.length > 2, 'Expected audible periodic reference');
    return { peak, rms: Math.sqrt(energy / samples.length), maxStep,
        frequency: (crossings.length - 1) * rate / (crossings.at(-1) - crossings[0]),
        sampleHash: hash(Buffer.from(samples.buffer, samples.byteOffset, samples.byteLength)) };
}

test('nested static macro reference at both sample rates', async () => {
    const results = [];
    assert.deepEqual(fixture.cases.map(c => c.name), ['zero', 'middle', 'one', 'activated-square', 'removed']);
    for (const rate of [44100, 48000]) {
        for (const item of fixture.cases) {
            assert.equal(item.candidate.source, item.reference.source);
            const candidate = await render(rate, item.candidate);
            const reference = await render(rate, item.reference);
            const measured = stats(candidate, rate);
            stats(reference, rate);
            let maxError = 0;
            for (let i = 0; i < candidate.length; i++)
                maxError = Math.max(maxError, Math.abs(candidate[i] - reference[i]));
            assert.ok(maxError < 1e-6, `${item.name} at ${rate}: paired sample error ${maxError}`);
            assert.ok(measured.peak > .01 && measured.peak <= 1.0001);
            assert.ok(Math.abs(measured.frequency - item.expectedFrequency) < 1,
                `${item.name} at ${rate}: expected ${item.expectedFrequency}, got ${measured.frequency}`);
            const row = { name: item.name, rate, expectedFrequency: item.expectedFrequency,
                ...measured, maxError, sourceHash: hash(item.candidate.source) };
            results.push(row);
            console.log('STATIC_MACRO_BASELINE ' + JSON.stringify(row));
        }
    }
    await writeFile(join(directory, 'static-macro-report.json'), JSON.stringify({
        schemaVersion: 1, revision: process.env.GITHUB_SHA ?? null, nodeVersion: process.version,
        faustVersion: fixture.faustVersion, blockSize: fixture.blockSize, frames: fixture.frames, results
    }, null, 2));
});
