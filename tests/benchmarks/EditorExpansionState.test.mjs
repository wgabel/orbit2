import test from "node:test";
import assert from "node:assert/strict";
import { planExpansionChanges, setBenchmarkExpansion } from "../../scripts/benchmark-expansion-state.mjs";

const ids = ["a", "b", "c", "d", "e"];
const snapshot = values => ids.map((id, i) => ({ id, expanded: String(values[i]) }));

test("expansion plan retains eligible order and changes only mismatched nodes", () => {
    const initial = snapshot([true, true, false, false, true]);
    assert.deepEqual(planExpansionChanges(ids, ["c", "a"], initial), [
        { id: "b", expanded: "false" }, { id: "c", expanded: "true" }, { id: "e", expanded: "false" }
    ]);
    assert.deepEqual(planExpansionChanges(ids, ["a", "b", "e"], initial), []);
});

test("expansion plan fails for missing, duplicate, foreign or malformed state", () => {
    const initial = snapshot([true, true, false, false, true]);
    for (const invalid of [initial.slice(1), [...initial, initial[0]],
        [...initial.slice(1), { id: "foreign", expanded: "true" }],
        [{ id: "a", expanded: null }, ...initial.slice(1)]])
        assert.throws(() => planExpansionChanges(ids, [], invalid));
    assert.throws(() => planExpansionChanges(ids, ["foreign"], initial));
    assert.throws(() => planExpansionChanges(ids, ["a", "a"], initial));
    assert.throws(() => planExpansionChanges([...ids, "a"], [], initial));
});

function fakeCanvas({ ignoreClicks = false, rejectWait = false } = {}) {
    const state = new Map(ids.map(id => [id, "true"]));
    const calls = [];
    let pending = null;
    const canvas = {
        locator(selector) {
            if (selector === "article.node-compactable[data-node-id]") return {
                evaluateAll: async fn => {
                    assert.equal(pending, null);
                    calls.push("snapshot");
                    return fn([...state].map(([id, expanded]) => ({
                        dataset: { nodeId: id },
                        querySelector: () => ({ getAttribute: () => expanded })
                    })));
                }
            };
            const id = selector.match(/^article\[data-node-id="([^"]+)"\]$/)?.[1];
            assert.ok(state.has(id));
            return { locator(inner) {
                if (inner === ".node-expand-toggle") return { evaluate: async fn => {
                    assert.equal(pending, null, "previous click must settle");
                    fn({ click: () => {
                        calls.push("click:" + id);
                        pending = id;
                        if (!ignoreClicks) state.set(id, state.get(id) === "true" ? "false" : "true");
                    } });
                } };
                const expected = inner.match(/aria-expanded="([^"]+)"/)?.[1];
                return { waitFor: async options => {
                    assert.equal(pending, id);
                    assert.deepEqual(options, { state: "attached", timeout: 123 });
                    calls.push("wait:" + id);
                    pending = null;
                    if (rejectWait) throw Error("toggle did not settle");
                    if (!ignoreClicks) assert.equal(state.get(id), expected);
                } };
            } };
        }
    };
    return { canvas, calls };
}

test("bulk transitions use two reads and sequential confirmed clicks, including no-op", async () => {
    const { canvas, calls } = fakeCanvas();
    assert.deepEqual(await setBenchmarkExpansion(canvas, ids, ["b", "d"], 123), { changed: 3, snapshotReads: 2 });
    assert.deepEqual(calls, ["snapshot", "click:a", "wait:a", "click:c", "wait:c", "click:e", "wait:e", "snapshot"]);
    calls.length = 0;
    assert.deepEqual(await setBenchmarkExpansion(canvas, ids, ["d", "b"], 123), { changed: 0, snapshotReads: 2 });
    assert.deepEqual(calls, ["snapshot", "snapshot"]);
});

test("bulk transitions fail on wrong final state or failed per-node confirmation", async () => {
    const wrong = fakeCanvas({ ignoreClicks: true });
    await assert.rejects(setBenchmarkExpansion(wrong.canvas, ids, [], 123), /exact requested state/);
    const failed = fakeCanvas({ rejectWait: true });
    await assert.rejects(setBenchmarkExpansion(failed.canvas, ids, [], 123), /toggle did not settle/);
    assert.deepEqual(failed.calls, ["snapshot", "click:a", "wait:a"]);
});
