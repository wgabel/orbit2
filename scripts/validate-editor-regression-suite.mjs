import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// Independent acceptance inventory: the plan under test must not define its own expected coverage.
const standard = ["idle", "pan", "wheel-zoom", "node-drag", "parameter-edit"];
const matrix = ["idle", "pan", "wheel-zoom"];
const expansion = ["all-expanded", "five-expanded", "one-expanded", "all-folded"];
const composite = ["composite-closed", "composite-open", "composite-reclosed"];
const resources = ["root-no-plots", "child-no-plots", "compact-live", "folded-compact", "compact-reopened",
    "independent-scope", "folded-independent-scope", "folded-scope-closed", "compact-before-navigation",
    "root-child-plot-inactive", "child-plot-rebound", "child-plot-hidden", "root-final"];
const telemetry = ["root-closed", "child-open", "root-reclosed"];
const folded = ["one-expanded-compact", "all-folded-no-scope", "one-expanded-rebound",
    "all-folded-independent-scope", "all-folded-scope-closed", "one-expanded-plot-hidden"];

export const coverage = [
    { report: "editor-report.json", ids: ["editor-50", "editor-200"] },
    { report: "editor-500-stopped.json", ids: ["editor-500"], playback: false },
    { report: "editor-mobile-stopped.json", ids: ["editor-200"], playback: false, mode: "mobile" },
    { report: "editor-render-counts.json", ids: ["editor-50"], flag: "countRenders" },
    { report: "editor-expansion-matrix.json", ids: ["editor-50"], flag: "expansionMatrix", scopes: expansion },
    { report: "editor-composite-100.json", ids: ["editor-composite-100"], flag: "compositeResources", scopes: composite, lifecycle: ["resourceLifecycle", resources] },
    { report: "editor-composite-telemetry.json", ids: ["editor-composite-100"], flag: "compositeTelemetry", lifecycle: ["canvasTelemetry", telemetry], untimed: true },
    { report: "editor-folded-resources.json", ids: ["editor-50"], flag: "foldedResources", lifecycle: ["foldedResourceLifecycle", folded], untimed: true }
];

export function expectedWindows(spec) {
    if (spec.untimed) return [];
    return (spec.playback === false ? ["stopped"] : ["stopped", "playing"]).flatMap(state =>
        (spec.scopes ?? [null]).flatMap(scope => (scope ? matrix : standard).map(workload => ({ state, workload, scope }))));
}

const windowKey = ({ state, workload, expansion }) => ({ state, workload, scope: expansion?.name ?? null });
const flags = ["countRenders", "expansionMatrix", "compositeResources", "compositeTelemetry", "foldedResources"];
const lifecycleFields = ["foldedTelemetry", "resourceLifecycle", "canvasTelemetry", "foldedResourceLifecycle"];

export function validatePair(spec, full, fast) {
    assert.equal(full.kind, "external-editor-benchmark");
    assert.equal(fast.kind, "external-editor-regression");
    for (const report of [full, fast]) {
        assert.equal(report.schemaVersion, 1);
        assert.equal(report.fixtureVersion, 1);
        assert.match(report.revision, /^[a-f0-9]{40}$/);
        assert.match(report.harnessFingerprint, /^[a-f0-9]{64}$/);
        assert.deepEqual(report.rows.map(row => row.id), spec.ids, "Fixture coverage mismatch");
        assert.equal(report.settings.repeats, 1);
        assert.equal(report.settings.idleMs, spec.untimed ? 1000 : 500);
        assert.equal(report.settings.playback, spec.playback !== false);
        assert.equal(report.settings.mode, spec.mode ?? "desktop");
        for (const flag of flags) assert.equal(report.settings[flag], flag === spec.flag, "Scenario flag mismatch: " + flag);
    }
    assert.equal(full.revision, fast.revision);
    assert.equal(full.harnessFingerprint, fast.harnessFingerprint);
    assert.deepEqual(full.environment, fast.environment);
    const { cdpDurationUnit, ...fullSettings } = full.settings;
    const { timingCollected, ...fastSettings } = fast.settings;
    assert.equal(cdpDurationUnit, "seconds");
    assert.equal(timingCollected, false);
    assert.deepEqual(fullSettings, fastSettings);
    const expected = expectedWindows(spec);
    for (const [i, a] of full.rows.entries()) {
        const b = fast.rows[i];
        assert.match(a.fingerprint, /^[a-f0-9]{64}$/);
        assert.equal(a.fingerprint, b.fingerprint);
        assert.equal(a.nodes, b.nodes);
        assert.equal(a.connections, b.connections);
        for (const row of [a, b]) for (const geometry of [row.initialGeometry, row.finalGeometry]) {
            assert.equal(geometry.renderedNodes, row.nodes);
            assert.equal(geometry.renderedConnections, row.connections);
        }
        assert.ok(!("measurements" in b) && !("importWallMs" in b) && !("initialPlayWallMs" in b));
        assert.deepEqual(a.measurements.map(windowKey), expected, "Full workload coverage mismatch");
        assert.deepEqual(b.checks.map(windowKey), expected, "Regression workload coverage mismatch");
        assert.deepEqual(b.checks.map(c => c.expansion), a.measurements.map(c => c.expansion), "Scope identity mismatch");
        for (const check of b.checks) {
            for (const field of ["cdp", "observedFps", "frameGapsMs", "longTasksMs", "elapsedMs"])
                assert.ok(!(field in check), "Timing leaked into regression check: " + field);
            if (spec.flag === "countRenders") {
                assert.ok(Number.isInteger(check.nodeRenders?.total) && Number.isInteger(check.nodeRenders?.affectedNodes));
            } else assert.equal(check.nodeRenders, null);
            if (check.workload === "parameter-edit") assert.ok(["pointer-hit-testable", "scripted-focus-in-viewport"].includes(check.parameterAccess));
        }
        for (const row of [a, b]) for (const field of lifecycleFields) {
            if (field === "foldedTelemetry" && spec.flag === "countRenders") {
                assert.equal(row[field]?.foldedNodes, 2);
                assert.equal(row[field]?.foldedFrameRenders, 0);
                assert.ok(row[field]?.expandedControlRenders > 0);
            } else if (field === spec.lifecycle?.[0]) {
                assert.deepEqual(row[field]?.steps?.map(step => step.name), spec.lifecycle[1], "Lifecycle coverage mismatch: " + field);
            } else assert.equal(row[field], null, "Unexpected lifecycle result: " + field);
        }
    }
    return expected.length * spec.ids.length;
}

export async function validateEditorRegressionSuite(directory) {
    const read = async name => JSON.parse(await readFile(join(directory, name), "utf8"));
    let windows = 0;
    for (const spec of coverage) {
        windows += validatePair(spec, await read(spec.report), await read("regression-" + spec.report));
    }
    assert.equal(windows, 82);
    return { reports: coverage.length, contexts: coverage.reduce((n, spec) => n + spec.ids.length, 0), windows };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    assert.equal(process.argv.length, 3, "Usage: validate-editor-regression-suite.mjs <fixture-directory>");
    console.log("EDITOR REGRESSION SUITE PARITY " + JSON.stringify(await validateEditorRegressionSuite(process.argv[2])));
}
