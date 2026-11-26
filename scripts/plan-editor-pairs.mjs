import assert from "node:assert/strict";
import { writeFile, mkdir } from "node:fs/promises";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const A = "a21198712750a9c4679ba4e451c636c68211091b";
const B = "0356ffe183b43f6654c21266f4816fdf2c35a1b1";
const C = "de2540b95388dc620271d348cb8d9525d74ba38a";

// Keep automatic verification focused on the latest measured runtime change.
export const verificationPair = Object.freeze({ before: "37bcb3931430d6a80695a3348ec77a5c4c401cf8", after: "9c1fc46e657960b62605d901a360f5fa4c698c9c" });

export function planEditorPairs({ before = "", after = "", smoke = false } = {}) {
    if (smoke) {
        assert.ok(!before && !after, "Smoke uses fixed trusted revisions");
        ({ before, after } = verificationPair);
    }
    if (!before && !after) return {
        historical: true, revisions: [A, B, C],
        pairs: [{ label: "pr-135", before: A, after: B }, { label: "pr-137", before: B, after: C }]
    };
    assert.ok(before.length === 40 && after.length === 40 && /^[a-f0-9]{40}$/.test(before) && /^[a-f0-9]{40}$/.test(after),
        "Provide both exact lowercase 40-character commit SHAs, or leave both blank for historical mode");
    assert.notEqual(before, after, "Baseline and candidate must be different commits");
    return { historical: false, revisions: [before, after], pairs: [{ label: "comparison", before, after }] };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    const directory = process.argv[2];
    assert.ok(directory, "Usage: node scripts/plan-editor-pairs.mjs output-directory");
    const plan = planEditorPairs({ before: process.env.PAIR_BEFORE ?? "", after: process.env.PAIR_AFTER ?? "",
        smoke: process.env.PAIR_SMOKE === "true" });
    await mkdir(directory, { recursive: true });
    await writeFile(join(directory, "plan.json"), JSON.stringify(plan, null, 2) + "\n");
    await writeFile(join(directory, "revisions.txt"), plan.revisions.join("\n") + "\n");
    await writeFile(join(directory, "pairs.tsv"), plan.pairs.map(p => [p.label, p.before, p.after].join("\t")).join("\n") + "\n");
    await writeFile(join(directory, "mode.txt"), plan.historical ? "historical\n" : "pair\n");
}
