import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { cpus, platform, release, arch } from 'node:os';

const directory = process.env.ORBIT_GRAPH_PROBE_DIR;
const fixture = JSON.parse(await readFile(join(directory, 'connection-amount-baseline.json'), 'utf8'));
const faust = process.env.ORBIT_FAUST_DIR;
const packageInfo = JSON.parse(await readFile(join(faust, 'package.json'), 'utf8'));
assert.equal(packageInfo.version, fixture.faustVersion);
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, 'dist/esm/index.js')));
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, 'libfaust-wasm/libfaust-wasm.js'))));
const factories = new Map();
const hash = data => createHash('sha256').update(data).digest('hex');
const bytes = samples => Buffer.from(samples.buffer, samples.byteOffset, samples.byteLength);

async function render(rate, program) {
    let factory = factories.get(program.source), compileMs = null;
    if (!factory) {
        const start = performance.now();
        factory = await compiler.createMonoDSPFactory('ConnectionAmountReference', program.source, '');
        compileMs = performance.now() - start;
        assert.ok(factory);
        factories.set(program.source, factory);
    }
    const renderMs = [], omitted = new Set(), hashes = [];
    let master, peak = 0, energy = 0;
    for (let repeat = 0; repeat < fixture.repeats; repeat++) {
        const dsp = await new FaustMonoDspGenerator().createOfflineProcessor(rate, fixture.blockSize,
            { ...factory, soundfiles: {} });
        const paths = dsp.getParams();
        for (const [name, value] of Object.entries(program.values)) {
            const matches = paths.filter(p => p.endsWith('/' + name));
            assert.ok(matches.length <= 1, 'Ambiguous control ' + name);
            // Match the runtime's initial binding semantics: literal-zero branches
            // may eliminate controls. Preserve omitted names as baseline evidence.
            if (matches.length === 0) omitted.add(name);
            else dsp.setParamValue(matches[0], value);
        }
        const start = performance.now();
        const audio = dsp.render([], fixture.frames);
        renderMs.push(performance.now() - start);
        assert.equal(audio.length, program.channels);
        for (const channel of audio) {
            assert.equal(channel.length, fixture.frames);
            for (const sample of channel) assert.ok(Number.isFinite(sample));
        }
        const samples = audio[0];
        hashes.push(hash(bytes(samples)));
        if (repeat === 0) {
            master = new Float32Array(samples);
            for (const sample of master) {
                peak = Math.max(peak, Math.abs(sample));
                energy += sample * sample;
            }
        }
    }
    assert.ok(hashes.every(h => h === hashes[0]), 'Reference render must repeat exactly');
    return { master, report: { sourceHash: hash(program.source), sampleHash: hashes[0],
        sourceGenerationMs: program.sourceGenerationMs, compileMs, renderMs,
        peak, rms: Math.sqrt(energy / fixture.frames), omitted: [...omitted].sort(),
        channels: program.channels, selectorCount: program.selectorCount } };
}

test('static signed, zero and unity coefficient reference audio', async () => {
    assert.equal(fixture.cases.length, 9);
    const results = [];
    for (const rate of [44100, 48000]) {
        const signals = new Map();
        for (const item of fixture.cases) {
            const mono = await render(rate, item.mono);
            const observed = await render(rate, item.observed);
            assert.equal(mono.report.sampleHash, observed.report.sampleHash, 'Idle observations changed master');
            assert.ok(mono.report.peak <= 1.0001);
            if (item.audio && item.amount === 0) assert.equal(mono.report.peak, 0);
            else assert.ok(mono.report.peak > 0.001, 'Unexpected silent reference');
            const filename = `connection-amount-${item.name}-${item.amount}-${rate}.f32`;
            await writeFile(join(directory, filename), bytes(mono.master));
            signals.set(item.name + ':' + item.amount, mono.master);
            const row = { name: item.name, amount: item.amount, rate, graphId: item.graphId,
                edgeId: item.edgeId, patchHash: hash(item.patch), sampleFile: filename,
                mono: mono.report, observed: observed.report };
            results.push(row);
            console.log('CONNECTION_AMOUNT_BASELINE ' + JSON.stringify(row));
        }
        for (const name of ['root-audio', 'nested-audio']) {
            const negative = signals.get(name + ':-1'), positive = signals.get(name + ':1');
            let maxError = 0;
            for (let i = 0; i < positive.length; i++)
                maxError = Math.max(maxError, Math.abs(positive[i] + negative[i]));
            assert.ok(maxError < 1e-6, name + ' signed route must invert the reference');
        }
        const positiveMod = signals.get('root-modulation:100');
        const zeroMod = signals.get('root-modulation:0');
        const negativeMod = signals.get('root-modulation:-100');
        assert.notEqual(hash(bytes(positiveMod)), hash(bytes(zeroMod)));
        assert.notEqual(hash(bytes(negativeMod)), hash(bytes(positiveMod)));
    }
    await writeFile(join(directory, 'connection-amount-report.json'), JSON.stringify({
        schemaVersion: 1, fixtureVersion: fixture.fixtureVersion, revision: process.env.GITHUB_SHA ?? null,
        environment: { node: process.version, faustwasm: packageInfo.version, platform: platform(),
            release: release(), arch: arch(), cpu: cpus()[0]?.model ?? null },
        blockSize: fixture.blockSize, frames: fixture.frames, repeats: fixture.repeats,
        scope: 'Offline WASM render including output allocation/copy; not live DSP load',
        results
    }, null, 2));
});
