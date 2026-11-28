import test from "node:test";
import assert from "node:assert/strict";
import { coverage, expectedWindows, validatePair } from "../../scripts/validate-editor-regression-suite.mjs";

function pair(spec) {
    const settings = { repeats: 1, idleMs: spec.untimed ? 1000 : 500, mode: spec.mode ?? "desktop", playback: spec.playback !== false,
        ...Object.fromEntries(["countRenders", "expansionMatrix", "compositeResources", "compositeTelemetry", "foldedResources"].map(flag => [flag, spec.flag === flag])) };
    const base = { schemaVersion: 1, fixtureVersion: 1, revision: "a".repeat(40), harnessFingerprint: "b".repeat(64), environment: {} };
    const rows = spec.ids.map(id => {
        const geometry = { renderedNodes: 50, renderedConnections: 49 };
        return { id, fingerprint: "c".repeat(64), nodes: 50, connections: 49, initialGeometry: geometry, finalGeometry: geometry,
            foldedTelemetry: spec.flag === "countRenders" ? { foldedNodes: 2, foldedFrameRenders: 0, expandedControlRenders: 1 } : null,
            resourceLifecycle: null, canvasTelemetry: null, foldedResourceLifecycle: null,
            ...(spec.lifecycle ? { [spec.lifecycle[0]]: { steps: spec.lifecycle[1].map(name => ({ name })) } } : {}) };
    });
    const checks = expectedWindows(spec).map(({ state, workload, scope }) => ({ state, workload, expansion: scope ? { name: scope } : null,
        nodeRenders: spec.flag === "countRenders" ? { total: 1, affectedNodes: 1 } : null,
        ...(workload === "parameter-edit" ? { parameterAccess: "pointer-hit-testable" } : {}) }));
    const full = { ...base, kind: "external-editor-benchmark", settings: { ...settings, cdpDurationUnit: "seconds" }, rows: rows.map(row => ({ ...row, measurements: checks })) };
    const fast = { ...base, kind: "external-editor-regression", settings: { ...settings, timingCollected: false }, rows: rows.map(row => ({ ...row, checks })) };
    return [structuredClone(full), structuredClone(fast)];
}

test("eight report pairs independently require nine contexts and 82 action windows", () => {
    assert.equal(coverage.length, 8);
    assert.equal(coverage.reduce((n, s) => n + s.ids.length, 0), 9);
    assert.equal(coverage.reduce((n, s) => n + validatePair(s, ...pair(s)), 0), 82);
});

test("reject missing workload even when both modes omit it, wrong fixture, scope or provenance", () => {
    const spec = coverage[4];
    let [full, fast] = pair(spec);
    full.rows[0].measurements.pop(); fast.rows[0].checks.pop();
    assert.throws(() => validatePair(spec, full, fast), /Full workload coverage mismatch/);
    [full, fast] = pair(spec); fast.rows[0].checks[0].expansion = { name: "all-expanded", expandedNodeIds: ["foreign"] };
    assert.throws(() => validatePair(spec, full, fast), /Scope identity mismatch/);
    [full, fast] = pair(spec); fast.rows[0].id = "wrong";
    assert.throws(() => validatePair(spec, full, fast), /Fixture coverage mismatch/);
    [full, fast] = pair(spec); fast.rows[0].fingerprint = "d".repeat(64);
    assert.throws(() => validatePair(spec, full, fast));
    [full, fast] = pair(spec); fast.revision = "b".repeat(40);
    assert.throws(() => validatePair(spec, full, fast));
});

test("reject timing contamination, absent counters, lifecycle omissions and wrong scenario flags", () => {
    let spec = coverage[3], [full, fast] = pair(spec);
    fast.rows[0].checks[0].observedFps = 60;
    assert.throws(() => validatePair(spec, full, fast), /Timing leaked/);
    [full, fast] = pair(spec); fast.rows[0].checks[0].nodeRenders = null;
    assert.throws(() => validatePair(spec, full, fast));
    [full, fast] = pair(spec); fast.settings.countRenders = false;
    assert.throws(() => validatePair(spec, full, fast), /Scenario flag mismatch/);
    spec = coverage[7]; [full, fast] = pair(spec);
    fast.rows[0].foldedResourceLifecycle.steps.pop();
    assert.throws(() => validatePair(spec, full, fast), /Lifecycle coverage mismatch/);
});
