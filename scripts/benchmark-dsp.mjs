// Explicit developer-only benchmark; never runs in the interactive audio path.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { resolve, join } from "node:path";
import { pathToFileURL } from "node:url";
import { cpus, platform, release, arch } from "node:os";
import { summarize, throughput, inspectOutput } from "./benchmark-metrics.mjs";

const regression = process.argv[2] === "--regression";
const [directory, outputFile, ...rest] = process.argv.slice(regression ? 3 : 2);
assert.ok(directory && outputFile && !rest.length && process.env.ORBIT_FAUST_DIR,
    "Usage: ORBIT_FAUST_DIR=... node scripts/benchmark-dsp.mjs [--regression] <fixture-directory> <report.json>");
function bounded(name, fallback, minimum, maximum) {
    const value = Number(process.env[name] ?? fallback);
    assert.ok(Number.isInteger(value) && value >= minimum && value <= maximum, name + " out of bounds");
    return value;
}
const seconds = bounded("ORBIT_BENCH_SECONDS", 2, 1, 10);
const repeats = bounded("ORBIT_BENCH_REPEATS", 5, 3, 10);
const blockSize = 128;
const rates = [44100, 48000];
const fixture = JSON.parse(await readFile(join(directory, "fixtures.json"), "utf8"));
assert.equal(fixture.schemaVersion, 1);
assert.equal(fixture.fixtureVersion, 1);
assert.ok(fixture.fixtures.length > 0 && fixture.fixtures.length <= 16);
const faust = resolve(process.env.ORBIT_FAUST_DIR);
const packageInfo = JSON.parse(await readFile(join(faust, "package.json"), "utf8"));
assert.equal(packageInfo.version, "0.19.0", "Use the pinned Faust distribution");
const { instantiateFaustModuleFromFile, LibFaust, FaustCompiler, FaustMonoDspGenerator } =
    await import(pathToFileURL(join(faust, "dist/esm/index.js")));
const clock = () => regression ? 0 : performance.now();
let start = clock();
const compiler = new FaustCompiler(new LibFaust(await instantiateFaustModuleFromFile(join(faust, "libfaust-wasm/libfaust-wasm.js"))));
const compilerInitializationMs = clock() - start;
const hash = value => createHash("sha256").update(value).digest("hex");
const factories = new Map(), references = new Map(), seen = new Set(), rows = [];
for (const profile of fixture.fixtures) {
    const { id, source, values, selections, expectedChannels, activeTapSlots, masterPeakLimit, pairedMono, sourceGenerationMs, ...metadata } = profile;
    assert.ok(!seen.has(id), "Duplicate profile ID");
    seen.add(id);
    const sourceSha256 = hash(source);
    let factory = factories.get(sourceSha256);
    let factoryCompilationMs = null;
    if (!factory) {
        start = clock();
        factory = await compiler.createMonoDSPFactory("OrbitGraph", source, "");
        factoryCompilationMs = clock() - start;
        assert.ok(factory, "Could not compile " + id);
        factories.set(sourceSha256, factory);
    }
    for (const rate of rates) {
        const frames = Math.ceil(rate * seconds / blockSize) * blockSize;
        const warmupFrames = Math.ceil(rate / blockSize) * blockSize;
        const creation = [], binding = [], warmup = [], render = [], peaks = [], rms = [];
        const masterHashes = [];
        for (let repeat = 0; repeat < repeats; repeat++) {
            start = clock();
            const processor = await new FaustMonoDspGenerator().createOfflineProcessor(rate, blockSize, { ...factory, soundfiles: {} });
            creation.push(clock() - start);
            assert.ok(processor);
            start = clock();
            const paths = processor.getParams();
            for (const [key, value] of Object.entries({ ...values, ...selections })) {
                const matches = paths.filter(p => p.endsWith("/" + key));
                assert.equal(matches.length, 1, "Missing or ambiguous control " + key);
                processor.setParamValue(matches[0], value);
            }
            binding.push(clock() - start);
            start = clock();
            processor.render([], warmupFrames);
            warmup.push(clock() - start);
            // This times the vendor's offline render call, including output allocation/copying.
            // Validation, hashing and report serialization are outside the measured interval.
            start = clock();
            const audio = processor.render([], frames);
            render.push(clock() - start);
            const channels = inspectOutput(audio, frames, expectedChannels, masterPeakLimit, activeTapSlots);
            peaks.push(channels[0].peak);
            rms.push(channels[0].rms);
            const master = audio[0];
            masterHashes.push(hash(Buffer.from(master.buffer, master.byteOffset, master.byteLength)));
        }
        if (pairedMono) assert.deepEqual(masterHashes, references.get(pairedMono + ":" + rate),
            "Dynamic taps changed the master signal");
        references.set(id + ":" + rate, masterHashes);
        const renderMs = regression ? null : summarize(render);
        rows.push({ id, ...metadata, sampleRateHz: rate, frames, warmupFrames, blockSize, expectedChannels,
            activeTapSlots, pairedMono, sourceSha256, factoryReused: factoryCompilationMs === null,
            ...(regression ? {} : { sourceGenerationMs: summarize(sourceGenerationMs), factoryCompilationMs,
                instanceCreationMs: summarize(creation), parameterBindingMs: summarize(binding), warmupMs: summarize(warmup),
                offlineRenderMs: renderMs, ...throughput(frames, rate, renderMs.median) }),
            validation: { masterPeak: Math.max(...peaks), masterRms: summarize(rms), masterHashes,
                finite: true, peakLimit: masterPeakLimit, pairedMasterIdentical: pairedMono ? true : null } });
        console.log(regression ? `${id} @ ${rate} Hz: ${repeats} validated renders, ${expectedChannels} output(s)` :
            `${id} @ ${rate} Hz: ${renderMs.median.toFixed(2)} ms median offline render, ${rows.at(-1).throughputTimesRealtime.toFixed(1)}x realtime, ${expectedChannels} output(s)`);
    }
}
const report = { schemaVersion: 1, fixtureVersion: fixture.fixtureVersion, kind: regression ? "offline-dsp-regression" : "offline-dsp-benchmark",
    scope: regression ? "FaustWasm offline render correctness; no performance measurements" :
        "FaustWasm offline render including output allocation/copying; not live renderer or CPU load",
    revision: process.env.ORBIT_REVISION ?? process.env.GITHUB_SHA ?? null, timestampUtc: new Date().toISOString(),
    environment: { node: process.version, faustwasm: packageInfo.version, platform: platform(), release: release(), arch: arch(),
        logicalCpuCount: cpus().length, cpuModel: cpus()[0]?.model ?? null },
    settings: { seconds, repeats, rates, blockSize, large: fixture.large, ...(regression ? { timingCollected: false } : {}) },
    ...(regression ? {} : { compilerInitializationMs }), rows };
await writeFile(outputFile, JSON.stringify(report, null, 2) + "\n");
