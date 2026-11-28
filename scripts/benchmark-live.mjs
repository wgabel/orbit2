// Opt-in external Chromium measurement of the published Orbit app. No production profiling hooks.
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile, writeFile } from "node:fs/promises";
import { extname, join, resolve, sep } from "node:path";
import { pathToFileURL } from "node:url";
import { setTimeout as delay } from "node:timers/promises";
import { platform, release, arch, cpus } from "node:os";
import { summarize } from "./benchmark-metrics.mjs";

import { runEditorBenchmark } from "./benchmark-editor.mjs";
import { runEditorSuite } from "./benchmark-editor-suite.mjs";

const liveRegression = process.argv[2] === "--live-regression";
const regressionSuite = process.argv[2] === "--editor-regression-suite";
const regression = process.argv[2] === "--editor-regression" || regressionSuite;
const editorMode = process.argv[2] === "--editor" || process.argv[2] === "--editor-regression";
const editorSuiteMode = process.argv[2] === "--editor-suite" || regressionSuite;
const [directory, outputFile, ...requested] = process.argv.slice(editorMode || editorSuiteMode || liveRegression ? 3 : 2);
assert.ok(directory && (editorSuiteMode ? outputFile === undefined : outputFile) &&
    process.env.ORBIT_PLAYWRIGHT_DIR && process.env.ORBIT_CHROME,
    "Usage: benchmark-live.mjs [--editor|--editor-regression|--live-regression] <fixtures> <report.json> [profile IDs] or [--editor-suite|--editor-regression-suite] <fixtures> (requires ORBIT_PLAYWRIGHT_DIR and ORBIT_CHROME)");
const seconds = Number(process.env.ORBIT_BENCH_LIVE_SECONDS ?? 10);
assert.ok(Number.isInteger(seconds) && seconds >= 5 && seconds <= 120);
const fixture = JSON.parse(await readFile(join(directory, "fixtures.json"), "utf8"));
assert.equal(fixture.fixtureVersion, 1);
const ids = editorSuiteMode ? [] : requested.length ? requested : editorMode ? ["editor-50", "editor-200", "editor-500"] : ["sine-bank-1", "sine-bank-10"];
assert.ok(ids.length <= 8 && new Set(ids).size === ids.length);
const profiles = ids.map(id => {
    const profile = fixture.fixtures.find(f => f.id === id && !f.pairedMono);
    assert.ok(profile, "Unknown/auxiliary-only profile: " + id);
    return profile;
});
const { chromium } = await import(pathToFileURL(resolve(process.env.ORBIT_PLAYWRIGHT_DIR, "index.mjs")));
const root = resolve(process.env.ORBIT_BENCH_PUBLISH_DIR ?? "publish/wwwroot");
const types = { ".js": "text/javascript", ".mjs": "text/javascript", ".html": "text/html", ".css": "text/css", ".json": "application/json", ".wasm": "application/wasm", ".svg": "image/svg+xml" };
const server = createServer(async (request, response) => {
    try {
        const path = new URL(request.url, "http://localhost").pathname;
        const file = resolve(root, "." + (path === "/" ? "/index.html" : path));
        if (!file.startsWith(root + sep)) throw new Error("Outside published assets");
        const bytes = await readFile(file);
        response.writeHead(200, { "Content-Type": types[extname(file)] ?? "application/octet-stream" });
        response.end(bytes);
    } catch { response.writeHead(404); response.end(); }
});
await new Promise(done => server.listen(0, "127.0.0.1", done));
let browser;
try {
    const headless = process.env.ORBIT_BENCH_HEADFUL !== "1";
    browser = await chromium.launch({ executablePath: process.env.ORBIT_CHROME, headless, args: ["--mute-audio"] });
    if (editorSuiteMode) {
        await runEditorSuite({ browser, baseUrl: "http://127.0.0.1:" + server.address().port, directory, fixture, headless, regression });
    } else if (editorMode) {
        await runEditorBenchmark({ browser, baseUrl: "http://127.0.0.1:" + server.address().port, directory, outputFile, profiles, headless, regression });
    } else {
    const rows = [];
    for (const profile of profiles) {
        const context = await browser.newContext();
        try {
            const page = await context.newPage();
            const cdp = liveRegression ? null : await context.newCDPSession(page);
            const realtime = new Map();
            let cdpError = null;
            const remember = ({ context }) => {
                if (context.contextType === "realtime") realtime.set(context.contextId, context);
            };
            if (cdp) {
                cdp.on("WebAudio.contextCreated", remember);
                cdp.on("WebAudio.contextChanged", remember);
                cdp.on("WebAudio.contextWillBeDestroyed", ({ contextId }) => realtime.delete(contextId));
                try { await cdp.send("WebAudio.enable"); }
                catch (error) { cdpError = String(error); }
            }
            await page.goto("http://127.0.0.1:" + server.address().port);
            const status = page.locator(".playback-toolbar").getByRole("status");
            const play = page.getByRole("button", { name: "Play patch" });
            const more = page.getByRole("navigation", { name: "Workspace tools" }).getByRole("button", { name: "More", exact: true });
            const moreTools = page.getByRole("region", { name: "More tools" });
            async function openMore() {
                await more.click();
                await moreTools.waitFor({ state: "visible" });
            }
            async function closeMore() {
                await more.click();
                await moreTools.waitFor({ state: "hidden" });
            }
            await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
            await openMore();
            await moreTools.getByRole("button", { name: "Patches", exact: true }).click();
            const manager = page.getByRole("region", { name: "Patch manager" });
            await manager.getByLabel("Import JSON").setInputFiles(resolve(directory, profile.patch));
            await manager.getByText("Imported patch.", { exact: true }).waitFor();
            await manager.getByRole("button", { name: "Close" }).click();
            await closeMore();
            await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
            const panel = page.getByRole("region", { name: "Audio performance diagnostics" });
            const toggle = moreTools.getByRole("button", { name: "Performance · Off", exact: true });
            async function readMonitor(state, voices, expectedRevision = null) {
                await panel.locator("[data-performance-session]").filter({ hasText: new RegExp("^" + state + "$") }).waitFor({ timeout: 10000 });
                if (voices) await panel.locator("[data-performance-worklets]").filter({ hasText: voices }).waitFor({ timeout: 10000 });
                if (expectedRevision !== null) await page.waitForFunction(revision =>
                    [...document.querySelectorAll("#audio-performance-details dt")].some(dt =>
                        dt.textContent === "Requested / applied revision" &&
                        dt.nextElementSibling.textContent.trim() === revision + " / " + revision), expectedRevision);
                return await panel.locator("dl").evaluateAll(lists => Object.fromEntries(
                    lists.flatMap(list => [...list.querySelectorAll("dt")].map(dt => [dt.textContent, dt.nextElementSibling.textContent]))));
            }
            async function openMonitor(state, voices, expectedRevision = null) {
                await openMore();
                await toggle.click();
                return readMonitor(state, voices, expectedRevision);
            }
            async function closeMonitor() {
                await panel.getByRole("button", { name: "Close" }).click();
                await toggle.waitFor();
                await closeMore();
            }
            // Operation diagnostics must be enabled before the operation, not
            // opened afterward. Read the idle snapshot to ensure collection started.
            await openMonitor("idle", "0 / 0");
            const before = performance.now();
            await play.click();
            await status.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 60000 });
            const initialPlayWallMs = performance.now() - before;
            let revision = Number((await status.innerText()).match(/\d+$/)[0]);
            const initial = await readMonitor("active", "1 / 0", revision);
            assert.equal(initial["Faust source builds"], "1");
            // External steady-state capacity should exclude monitor overhead.
            await closeMonitor();
            await delay(1000);
            const observations = [];
            const samplingStart = performance.now();
            for (let i = 0; !liveRegression && i < seconds; i++) {
                await delay(1000);
                if (cdpError) continue;
                const audio = [...realtime.values()].find(c => c.contextState === "running");
                if (!audio) continue;
                try {
                    const { realtimeData: data } = await cdp.send("WebAudio.getRealtimeData", { contextId: audio.contextId });
                    if (Number.isFinite(data.currentTime) && Number.isFinite(data.renderCapacity) && data.renderCapacity >= 0)
                        observations.push({ elapsedMs: performance.now() - samplingStart,
                            contextTimeSeconds: data.currentTime, renderCapacityPercent: data.renderCapacity });
                } catch (error) { cdpError = String(error); }
            }
            const audio = [...realtime.values()].find(c => c.contextState === "running");
            const measured = observations.length >= 2 && observations.at(-1).contextTimeSeconds > observations[0].contextTimeSeconds;
            // A new monitoring session has no history of the earlier Play.
            const steady = await openMonitor("active", "1 / 0", revision);
            assert.equal(steady["Faust source builds"], "0");
            assert.equal(steady["Voice preparation attempts"], "0");
            let edits = null;
            if (profile.id === "sine-bank-1") {
                const frequency = page.getByRole("spinbutton", { name: "Frequency (Hz)" });
                const waveform = page.getByLabel("Waveform", { exact: true });
                let started = performance.now();
                await frequency.fill("220");
                await status.filter({ hasText: new RegExp(`^Playing revision ${++revision}$`) }).waitFor({ timeout: 15000 });
                const liveControlWallMs = performance.now() - started;
                const control = await readMonitor("active", "1 / 0", revision);
                assert.equal(control["Faust source builds"], "0");
                assert.equal(control["Live control updates"], "1");
                started = performance.now();
                await waveform.selectOption("2");
                await status.filter({ hasText: new RegExp(`^Playing revision ${++revision}$`) }).waitFor({ timeout: 30000 });
                const replacementWallMs = performance.now() - started;
                const replacement = await readMonitor("active", "1 / 0", revision);
                assert.equal(replacement["Faust source builds"], "1");
                assert.equal(replacement["Accepted replacements"], "1");
                edits = { ...(liveRegression ? {} : { liveControlWallMs, replacementWallMs }), control, replacement };
            }
            await page.getByRole("button", { name: "Stop", exact: true }).click();
            await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
            const stopped = await readMonitor("stopped", "0 / 0");
            const secondPlayStart = performance.now();
            await play.click();
            await status.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 60000 });
            const replayWallMs = performance.now() - secondPlayStart;
            const replay = await readMonitor("active", "1 / 0", revision);
            assert.equal(replay["Faust source builds"], "1");
            await page.getByRole("button", { name: "Stop", exact: true }).click();
            await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
            await readMonitor("stopped", "0 / 0");
            await closeMonitor();
            rows.push({ id: profile.id, ...(liveRegression ? {} : { initialPlayWallMs, replayWallMs,
                sampleRateHz: audio?.sampleRate ?? null, callbackBufferFrames: audio?.callbackBufferSize ?? null,
                renderQuantumFrames: audio?.renderQuantumSize ?? null, maxOutputChannels: audio?.maxOutputChannelCount ?? null,
                renderCapacityPercent: measured ? summarize(observations.map(o => o.renderCapacityPercent)) : null,
                observations, availability: measured ? "observed" : cdpError ?? "No advancing real-time context samples" }),
                initial, steady, edits, stopped, replay });
            console.log(liveRegression ? `${profile.id}: playback lifecycle verified (no renderer sampling)` :
                `${profile.id}: ${measured ? rows.at(-1).renderCapacityPercent.median.toFixed(2) + "% median renderer capacity" : "renderer capacity unavailable"} (external Chromium, muted ${headless ? "headless" : "headed"})`);
        } finally { await context.close(); }
    }
    await writeFile(outputFile, JSON.stringify({ schemaVersion: 1, fixtureVersion: fixture.fixtureVersion,
        kind: liveRegression ? "external-live-renderer-regression" : "external-live-renderer-benchmark",
        scope: liveRegression ? "Live playback lifecycle correctness; no renderer sampling" : "Chrome DevTools Protocol WebAudio renderer capacity, not Faust-only load",
        revision: process.env.ORBIT_REVISION ?? process.env.GITHUB_SHA ?? null, timestampUtc: new Date().toISOString(),
        environment: { chrome: browser.version(), node: process.version, headless, muted: true,
            platform: platform(), release: release(), arch: arch(), cpuModel: cpus()[0]?.model ?? null, logicalCpuCount: cpus().length },
        settings: { ...(liveRegression ? { timingCollected: false } : { seconds, sampleCadenceMs: 1000 }),
            plotViews: 0, monitorOpenDuringRendererSampling: false }, rows }, null, 2) + "\n");
    }
} finally {
    await browser?.close();
    await new Promise(done => server.close(done));
}
