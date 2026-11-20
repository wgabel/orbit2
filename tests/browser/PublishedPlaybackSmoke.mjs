// Exercise the published Blazor app, not just its JavaScript components.
// Reject the unversioned audio path to model a browser holding stale assets.
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, resolve, sep } from "node:path";
import { pathToFileURL } from "node:url";

const { chromium } = await import(pathToFileURL(resolve(process.env.ORBIT_PLAYWRIGHT_DIR, "index.mjs")));
const root = resolve("publish/wwwroot");
const version = process.env.GITHUB_SHA;
assert.ok(version && process.env.ORBIT_CHROME);
const requests = new Set();
const types = { ".js": "text/javascript", ".mjs": "text/javascript", ".html": "text/html", ".css": "text/css", ".json": "application/json", ".wasm": "application/wasm", ".svg": "image/svg+xml" };
const server = createServer(async (request, response) => {
    const path = new URL(request.url, "http://localhost").pathname;
    requests.add(path);
    try {
        if (path.startsWith("/_content/Orbit.Infrastructure/audio/")) throw new Error("Unversioned audio assets are stale");
        const file = resolve(root, "." + (path === "/" ? "/index.html" : path));
        if (!file.startsWith(root + sep)) throw new Error("Outside static assets");
        const bytes = await readFile(file);
        response.writeHead(200, { "Content-Type": types[extname(file)] ?? "application/octet-stream" });
        response.end(bytes);
    } catch (error) { response.writeHead(404); response.end(String(error)); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
let browser;
try {
    browser = await chromium.launch({ executablePath: process.env.ORBIT_CHROME, headless: true,
        args: ["--no-sandbox", "--mute-audio"] });
    const page = await browser.newPage();
    page.on("pageerror", error => console.error(error.message));
    await page.goto("http://127.0.0.1:" + server.address().port);
    const status = page.getByRole("status");
    const play = page.getByRole("button", { name: "Play patch" });
    const articles = page.locator("article");
    const frequency = articles.nth(1).locator(".parameter-slider").nth(0);
    const amplitude = articles.nth(1).locator(".parameter-slider").nth(1);
    const marker = frequency.locator(".parameter-effective-marker");
    await frequency.getByText("Modulation: 340–540 Hz").waitFor({ timeout: 30000 });
    assert.equal(await marker.count(), 0);
    await play.click({ timeout: 30000 });
    await status.filter({ hasText: /Playing revision/ }).waitFor({ timeout: 30000 });
    const initialRevision = Number((await status.innerText()).match(/Playing revision (\d+)/)[1]);
    await marker.waitFor({ timeout: 10000 });
    const firstPosition = await marker.getAttribute("style");
    let moved = false;
    for (let i = 0; i < 20 && !moved; i++) {
        await page.waitForTimeout(100);
        moved = (await marker.getAttribute("style")) !== firstPosition;
    }
    assert.ok(moved, "Live frequency marker did not move");
    // Changing an existing control advances the audible revision without stopping.
    await page.getByRole("spinbutton", { name: "Frequency (Hz)" }).nth(1).fill("330");
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 1}$`) }).waitFor({ timeout: 10000 });
    assert.equal(await play.isEnabled(), false);
    // Unconnected nodes do not require a new worklet.
    await page.getByRole("button", { name: "Add", exact: true }).click();
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 2}$`) }).waitFor({ timeout: 10000 });
    // Remove the original LFO-to-frequency connection, then connect to amplitude.
    const wire = page.locator(".connection-modulation .connection-hit");
    const midpoint = await wire.evaluate(path => {
        const point = path.getPointAtLength(path.getTotalLength() / 2).matrixTransform(path.getScreenCTM());
        return { x: point.x, y: point.y };
    });
    await page.mouse.click(midpoint.x, midpoint.y);
    await page.getByRole("button", { name: "Delete selected" }).click();
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 3}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await marker.count(), 0);
    assert.equal(await frequency.locator(".parameter-band").count(), 0);
    await articles.nth(0).getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await articles.nth(1).getByRole("button", { name: "Input: Amplitude (Modulation)" }).press("Enter");
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 4}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await play.isEnabled(), false);
    await amplitude.locator(".parameter-effective-marker").waitFor({ timeout: 10000 });
    await page.getByRole("button", { name: "Stop", exact: true }).click();
    await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 10000 });
    assert.equal(await amplitude.locator(".parameter-effective-marker").count(), 0);
    assert.equal(await amplitude.locator(".parameter-band").count(), 1);
    const prefix = `/_content/Orbit.Infrastructure/audio-${version}/`;
    for (const file of ["audio.js", "AudioEngine.js", "Faust/FaustCompilerWorker.js", "generated/compiler/index.js"])
        assert.ok(requests.has(prefix + file), `Missing versioned asset: ${file}`);
    assert.ok([...requests].every(path => !path.startsWith("/_content/Orbit.Infrastructure/audio/")));
    console.log("Published editor played, animated real modulation, applied live parameter edits, rewired frequency to amplitude and stopped.");
} finally {
    await browser?.close();
    server.close();
}
