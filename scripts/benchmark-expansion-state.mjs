import assert from "node:assert/strict";

const eligibleSelector = "article.node-compactable[data-node-id]";

// Plan from a single DOM snapshot. Validate the complete eligible set rather
// than treating a missing frame or toggle as folded.
export function planExpansionChanges(eligibleIds, desiredIds, snapshot) {
    const eligible = new Set(eligibleIds);
    const desired = new Set(desiredIds);
    assert.equal(eligible.size, eligibleIds.length, "Duplicate eligible node ID");
    assert.equal(desired.size, desiredIds.length, "Duplicate desired node ID");
    assert.ok(desiredIds.every(id => eligible.has(id)), "Unknown desired node ID");
    const states = new Map();
    for (const { id, expanded } of snapshot) {
        assert.ok(eligible.has(id) && !states.has(id), "Unexpected or duplicate expansion frame: " + id);
        assert.ok(expanded === "true" || expanded === "false", "Missing or invalid expansion toggle: " + id);
        states.set(id, expanded);
    }
    assert.equal(states.size, eligible.size, "Missing eligible expansion frame");
    return eligibleIds.flatMap(id => {
        const expanded = String(desired.has(id));
        return states.get(id) === expanded ? [] : [{ id, expanded }];
    });
}

// Never batch clicks: each real Blazor toggle must settle before changing
// another node. Only the redundant per-node state reads are consolidated.
export async function setBenchmarkExpansion(canvas, eligibleIds, desiredIds, timeout = 60000) {
    const snapshot = () => canvas.locator(eligibleSelector).evaluateAll(nodes => nodes.map(node => ({
        id: node.dataset.nodeId,
        expanded: node.querySelector(".node-expand-toggle")?.getAttribute("aria-expanded") ?? null
    })));
    const changes = planExpansionChanges(eligibleIds, desiredIds, await snapshot());
    for (const { id, expanded } of changes) {
        const frame = canvas.locator(`article[data-node-id="${id}"]`);
        await frame.locator(".node-expand-toggle").evaluate(button => button.click());
        await frame.locator(`.node-expand-toggle[aria-expanded="${expanded}"]`)
            .waitFor({ state: "attached", timeout });
    }
    assert.deepEqual(planExpansionChanges(eligibleIds, desiredIds, await snapshot()), [],
        "Expansion transition did not reach the exact requested state");
    return { changed: changes.length, snapshotReads: 2 };
}
