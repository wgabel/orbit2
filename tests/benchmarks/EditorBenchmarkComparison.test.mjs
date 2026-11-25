import test from "node:test";
import assert from "node:assert/strict";
import { compareEditorReports } from "../../scripts/compare-editor-benchmarks.mjs";

function report(revision = "a", fps = [10, 20, 30]) {
    return { kind: "external-editor-benchmark", schemaVersion: 1, fixtureVersion: 1,
        revision: revision.repeat(40), harnessFingerprint: "c".repeat(64),
        environment: { chrome: "test" }, settings: { repeats: 3, expansionMatrix: true },
        rows: [{ id: "editor-50", fingerprint: "d".repeat(64), nodes: 50, connections: 49,
            measurements: fps.map(observedFps => ({ state: "playing", workload: "idle",
                expansion: { name: "all-folded", eligibleLeaves: 49, expandedNodeIds: [] },
                observedFps, frameGapsMs: { p95: 50 }, cdp: { ScriptDuration: .1, JSHeapUsedSize: -10 },
                longTasksMs: [], geometry: { domElements: 200 } })) }] };
}
test("paired reports compare repeat medians, preserve missing/zero and signed heap values", () => {
    const result = compareEditorReports(report(), report("b", [30, 40, 50]));
    const c = result.rows[0].changes;
    assert.deepEqual(c.fps, { before: 20, after: 40, delta: 20, percentChange: 100 });
    assert.deepEqual(c.longTasks, { before: 0, after: 0, delta: 0, percentChange: null });
    assert.deepEqual(c.frameRenders, { before: null, after: null, delta: null, percentChange: null });
    assert.equal(c.scriptMs.before, 100);
    assert.equal(c.heapDeltaBytes.before, -10);
});
test("reject mismatched provenance, coverage, and incomplete repetitions", () => {
    for (const change of [
        r => { r.revision = null; },
        r => { r.harnessFingerprint = "e".repeat(64); },
        r => { r.environment.chrome = "other"; },
        r => { r.settings.expansionMatrix = false; },
        r => { r.rows[0].fingerprint = "e".repeat(64); },
        r => { r.rows[0].measurements[0].expansion.name = "one-expanded"; },
        r => { r.rows[0].measurements.pop(); }
    ]) {
        const candidate = report("b");
        change(candidate);
        assert.throws(() => compareEditorReports(report(), candidate));
    }
});
test("partial missing metric is not treated as a valid zero or partial median", () => {
    const candidate = report("b");
    candidate.rows[0].measurements[0].observedFps = null;
    assert.deepEqual(compareEditorReports(report(), candidate).rows[0].changes.fps,
        { before: 20, after: null, delta: null, percentChange: null });
});
