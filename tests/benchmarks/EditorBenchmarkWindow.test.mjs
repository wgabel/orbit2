import test from "node:test";
import assert from "node:assert/strict";
import { executeEditorWindow } from "../../scripts/benchmark-editor-window.mjs";

function options({ state = "stopped", workload = "parameter-edit", after = { a: 2, b: 1, c: 1 }, timed = false } = {}) {
    const calls = [];
    let reads = 0;
    return { calls, args: {
        state, workload, expectedNodes: 3,
        action: async () => { calls.push("action"); return "pointer-hit-testable"; },
        settle: async () => { calls.push("settle"); },
        renderCounts: async () => { calls.push("counts"); return reads++ === 0 ? { a: 1, b: 1, c: 1 } : after; },
        beginTiming: timed ? async () => {
            calls.push("begin");
            return async () => { calls.push("finish"); return { sample: "test" }; };
        } : null
    } };
}

test("correctness-only and timed windows execute identical actions and counter checks", async () => {
    const plain = options();
    const timed = options({ timed: true });
    const a = await executeEditorWindow(plain.args);
    const b = await executeEditorWindow(timed.args);
    assert.deepEqual(plain.calls, ["counts", "action", "settle", "counts"]);
    assert.deepEqual(timed.calls, ["counts", "begin", "action", "settle", "finish", "counts"]);
    assert.deepEqual(a, { actionResult: "pointer-hit-testable", nodeRenders: { total: 1, affectedNodes: 1 }, timing: null });
    assert.deepEqual(b, { ...a, timing: { sample: "test" } });
});

test("both modes enforce every render-count invariant and reject malformed deltas", async () => {
    for (const timed of [false, true]) {
        for (const [state, workload, after, error] of [
            ["playing", "idle", { a: 2, b: 2, c: 2 }, /every node frame/],
            ["stopped", "parameter-edit", { a: 2, b: 2, c: 1 }, /changed frame/],
            ["stopped", "pan", { a: 2, b: 1, c: 1 }, /Pure canvas pan/],
            ["stopped", "node-drag", { a: 1, b: 1, c: 1 }, /Dragging/],
            ["stopped", "node-drag", { a: 2, b: 2, c: 2 }, /Dragging/],
            ["stopped", "wheel-zoom", { a: 0, b: 1, c: 1 }, /false/],
            ["stopped", "wheel-zoom", { a: 1, b: 1, foreign: 1 }, /false/]
        ]) {
            const { args } = options({ state, workload, after, timed });
            await assert.rejects(executeEditorWindow(args), error);
        }
    }
});

test("without counters the operation still runs; failures propagate and timing cleans up", async () => {
    const plain = options();
    plain.args.renderCounts = null;
    assert.deepEqual(await executeEditorWindow(plain.args), { actionResult: "pointer-hit-testable", nodeRenders: null, timing: null });
    assert.deepEqual(plain.calls, ["action", "settle"]);
    for (const timed of [false, true]) {
        const example = options({ timed });
        example.args.action = async () => { throw Error("action failed"); };
        await assert.rejects(executeEditorWindow(example.args), /action failed/);
        assert.deepEqual(example.calls, timed ? ["counts", "begin", "finish"] : ["counts"]);
    }
});
