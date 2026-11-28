import test from "node:test";
import assert from "node:assert/strict";
import { validateOfflineParity, validateLiveParity } from "../../scripts/validate-dsp-regression.mjs";

const base = { schemaVersion: 1, fixtureVersion: 1, revision: "a".repeat(40), environment: {} };
function offline() {
    const fixture = { fixtures: ["mono", "taps"].map((id, i) => ({ id, expectedChannels: i + 1, activeTapSlots: i,
        pairedMono: i ? "mono" : null, masterPeakLimit: 1 })) };
    const settings = { seconds: 1, repeats: 5, rates: [44100, 48000], blockSize: 128 };
    const rows = fixture.fixtures.flatMap(p => settings.rates.map(rate => ({
        ...p, sampleRateHz: rate, frames: Math.ceil(rate / 128) * 128, warmupFrames: Math.ceil(rate / 128) * 128,
        sourceSha256: "b".repeat(64), validation: { finite: true, peakLimit: 1, masterPeak: .5,
            masterRms: { count: 5, samples: Array(5).fill(.25) },
            masterHashes: Array(5).fill("c".repeat(64)), pairedMasterIdentical: p.pairedMono ? true : null }
    })));
    return [fixture, { ...base, kind: "offline-dsp-benchmark", settings, rows: structuredClone(rows) },
        { ...base, kind: "offline-dsp-regression", settings: { ...settings, timingCollected: false }, rows: structuredClone(rows) }];
}
function live() {
    const rows = ["sine-bank-1", "sine-bank-10"].map((id, i) => ({ id,
        initial: { "Faust source builds": "1" }, steady: { "Faust source builds": "0", "Voice preparation attempts": "0" },
        stopped: {}, replay: { "Faust source builds": "1" }, edits: i ? null :
            { control: { "Faust source builds": "0", "Live control updates": "1" },
                replacement: { "Faust source builds": "1", "Accepted replacements": "1" } } }));
    const settings = { plotViews: 0, monitorOpenDuringRendererSampling: false };
    return [{ ...base, kind: "external-live-renderer-benchmark", settings, rows: structuredClone(rows) },
        { ...base, kind: "external-live-renderer-regression", settings: { ...settings, timingCollected: false }, rows: structuredClone(rows) }];
}
test("DSP parity covers both rates, repeats, paired master hashes and both live profiles", () => {
    assert.equal(validateOfflineParity(...offline()), 4);
    assert.equal(validateLiveParity(...live()), 2);
});
test("offline parity rejects jointly missing coverage, mismatched paired output and leaked timing", () => {
    let a = offline(); a[1].rows.pop(); a[2].rows.pop();
    assert.throws(() => validateOfflineParity(...a), /coverage mismatch/);
    a = offline(); a[2].rows[2].validation.masterHashes[0] = "d".repeat(64);
    assert.throws(() => validateOfflineParity(...a), /Paired master hash mismatch/);
    a = offline(); a[2].rows[0].offlineRenderMs = {};
    assert.throws(() => validateOfflineParity(...a), /Timing leaked/);
});
test("live parity rejects absent lifecycle assertions, missing profile and leaked capacity metrics", () => {
    let a = live(); delete a[1].rows[0].edits.replacement["Accepted replacements"];
    assert.throws(() => validateLiveParity(...a));
    a = live(); a[1].rows.pop();
    assert.throws(() => validateLiveParity(...a), /coverage mismatch/);
    a = live(); a[1].rows[0].observations = [];
    assert.throws(() => validateLiveParity(...a), /Timing leaked/);
});
