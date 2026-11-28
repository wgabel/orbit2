import test from "node:test";
import assert from "node:assert/strict";
import { join } from "node:path";
import { editorSuitePlan, runEditorSuite } from "../../scripts/benchmark-editor-suite.mjs";

const fixture = { fixtureVersion: 1, fixtures: ["editor-50", "editor-200", "editor-500", "editor-composite-100"].map(id => ({ id })) };

test("CI suite retains the eight legacy reports, fixture profiles and all scenario flags", () => {
    const plan = editorSuitePlan(fixture);
    assert.deepEqual(plan.map(p => [p.report, p.profiles.map(f => f.id)]), [
        ["editor-report.json", ["editor-50", "editor-200"]],
        ["editor-500-stopped.json", ["editor-500"]],
        ["editor-mobile-stopped.json", ["editor-200"]],
        ["editor-render-counts.json", ["editor-50"]],
        ["editor-expansion-matrix.json", ["editor-50"]],
        ["editor-composite-100.json", ["editor-composite-100"]],
        ["editor-composite-telemetry.json", ["editor-composite-100"]],
        ["editor-folded-resources.json", ["editor-50"]]
    ]);
    const activeFlags = [
        [], [], [], ["ORBIT_EDITOR_RENDER_COUNTS"], ["ORBIT_EDITOR_EXPANSION_MATRIX"],
        ["ORBIT_EDITOR_COMPOSITE_RESOURCES"],
        ["ORBIT_EDITOR_COMPOSITE_TELEMETRY", "ORBIT_EDITOR_ASSERT_QUIET_ROOT"],
        ["ORBIT_EDITOR_FOLD_RESOURCES", "ORBIT_EDITOR_ASSERT_FOLDED_DEMAND"]
    ];
    for (const [i, scenario] of plan.entries()) {
        const s = scenario.settings;
        assert.equal(s.ORBIT_EDITOR_REPEATS, "1");
        assert.equal(s.ORBIT_EDITOR_IDLE_MS, i >= 6 ? "1000" : "500");
        assert.equal(s.ORBIT_EDITOR_VIEWPORT, i === 2 ? "mobile" : "desktop");
        assert.equal(s.ORBIT_EDITOR_PLAYBACK, i === 1 || i === 2 ? "0" : "1");
        assert.deepEqual(Object.keys(s).filter(k => /COUNTS|MATRIX|RESOURCES|TELEMETRY|ASSERT/.test(k) && s[k] === "1").sort(), activeFlags[i].slice().sort());
    }
    assert.throws(() => editorSuitePlan({ ...fixture, fixtures: fixture.fixtures.slice(0, 3) }), /Missing editor suite profile/);
});

test("shared suite runs sequentially with one browser, distinct settings and restored environment", async () => {
    const browser = {}, env = { ORBIT_EDITOR_PLAYBACK: "0", ORBIT_EDITOR_FOLD_RESOURCES: "1", ORBIT_REVISION: "revision" };
    const previous = { ...env };
    const calls = [];
    let running = false;
    await runEditorSuite({ browser, baseUrl: "http://localhost", directory: "fixtures", fixture, headless: true, env, log: () => {},
        run: async options => {
            assert.equal(running, false);
            running = true;
            assert.equal(options.browser, browser);
            assert.equal(options.baseUrl, "http://localhost");
            assert.equal(options.headless, true);
            assert.equal(env.ORBIT_REVISION, "revision");
            calls.push([options.outputFile, { ...env }]);
            await Promise.resolve();
            running = false;
        }
    });
    const plan = editorSuitePlan(fixture);
    assert.deepEqual(calls, plan.map(p => [join("fixtures", p.report), { ...previous, ...p.settings }]));
    assert.deepEqual(env, previous);
});

test("a failed scenario rejects the suite, stops subsequent work and restores flags", async () => {
    const env = { ORBIT_EDITOR_RENDER_COUNTS: "1" };
    let calls = 0;
    await assert.rejects(runEditorSuite({ directory: "fixtures", fixture, env, log: () => {},
        run: async () => { if (++calls === 2) throw new Error("benchmark failed"); }
    }), /benchmark failed/);
    assert.equal(calls, 2);
    assert.deepEqual(env, { ORBIT_EDITOR_RENDER_COUNTS: "1" });
});

test("regression suite uses the same eight profiles and settings with distinct reports", async () => {
    const full = editorSuitePlan(fixture);
    const fast = editorSuitePlan(fixture, { regression: true });
    assert.deepEqual(fast, full.map(s => ({ ...s, report: "regression-" + s.report })));
    const calls = [], logs = [], env = { ORBIT_EDITOR_PLAYBACK: "0" };
    await runEditorSuite({ directory: "fixtures", fixture, env, regression: true, log: message => logs.push(message),
        run: async options => {
            assert.equal(options.regression, true);
            calls.push([options.outputFile, options.profiles.map(p => p.id), { ...env }]);
        }
    });
    assert.deepEqual(calls, fast.map(s => [join("fixtures", s.report), s.profiles.map(p => p.id), s.settings]));
    assert.deepEqual(env, { ORBIT_EDITOR_PLAYBACK: "0" });
    assert.ok(logs.at(-1).startsWith("EDITOR REGRESSION SUITE TOTAL "));
});
