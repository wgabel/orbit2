import test from "node:test";
import assert from "node:assert/strict";
import { readFile, mkdtemp, rm, mkdir, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync, spawnSync } from "node:child_process";

const workflow = await readFile(new URL("../../.github/workflows/build-and-deploy.yml", import.meta.url), "utf8");
const deployJob = workflow.slice(workflow.indexOf("\n  deploy:"));
const guard = deployJob.match(/- name: Check current main runtime inputs\n[\s\S]*?run: \|\n((?: {10}[^\n]*\n)+)/)?.[1];
assert.ok(guard, "deployment must check current main inside the serialized job");
const script = guard.replace(/^ {10}/gm, "");
const classifier = fileURLToPath(new URL("../../scripts/ci-change-scope.mjs", import.meta.url));

test("Pages deploy and configuration are gated by the serialized current-main check", () => {
    assert.match(deployJob, /concurrency:\s+group: github-pages\s+cancel-in-progress: false/);
    assert.match(deployJob, /if: needs\.build\.outputs\.runtime == 'true'/);
    assert.match(deployJob, /- uses: actions\/configure-pages@v5\s+if: steps\.current-main\.outputs\.deploy == 'true'/);
    assert.match(deployJob, /- name: Deploy to GitHub Pages\s+if: steps\.current-main\.outputs\.deploy == 'true'/);
});

test("actual workflow guard permits documentation advances, rejects stale runtime and fails closed on fetch failure", async () => {
    const directory = await mkdtemp(join(tmpdir(), "orbit-pages-guard-"));
    try {
        const remote = join(directory, "remote.git");
        const source = join(directory, "source");
        const checkout = join(directory, "checkout");
        const git = (cwd, ...args) => execFileSync("git", args, {
            cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"]
        }).trim();
        git(directory, "init", "--bare", "--initial-branch=main", remote);
        git(directory, "clone", remote, source);
        git(source, "config", "user.name", "CI fixture");
        git(source, "config", "user.email", "test@example.invalid");
        await mkdir(join(source, "src"));
        await mkdir(join(source, "docs"));
        await mkdir(join(source, "scripts"));
        await writeFile(join(source, "scripts", "ci-change-scope.mjs"), await readFile(classifier));
        await writeFile(join(source, "src", "editor.cs"), "built");
        await writeFile(join(source, "docs", "guide.md"), "initial");
        git(source, "add", ".");
        git(source, "commit", "-m", "Built runtime");
        git(source, "push", "origin", "main");
        const built = git(source, "rev-parse", "HEAD");
        git(directory, "clone", remote, checkout);
        function check(name, expected) {
            const output = join(directory, name);
            const result = spawnSync("bash", ["-e", "-c", script], {
                cwd: checkout,
                env: { ...process.env, GITHUB_SHA: built, GITHUB_OUTPUT: output },
                encoding: "utf8"
            });
            if (expected === null) {
                assert.notEqual(result.status, 0);
                return assert.rejects(readFile(output), { code: "ENOENT" });
            }
            assert.equal(result.status, 0, result.stderr);
            return readFile(output, "utf8").then(value => assert.equal(value, expected));
        }
        await check("current", "deploy=true\n");
        await writeFile(join(source, "docs", "guide.md"), "new documentation");
        git(source, "commit", "-am", "Documentation only");
        git(source, "push", "origin", "main");
        await check("documentation-advance", "deploy=true\n");
        await writeFile(join(source, "src", "editor.cs"), "new runtime");
        git(source, "commit", "-am", "Runtime change");
        git(source, "push", "origin", "main");
        await check("stale", "deploy=false\n");
        git(checkout, "remote", "set-url", "origin", join(directory, "missing.git"));
        await check("failed-fetch", null);
    } finally {
        await rm(directory, { recursive: true, force: true });
    }
});
