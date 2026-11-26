import test from "node:test";
import assert from "node:assert/strict";
import { planEditorPairs } from "../../scripts/plan-editor-pairs.mjs";

test("historical mode shares the middle revision; smoke exercises the generic pair", () => {
    const history = planEditorPairs();
    assert.equal(history.historical, true);
    assert.equal(history.revisions.length, 3);
    assert.equal(history.pairs[0].after, history.pairs[1].before);
    const smoke = planEditorPairs({ smoke: true });
    assert.equal(smoke.historical, false);
    assert.deepEqual(smoke.revisions, history.revisions.slice(0, 2));
});
test("arbitrary exact revisions preserve order and reject incomplete or unsafe inputs", () => {
    const before = "a".repeat(40), after = "b".repeat(40);
    assert.deepEqual(planEditorPairs({ before, after }).pairs, [{ label: "comparison", before, after }]);
    for (const invalid of ["main", "abc123", "A".repeat(40), before + "\n", "$(echo unsafe)", ""]) {
        assert.throws(() => planEditorPairs({ before: invalid, after }));
    }
    assert.throws(() => planEditorPairs({ before, after: before }));
    assert.throws(() => planEditorPairs({ before, after, smoke: true }));
});
