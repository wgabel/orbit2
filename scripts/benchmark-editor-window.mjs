import assert from "node:assert/strict";

// The same action, settling and deterministic counter checks run with or
// without the optional external timing collector. Never skip an action because
// a timing collector is absent.
export async function executeEditorWindow({ state, workload, action, settle,
    expectedNodes, renderCounts = null, beginTiming = null }) {
    const beforeRenders = renderCounts ? await renderCounts() : null;
    const finishTiming = beginTiming ? await beginTiming() : null;
    let actionResult, timing = null;
    try {
        actionResult = await action();
        await settle();
    } finally {
        if (finishTiming) timing = await finishTiming();
    }
    let nodeRenders = null;
    if (beforeRenders) {
        const afterRenders = await renderCounts();
        assert.equal(Object.keys(afterRenders).length, expectedNodes);
        assert.equal(Object.keys(beforeRenders).length, expectedNodes);
        const changes = Object.entries(afterRenders).map(([id, count]) => count - beforeRenders[id]);
        assert.ok(changes.every(value => Number.isInteger(value) && value >= 0), "Invalid node render delta");
        nodeRenders = { total: changes.reduce((a, b) => a + b, 0), affectedNodes: changes.filter(n => n > 0).length };
        if (state === "playing" && workload === "idle")
            assert.ok(nodeRenders.affectedNodes < expectedNodes,
                "Playback telemetry must not invalidate every node frame");
        if (state === "stopped" && workload === "parameter-edit")
            assert.equal(nodeRenders.affectedNodes, 1, "Carrier frequency edit must only rerender the changed frame");
        if (state === "stopped" && workload === "pan")
            assert.equal(nodeRenders.total, 0, "Pure canvas pan must not rerender node frames");
        if (state === "stopped" && workload === "node-drag")
            assert.ok(nodeRenders.affectedNodes > 0 && nodeRenders.affectedNodes <= 2,
                "Dragging must rerender only moved/previously selected frames");
    }
    return { actionResult, nodeRenders, timing };
}
