import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const rates = [44100, 48000];
const liveIds = ["sine-bank-1", "sine-bank-10"];
const offlineTiming = ["sourceGenerationMs", "factoryCompilationMs", "instanceCreationMs",
    "parameterBindingMs", "warmupMs", "offlineRenderMs", "offlineRealtimeFactor",
    "throughputTimesRealtime", "framesPerWallSecond"];
const liveTiming = ["initialPlayWallMs", "replayWallMs", "renderCapacityPercent", "observations", "availability"];

function provenance(full, fast, fullKind, fastKind) {
    assert.equal(full.kind, fullKind);
    assert.equal(fast.kind, fastKind);
    for (const report of [full, fast]) {
        assert.equal(report.schemaVersion, 1);
        assert.equal(report.fixtureVersion, 1);
        assert.match(report.revision, /^[a-f0-9]{40}$/);
    }
    assert.equal(fast.revision, full.revision);
    assert.deepEqual(fast.environment, full.environment);
    assert.equal(fast.settings.timingCollected, false);
}

export function validateOfflineParity(fixture, full, fast) {
    provenance(full, fast, "offline-dsp-benchmark", "offline-dsp-regression");
    const { timingCollected, ...settings } = fast.settings;
    assert.deepEqual(settings, full.settings);
    assert.deepEqual(settings.rates, rates);
    assert.equal(settings.blockSize, 128);
    assert.ok(Number.isInteger(settings.repeats) && settings.repeats >= 3);
    assert.ok(Number.isInteger(settings.seconds) && settings.seconds >= 1);
    assert.ok(fixture.fixtures.length > 0);
    assert.ok(!("compilerInitializationMs" in fast));
    const expected = fixture.fixtures.flatMap(p => rates.map(rate => [p.id, rate]));
    for (const report of [full, fast]) {
        assert.deepEqual(report.rows.map(r => [r.id, r.sampleRateHz]), expected, "Offline profile/rate coverage mismatch");
        const hashes = new Map();
        for (const [i, row] of report.rows.entries()) {
            const profile = fixture.fixtures[Math.floor(i / rates.length)];
            assert.equal(row.expectedChannels, profile.expectedChannels);
            assert.equal(row.activeTapSlots, profile.activeTapSlots);
            assert.equal(row.pairedMono ?? null, profile.pairedMono ?? null);
            assert.equal(row.frames, Math.ceil(row.sampleRateHz * settings.seconds / 128) * 128);
            assert.equal(row.warmupFrames, Math.ceil(row.sampleRateHz / 128) * 128);
            assert.match(row.sourceSha256, /^[a-f0-9]{64}$/);
            const v = row.validation;
            assert.equal(v.finite, true);
            assert.equal(v.peakLimit, profile.masterPeakLimit);
            assert.ok(Number.isFinite(v.masterPeak) && v.masterPeak > 0 && v.masterPeak <= v.peakLimit);
            assert.equal(v.masterRms.count, settings.repeats);
            assert.ok(v.masterRms.samples.every(x => Number.isFinite(x) && x > 1e-6));
            assert.equal(v.masterHashes.length, settings.repeats);
            for (const hash of v.masterHashes) assert.match(hash, /^[a-f0-9]{64}$/);
            if (row.pairedMono) {
                assert.equal(v.pairedMasterIdentical, true);
                assert.deepEqual(v.masterHashes, hashes.get(row.pairedMono + ":" + row.sampleRateHz), "Paired master hash mismatch");
            } else assert.equal(v.pairedMasterIdentical, null);
            hashes.set(row.id + ":" + row.sampleRateHz, v.masterHashes);
        }
    }
    for (const [i, row] of fast.rows.entries()) {
        assert.equal(row.sourceSha256, full.rows[i].sourceSha256);
        for (const field of offlineTiming) assert.ok(!(field in row), "Timing leaked: " + field);
    }
    return expected.length;
}

export function validateLiveParity(full, fast) {
    provenance(full, fast, "external-live-renderer-benchmark", "external-live-renderer-regression");
    assert.equal(fast.settings.plotViews, full.settings.plotViews);
    assert.equal(fast.settings.monitorOpenDuringRendererSampling, false);
    for (const report of [full, fast]) {
        assert.deepEqual(report.rows.map(r => r.id), liveIds, "Live profile coverage mismatch");
        for (const row of report.rows) {
            assert.equal(row.initial["Faust source builds"], "1");
            assert.equal(row.steady["Faust source builds"], "0");
            assert.equal(row.steady["Voice preparation attempts"], "0");
            assert.equal(row.replay["Faust source builds"], "1");
            assert.ok(row.stopped && typeof row.stopped === "object");
            if (row.id === "sine-bank-1") {
                assert.equal(row.edits.control["Faust source builds"], "0");
                assert.equal(row.edits.control["Live control updates"], "1");
                assert.equal(row.edits.replacement["Faust source builds"], "1");
                assert.equal(row.edits.replacement["Accepted replacements"], "1");
            } else assert.equal(row.edits, null);
        }
    }
    for (const row of fast.rows) {
        for (const field of liveTiming) assert.ok(!(field in row), "Timing leaked: " + field);
        if (row.edits) for (const field of ["liveControlWallMs", "replacementWallMs"])
            assert.ok(!(field in row.edits), "Timing leaked: " + field);
    }
    return liveIds.length;
}

export async function validateDspRegression(directory) {
    const read = async file => JSON.parse(await readFile(join(directory, file), "utf8"));
    return { offlineRows: validateOfflineParity(await read("fixtures.json"), await read("offline-report.json"), await read("offline-regression.json")),
        liveProfiles: validateLiveParity(await read("live-report.json"), await read("live-regression.json")) };
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    assert.equal(process.argv.length, 3, "Usage: validate-dsp-regression.mjs <fixture-directory>");
    console.log("DSP REGRESSION PARITY " + JSON.stringify(await validateDspRegression(process.argv[2])));
}
