// The CI editor suite shares the host and browser, not app state or measured contexts.
import assert from "node:assert/strict";
import { join } from "node:path";
import { runEditorBenchmark } from "./benchmark-editor.mjs";

const defaults = Object.freeze({
    ORBIT_EDITOR_REPEATS: "1",
    ORBIT_EDITOR_IDLE_MS: "500",
    ORBIT_EDITOR_VIEWPORT: "desktop",
    ORBIT_EDITOR_PLAYBACK: "1",
    ORBIT_EDITOR_RENDER_COUNTS: "0",
    ORBIT_EDITOR_EXPANSION_MATRIX: "0",
    ORBIT_EDITOR_COMPOSITE_RESOURCES: "0",
    ORBIT_EDITOR_COMPOSITE_TELEMETRY: "0",
    ORBIT_EDITOR_FOLD_RESOURCES: "0",
    ORBIT_EDITOR_ASSERT_QUIET_ROOT: "0",
    ORBIT_EDITOR_ASSERT_FOLDED_DEMAND: "0"
});

const scenarios = [
    ["editor-report.json", ["editor-50", "editor-200"], {}],
    ["editor-500-stopped.json", ["editor-500"], { ORBIT_EDITOR_PLAYBACK: "0" }],
    ["editor-mobile-stopped.json", ["editor-200"], { ORBIT_EDITOR_VIEWPORT: "mobile", ORBIT_EDITOR_PLAYBACK: "0" }],
    ["editor-render-counts.json", ["editor-50"], { ORBIT_EDITOR_RENDER_COUNTS: "1" }],
    ["editor-expansion-matrix.json", ["editor-50"], { ORBIT_EDITOR_EXPANSION_MATRIX: "1" }],
    ["editor-composite-100.json", ["editor-composite-100"], { ORBIT_EDITOR_COMPOSITE_RESOURCES: "1" }],
    ["editor-composite-telemetry.json", ["editor-composite-100"], {
        ORBIT_EDITOR_IDLE_MS: "1000", ORBIT_EDITOR_COMPOSITE_TELEMETRY: "1", ORBIT_EDITOR_ASSERT_QUIET_ROOT: "1" }],
    ["editor-folded-resources.json", ["editor-50"], {
        ORBIT_EDITOR_IDLE_MS: "1000", ORBIT_EDITOR_FOLD_RESOURCES: "1", ORBIT_EDITOR_ASSERT_FOLDED_DEMAND: "1" }]
];

export function editorSuitePlan(fixture) {
    assert.equal(fixture.fixtureVersion, 1);
    return scenarios.map(([report, ids, overrides]) => ({
        report,
        profiles: ids.map(id => {
            const profile = fixture.fixtures.find(f => f.id === id && !f.pairedMono);
            assert.ok(profile, "Missing editor suite profile: " + id);
            return profile;
        }),
        settings: { ...defaults, ...overrides }
    }));
}

// The legacy runner and diagnostic helpers consume environment flags. Scope all
// of those flags to each sequential scenario and restore even on failure. Never
// parallelize these calls in this process; each runner owns fresh browser contexts.
export async function runEditorSuite({ browser, baseUrl, directory, fixture, headless,
    run = runEditorBenchmark, env = process.env, log = console.log }) {
    const plan = editorSuitePlan(fixture);
    const previous = Object.fromEntries(Object.keys(defaults).map(key => [key, env[key]]));
    const suiteStarted = performance.now();
    try {
        for (const { report, profiles, settings } of plan) {
            Object.assign(env, settings);
            const started = performance.now();
            log("EDITOR SUITE START " + report);
            try {
                await run({ browser, baseUrl, directory, outputFile: join(directory, report), profiles, headless });
            } finally {
                log("EDITOR SUITE WALL " + report + " " + Math.round(performance.now() - started) + "ms");
            }
        }
    } finally {
        for (const [key, value] of Object.entries(previous)) {
            if (value === undefined) delete env[key];
            else env[key] = value;
        }
        log("EDITOR SUITE TOTAL " + Math.round(performance.now() - suiteStarted) + "ms");
    }
}
