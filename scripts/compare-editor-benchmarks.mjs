import assert from "node:assert/strict";
import { readFile, writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";

const metrics = {
    fps: m => m.observedFps,
    frameGapP95Ms: m => m.frameGapsMs?.p95,
    scriptMs: m => milliseconds(m.cdp?.ScriptDuration),
    layoutMs: m => milliseconds(m.cdp?.LayoutDuration),
    styleMs: m => milliseconds(m.cdp?.RecalcStyleDuration),
    taskMs: m => milliseconds(m.cdp?.TaskDuration),
    heapDeltaBytes: m => m.cdp?.JSHeapUsedSize,
    longTasks: m => m.longTasksMs?.length,
    domElements: m => m.geometry?.domElements,
    frameRenders: m => m.nodeRenders?.total,
    affectedFrames: m => m.nodeRenders?.affectedNodes
};
const milliseconds = value => Number.isFinite(value) ? value * 1000 : null;
function median(values) {
    if (!values.length || !values.every(Number.isFinite)) return null;
    const sorted = [...values].sort((a, b) => a - b);
    const i = Math.floor(sorted.length / 2);
    return sorted.length % 2 ? sorted[i] : (sorted[i - 1] + sorted[i]) / 2;
}
function index(report) {
    const groups = new Map();
    const fixtures = new Map();
    for (const row of report.rows) {
        assert.ok(row.id && /^[a-f0-9]{64}$/.test(row.fingerprint), "Missing fixture fingerprint");
        assert.ok(!fixtures.has(row.id), "Duplicate fixture ID");
        fixtures.set(row.id, { fingerprint: row.fingerprint, nodes: row.nodes, connections: row.connections });
        for (const m of row.measurements) {
            const key = JSON.stringify([row.id, m.state, m.workload, m.expansion ?? null]);
            if (!groups.has(key)) groups.set(key, []);
            groups.get(key).push(m);
        }
    }
    assert.ok(groups.size, "No benchmark measurements");
    return { groups, fixtures };
}
export function compareEditorReports(before, after) {
    for (const r of [before, after]) {
        assert.equal(r.kind, "external-editor-benchmark");
        assert.equal(r.schemaVersion, 1);
        assert.ok(/^[a-f0-9]{40}$/.test(r.revision ?? ""), "Exact runtime revision SHA required");
        assert.ok(/^[a-f0-9]{64}$/.test(r.harnessFingerprint ?? ""), "Missing benchmark harness fingerprint");
        assert.ok(Number.isInteger(r.settings?.repeats) && r.settings.repeats > 0, "Missing repeat count");
        assert.ok(r.environment && r.settings, "Missing benchmark provenance");
    }
    assert.equal(before.fixtureVersion, after.fixtureVersion, "Fixture version mismatch");
    assert.equal(before.harnessFingerprint, after.harnessFingerprint, "Harness mismatch");
    assert.deepEqual(before.environment, after.environment, "Environment mismatch");
    assert.deepEqual(before.settings, after.settings, "Settings mismatch");
    const a = index(before), b = index(after);
    assert.deepEqual(a.fixtures, b.fixtures, "Fixture mismatch");
    assert.deepEqual([...a.groups.keys()].sort(), [...b.groups.keys()].sort(), "Workload/expansion mismatch");
    const rows = [...a.groups].map(([key, left]) => {
        const right = b.groups.get(key);
        assert.equal(left.length, before.settings.repeats, "Incomplete baseline repeats");
        assert.equal(right.length, after.settings.repeats, "Incomplete candidate repeats");
        const [fixture, state, workload, expansion] = JSON.parse(key);
        const changes = Object.fromEntries(Object.entries(metrics).map(([name, get]) => {
            const base = median(left.map(get)), candidate = median(right.map(get));
            const delta = base === null || candidate === null ? null : candidate - base;
            return [name, { before: base, after: candidate, delta,
                percentChange: delta === null || base === 0 ? null : delta / Math.abs(base) * 100 }];
        }));
        return { fixture, state, workload, expansion, repeats: left.length, changes };
    });
    return { schemaVersion: 1, kind: "paired-editor-benchmark", beforeRevision: before.revision,
        afterRevision: after.revision, harnessFingerprint: before.harnessFingerprint,
        environment: before.environment, settings: before.settings, fixtures: Object.fromEntries(a.fixtures),
        limitations: ["Median of repeat-level measurements; frameGapP95Ms is median of window p95 values.",
            "Matching metadata does not establish identical hardware or runner load; timings are diagnostic."], rows };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    const [before, after, output] = process.argv.slice(2);
    assert.ok(before && after && output, "Usage: node scripts/compare-editor-benchmarks.mjs before.json after.json comparison.json");
    const read = async path => JSON.parse(await readFile(path, "utf8"));
    await writeFile(output, JSON.stringify(compareEditorReports(await read(before), await read(after)), null, 2) + "\n");
}
