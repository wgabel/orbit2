import test from "node:test";
import assert from "node:assert/strict";
import { readFile, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

const workflow = await readFile(new URL("../../.github/workflows/build-and-deploy.yml", import.meta.url), "utf8");
const deployJob = workflow.slice(workflow.indexOf("\n  deploy:"));
const guard = deployJob.match(/- name: Check current main revision\n[\s\S]*?run: \|\n((?: {10}[^\n]*\n)+)/)?.[1];
assert.ok(guard, "deployment must check current main inside the serialized job");
const script = guard.replace(/^ {10}/gm, "");

test("Pages deploy and configuration are gated by the serialized current-main check", () => {
    assert.match(deployJob, /concurrency:\s+group: github-pages\s+cancel-in-progress: false/);
    assert.match(deployJob, /- uses: actions\/configure-pages@v5\s+if: steps\.current-main\.outputs\.deploy == 'true'/);
    assert.match(deployJob, /- name: Deploy to GitHub Pages\s+if: steps\.current-main\.outputs\.deploy == 'true'/);
});

test("current build deploys; stale build is skipped; failed head lookup fails closed", async () => {
    const directory = await mkdtemp(join(tmpdir(), "orbit-pages-guard-"));
    try {
        for (const [name, latest, failure, expected] of [
            ["current", "new", "0", "deploy=true\n"],
            ["stale", "new", "0", "deploy=false\n"],
            ["failed-lookup", "", "1", null]
        ]) {
            const output = join(directory, name);
            const result = spawnSync("bash", ["-e", "-c",
                'gh() { if [[ "$TEST_FAILURE" == "1" ]]; then return 1; fi; printf "%s\\n" "$TEST_LATEST"; }\n' + script], {
                env: { ...process.env, GITHUB_REPOSITORY: "wgabel/orbit2",
                    GITHUB_SHA: name === "current" ? "new" : "old",
                    GITHUB_OUTPUT: output, TEST_LATEST: latest, TEST_FAILURE: failure },
                encoding: "utf8"
            });
            if (expected === null) {
                assert.notEqual(result.status, 0);
                await assert.rejects(readFile(output), { code: "ENOENT" });
            } else {
                assert.equal(result.status, 0, result.stderr);
                assert.equal(await readFile(output, "utf8"), expected);
            }
        }
    } finally { await rm(directory, { recursive: true, force: true }); }
});
