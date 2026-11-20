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
    const transport = page.getByRole("region", { name: "Musical transport" });
    const tempo = transport.getByRole("spinbutton", { name: "Tempo (BPM)" });
    const beat = transport.getByTestId("transport-beat");
    const transportState = transport.getByTestId("transport-state");
    const applyTempo = transport.getByRole("button", { name: "Apply tempo" });
    assert.equal(await tempo.inputValue(), "120");
    await tempo.fill("0");
    await applyTempo.click();
    await transport.getByText("Enter a tempo from 20 to 300 BPM.").waitFor();
    await tempo.fill("60");
    await applyTempo.click();
    await transport.getByText("Synchronized rates: 1/4 note = 1 Hz · 1 bar (4/4) = 0.25 Hz").waitFor();
    assert.equal(await beat.innerText(), "0");
    assert.equal(await transport.getByTestId("transport-sample-rate").count(), 0);
    await play.click({ timeout: 30000 });
    await status.filter({ hasText: /Playing revision/ }).waitFor({ timeout: 30000 });
    let initialRevision = Number((await status.innerText()).match(/Playing revision (\d+)/)[1]);
    await transportState.filter({ hasText: "Running" }).waitFor();
    await transport.getByTestId("transport-sample-rate").waitFor();
    const firstBeat = Number(await beat.innerText());
    await page.waitForFunction(previous => Number(document.querySelector('[data-testid="transport-beat"]').textContent) > previous, firstBeat);
    await tempo.fill("90");
    await applyTempo.click();
    await transport.getByText("Synchronized rates: 1/4 note = 1.5 Hz · 1 bar (4/4) = 0.375 Hz").waitFor();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    assert.ok(Number(await beat.innerText()) >= firstBeat);
    await marker.waitFor({ timeout: 10000 });
    const firstPosition = await marker.getAttribute("style");
    let moved = false;
    for (let i = 0; i < 20 && !moved; i++) {
        await page.waitForTimeout(100);
        moved = (await marker.getAttribute("style")) !== firstPosition;
    }
    assert.ok(moved, "Live frequency marker did not move");
    // Select the existing modulation wire, edit depth and retain its identity.
    const wire = page.locator(".connection-modulation .connection-hit");
    await wire.scrollIntoViewIfNeeded();
    const position = await wire.evaluate(path => {
        const point = path.getPointAtLength(path.getTotalLength() / 2).matrixTransform(path.getScreenCTM());
        return { x: point.x, y: point.y };
    });
    await page.mouse.click(position.x, position.y);
    const amountEditor = page.getByRole("region", { name: "Connection amount editor" });
    const amount = amountEditor.getByRole("spinbutton", { name: "Modulation depth (Hz)" });
    await amount.waitFor();
    assert.equal(await amount.inputValue(), "100");
    await amount.fill("");
    await amountEditor.getByRole("button", { name: "Apply amount" }).click();
    await amountEditor.getByText("Enter a finite number.").waitFor();
    await amount.fill("25");
    await amountEditor.getByRole("button", { name: "Apply amount" }).click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await frequency.getByText("Modulation: 415–465 Hz").waitFor();
    await marker.waitFor({ timeout: 10000 });
    assert.equal(await wire.count(), 1);
    assert.equal(await play.isEnabled(), false);
    // Changing an existing control advances the audible revision without stopping.
    await page.getByRole("spinbutton", { name: "Frequency (Hz)" }).nth(1).fill("330");
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 1}$`) }).waitFor({ timeout: 10000 });
    assert.equal(await play.isEnabled(), false);
    // Unconnected nodes do not require a new worklet.
    await page.getByRole("button", { name: "Add", exact: true }).click();
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 2}$`) }).waitFor({ timeout: 10000 });
    // Remove the original LFO-to-frequency connection, then connect to amplitude.
    await wire.scrollIntoViewIfNeeded();
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
    await transportState.filter({ hasText: "Stopped" }).waitFor();
    const frozenBeat = await beat.innerText();
    await page.waitForTimeout(300);
    assert.equal(await beat.innerText(), frozenBeat);
    assert.equal(await amplitude.locator(".parameter-effective-marker").count(), 0);
    assert.equal(await amplitude.locator(".parameter-band").count(), 1);

    // Create an editable drone and connect its public sockets using the same
    // graph editor. Only the root has a master output.
    await page.locator("#node-type").selectOption("composite.graph");
    await page.getByRole("button", { name: "Add", exact: true }).click();
    const drone = page.locator(".node-role-composite");
    await drone.waitFor();
    await page.getByRole("button", { name: "Fit", exact: true }).click();
    const masterWire = page.locator(".connection-audio .connection-hit").nth(1);
    await masterWire.scrollIntoViewIfNeeded();
    const masterMidpoint = await masterWire.evaluate(path => {
        const point = path.getPointAtLength(path.getTotalLength() / 2).matrixTransform(path.getScreenCTM());
        return { x: point.x, y: point.y };
    });
    await page.mouse.click(masterMidpoint.x, masterMidpoint.y);
    await page.getByRole("button", { name: "Delete selected" }).click();
    await articles.nth(0).getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await drone.getByRole("button", { name: "Input: Pitch (Modulation)" }).press("Enter");
    await drone.getByRole("button", { name: "Output: Audio (Audio)" }).press("Enter");
    await articles.nth(3).getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    assert.equal(await page.getByRole("alert").count(), 0);
    const rootTransform = await page.locator(".canvas-world").getAttribute("style");
    await play.click();
    await status.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    const droneRevision = Number((await status.innerText()).match(/Playing revision (\d+)/)[1]);
    await drone.getByRole("button", { name: "Open Drone" }).click();
    const breadcrumbs = page.getByRole("navigation", { name: "Canvas path" });
    await breadcrumbs.getByText("Drone", { exact: true }).waitFor();
    assert.equal(await tempo.inputValue(), "90");
    await transportState.filter({ hasText: "Running" }).waitFor();
    assert.ok(Number(await beat.innerText()) >= Number(frozenBeat));
    assert.equal(await page.locator("#node-type option[value='output.master']").count(), 0);
    assert.equal(await articles.count(), 4);
    const rack = articles.filter({ hasText: "Input Parameters Rack" });
    await rack.getByRole("button", { name: "Output: Pitch (Modulation)" }).waitFor();
    const childFrequency = page.getByRole("spinbutton", { name: "Frequency (Hz)" });
    await page.getByText("Modulation: 439–441 Hz").waitFor({ timeout: 10000 });
    await childFrequency.locator("xpath=../..").locator(".parameter-effective-marker").waitFor({ timeout: 10000 });
    await childFrequency.fill("330");
    await status.filter({ hasText: new RegExp(`^Playing revision ${droneRevision + 1}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await play.isEnabled(), false);
    await page.getByRole("button", { name: "Zoom out" }).click();
    const childTransform = await page.locator(".canvas-world").getAttribute("style");
    await breadcrumbs.getByRole("button", { name: "Patch" }).click();
    await page.locator(".node-role-composite").getByRole("button", { name: "Open Drone" }).waitFor();
    assert.equal(await page.locator(".canvas-world").getAttribute("style"), rootTransform);
    await page.locator(".node-role-composite").getByRole("button", { name: "Open Drone" }).click();
    await childFrequency.waitFor();
    assert.equal(await page.locator(".canvas-world").getAttribute("style"), childTransform);
    assert.equal(await childFrequency.inputValue(), "330");
    assert.equal(await play.isEnabled(), false);

    // Boundary edits are one patch commit, including both mirrored sockets.
    await rack.getByRole("button", { name: "Edit ports" }).click();
    const editor = page.getByRole("region", { name: "Edit Input Parameters Rack" });
    await editor.getByLabel("New port name").fill("Cutoff");
    await editor.getByLabel("Receiving socket").selectOption("Sum");
    await editor.getByRole("button", { name: "Add port", exact: true }).click();
    await rack.getByRole("button", { name: "Output: Cutoff (Modulation)" }).waitFor();
    await editor.getByRole("textbox", { name: "Name for Pitch" }).fill("Sweep");
    await editor.getByRole("button", { name: "Rename Pitch" }).click();
    await rack.getByRole("button", { name: "Output: Sweep (Modulation)" }).waitFor();
    await editor.getByRole("button", { name: "Move Sweep down" }).click();
    await status.filter({ hasText: new RegExp(`^Playing revision ${droneRevision + 4}$`) }).waitFor({ timeout: 15000 });
    assert.deepEqual(await rack.locator(".port-output .port-label").allTextContents(), ["Gain", "Sweep", "Cutoff"]);
    await breadcrumbs.getByRole("button", { name: "Patch" }).click();
    await drone.getByRole("button", { name: "Input: Sweep (Modulation)" }).waitFor();
    assert.deepEqual(await drone.locator(".port-input .port-label").allTextContents(), ["Gain", "Sweep", "Cutoff"]);
    await drone.getByRole("button", { name: "Open Drone" }).click();
    await rack.getByRole("button", { name: "Edit ports" }).click();
    await editor.getByRole("button", { name: "Remove Sweep" }).click();
    await status.filter({ hasText: new RegExp(`^Playing revision ${droneRevision + 5}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await childFrequency.locator("xpath=../..").locator(".parameter-band").count(), 0);
    assert.equal(await rack.getByRole("button", { name: "Output: Sweep (Modulation)" }).count(), 0);
    assert.equal(await play.isEnabled(), false);
    await breadcrumbs.getByRole("button", { name: "Patch" }).click();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    assert.equal(await drone.getByRole("button", { name: "Input: Sweep (Modulation)" }).count(), 0);
    assert.equal(await page.getByRole("alert").count(), 0);
    await page.getByRole("button", { name: "Stop", exact: true }).click();
    await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 10000 });
    await transport.getByRole("button", { name: "Reset transport" }).click();
    await beat.filter({ hasText: /^0$/ }).waitFor();
    assert.equal(await tempo.inputValue(), "90");
    // The tempo LFO is an ordinary registered generator and a live control source.
    await page.locator("#node-type").selectOption("generator.tempo-sine");
    await page.getByRole("button", { name: "Add", exact: true }).click();
    const tempoLfo = articles.filter({ hasText: "Tempo sine LFO" });
    const division = tempoLfo.getByLabel("Musical division");
    await division.waitFor();
    assert.equal(await division.inputValue(), "2");
    await division.selectOption("3");
    await tempoLfo.getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await drone.getByRole("button", { name: "Input: Gain (Modulation)" }).press("Enter");
    await play.click();
    await status.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    await transportState.filter({ hasText: "Running" }).waitFor();
    await division.selectOption("4");
    assert.equal(await division.inputValue(), "4");
    await page.getByRole("button", { name: "Stop", exact: true }).click();
    await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 10000 });
    const prefix = `/_content/Orbit.Infrastructure/audio-${version}/`;
    for (const file of ["audio.js", "AudioEngine.js", "Faust/FaustCompilerWorker.js", "generated/compiler/index.js",
        "Components/TransportSignalProcessor.js"])
        assert.ok(requests.has(prefix + file), `Missing versioned asset: ${file}`);
    assert.ok([...requests].every(path => !path.startsWith("/_content/Orbit.Infrastructure/audio/")));
    console.log("Published editor played, navigated nested canvases, edited live frequency and mirrored rack port add/rename/reorder/remove during playback.");
} finally {
    await browser?.close();
    server.close();
}
