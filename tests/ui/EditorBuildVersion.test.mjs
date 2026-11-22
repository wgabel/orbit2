import test from "node:test";
import assert from "node:assert/strict";
import { mkdtemp, mkdir, readFile, writeFile, rm } from "node:fs/promises";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { runInNewContext } from "node:vm";
import { versionPublishedEditor } from "../../scripts/version-published-editor.mjs";

const index = await readFile(new URL("../../src/Orbit.UI/wwwroot/index.html", import.meta.url), "utf8");
const startup = index.match(/<script id="orbit-startup">([\s\S]*?)<\/script>/)[1];
const old = "a".repeat(40), latest = "b".repeat(40);

test("publish namespaces all editor styles and sibling modules and writes the matching manifest", async () => {
    const root = await mkdtemp(join(tmpdir(), "orbit-editor-version-"));
    try {
        await mkdir(join(root, "css/components"), { recursive: true });
        await mkdir(join(root, "js"), { recursive: true });
        await writeFile(join(root, "index.html"), index);
        for (const file of ["tokens.css", "base.css", "components.css"])
            await writeFile(join(root, "css", file), '@import url("./components/piano.css");');
        await writeFile(join(root, "css/components/piano.css"), ".piano {}");
        await writeFile(join(root, "js/scopeView.js"), 'import "./signalPlot.js";');
        await writeFile(join(root, "js/signalPlot.js"), "export const version = 1;");
        await assert.rejects(versionPublishedEditor(root, "../escape", "5"), /commit SHA/);
        await assert.rejects(versionPublishedEditor(root, latest, "0"), /build number/);
        await versionPublishedEditor(root, latest, "123");
        const published = await readFile(join(root, "index.html"), "utf8");
        assert.ok(published.includes(`name="orbit-build" content="${latest}"`));
        assert.ok(published.includes('name="orbit-build-number" content="123"'));
        for (const file of ["tokens.css", "base.css", "components.css"])
            assert.ok(published.includes(`href="css-${latest}/${file}"`));
        assert.equal(await readFile(join(root, `css-${latest}/components/piano.css`), "utf8"), ".piano {}");
        assert.equal(await readFile(join(root, `js-${latest}/signalPlot.js`), "utf8"), "export const version = 1;");
        assert.deepEqual(JSON.parse(await readFile(join(root, "build-manifest.json"), "utf8")), { commit: latest, build: "123" });
        await assert.rejects(versionPublishedEditor(root, old, "122"), /already versioned/);
    } finally { await rm(root, { recursive: true, force: true }); }
});

async function boot({ current = old, deployed = latest, href = "https://example.test/orbit2/?patch=demo#graph", fails = false } = {}) {
    let starts = 0, redirected, requested, options;
    const app = { textContent: "" };
    await runInNewContext(startup, {
        URL, Date, AbortController, setTimeout, clearTimeout,
        document: {
            baseURI: "https://example.test/orbit2/",
            querySelector: () => ({ content: current }),
            getElementById: () => app
        },
        location: { href, replace: url => { redirected = url; } },
        fetch: async (url, init) => {
            requested = url; options = init;
            if (fails) throw Error("offline");
            return { ok: true, json: async () => ({ commit: deployed }) };
        },
        Blazor: { start: async () => { starts++; } }
    });
    return { starts, redirected, requested, options, message: app.textContent };
}

test("cached shell revalidates without the HTTP cache and redirects once, preserving URL context", async () => {
    const state = await boot();
    assert.equal(state.starts, 0);
    assert.equal(state.requested.pathname, "/orbit2/build-manifest.json");
    assert.ok(state.requested.searchParams.has("check"));
    assert.equal(state.options.cache, "no-store");
    const target = new URL(state.redirected);
    assert.equal(target.searchParams.get("orbit-build"), latest);
    assert.ok(target.searchParams.has("orbit-refresh"));
    assert.equal(target.searchParams.get("patch"), "demo");
    assert.equal(target.hash, "#graph");
    const stillStale = await boot({ href: state.redirected });
    assert.equal(stillStale.redirected, undefined);
    assert.equal(stillStale.starts, 0);
    assert.match(stillStale.message, /newer Orbit build/);
    assert.equal((await boot({ current: latest, href: state.redirected })).starts, 1);
});

test("current, development, offline and malformed manifest start normally", async () => {
    for (const state of [
        await boot({ current: latest }), await boot({ current: "local" }),
        await boot({ fails: true }), await boot({ deployed: "bad" })
    ]) {
        assert.equal(state.starts, 1);
        assert.equal(state.redirected, undefined);
    }
    assert.equal((await boot({ current: "local" })).requested, undefined);
});
