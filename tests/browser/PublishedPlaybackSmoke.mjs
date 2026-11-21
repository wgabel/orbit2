// Exercise the published Blazor app, not just its JavaScript components.
// Reject unversioned interop paths to model a browser holding stale assets.
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
        if (["audio", "persistence"].some(directory => path.startsWith(`/_content/Orbit.Infrastructure/${directory}/`)))
            throw new Error("Unversioned browser assets are stale");
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
    const frequency = articles.nth(1).locator(".parameter-slider").filter({ has: page.getByRole("spinbutton", { name: "Frequency (Hz)" }) });
    const amplitude = articles.nth(1).locator(".parameter-slider").filter({ has: page.getByRole("spinbutton", { name: "Amplitude (normalized)" }) });
    const marker = frequency.locator(".parameter-effective-marker");
    await frequency.getByText("Modulation: 340–540 Hz").waitFor({ timeout: 30000 });
    // Primary controls share one compact toolbar; the canvas consumes the
    // remaining screen height rather than using a fixed viewport fraction.
    const toolbar = page.locator(".canvas-caption");
    await toolbar.getByRole("button", { name: "Zoom out" }).waitFor();
    await toolbar.getByRole("button", { name: "Add", exact: true }).waitFor();
    await toolbar.getByRole("button", { name: "Play patch" }).waitFor();
    const viewport = page.locator(".canvas-viewport");
    await viewport.getByText("4 nodes · 3 connections", { exact: true }).waitFor();
    const canvasBounds = await viewport.boundingBox();
    assert.ok(canvasBounds.height >= page.viewportSize().height * 0.75, "Canvas must fill most of the viewport");
    const countBounds = await viewport.locator(".canvas-count").boundingBox();
    assert.ok(countBounds.x > canvasBounds.x + canvasBounds.width / 2 && countBounds.y < canvasBounds.y + 40,
        "Node and connection count must remain in the upper right of the canvas");
    assert.equal(await marker.count(), 0);
    // A real range drag commits multiple input events but exactly one undo step.
    const range = amplitude.getByRole("slider");
    const amplitudeInput = amplitude.getByRole("spinbutton");
    const originalAmplitude = await amplitudeInput.inputValue();
    await range.scrollIntoViewIfNeeded();
    await range.evaluate(element => {
        element.dataset.gestureInputs = "0";
        element.addEventListener("input", () => element.dataset.gestureInputs = String(Number(element.dataset.gestureInputs) + 1));
    });
    const rangeBounds = await range.boundingBox();
    const rangeY = rangeBounds.y + rangeBounds.height / 2;
    await page.mouse.move(rangeBounds.x + rangeBounds.width * 0.35, rangeY);
    await page.mouse.down();
    await page.mouse.move(rangeBounds.x + rangeBounds.width * 0.8, rangeY, { steps: 12 });
    await page.mouse.up();
    assert.ok(Number(await range.getAttribute("data-gesture-inputs")) > 2, "Drag must produce multiple live values");
    const changedAmplitude = await amplitudeInput.inputValue();
    assert.notEqual(changedAmplitude, originalAmplitude);
    const undoButton = page.getByRole("button", { name: "Undo", exact: true });
    const redoButton = page.getByRole("button", { name: "Redo", exact: true });
    await undoButton.click();
    assert.equal(await amplitudeInput.inputValue(), originalAmplitude);
    await redoButton.click();
    assert.equal(await amplitudeInput.inputValue(), changedAmplitude);
    await undoButton.click();
    assert.equal(await amplitudeInput.inputValue(), originalAmplitude);
    assert.equal(await undoButton.isEnabled(), false);
    // The shared catalog selector applies an entire configuration in one undo step.
    const rootPreset = articles.nth(1).getByRole("combobox", { name: "Preset for Oscillator" });
    assert.equal(await rootPreset.inputValue(), "");
    assert.ok(await rootPreset.locator("optgroup[label='Drones'] option").count() >= 3);
    assert.equal(await rootPreset.locator("optgroup[label='Bass'] option[value='oscillator-sub-bass']").count(), 1);
    assert.equal(await rootPreset.locator("optgroup[label='Rhythmic modulation'] option[value='oscillator-bar-swell']").count(), 1);
    assert.equal(await rootPreset.locator("optgroup[label='Custom']").evaluate(group => group.disabled), true);
    assert.equal(await articles.last().locator(".node-preset-selector").count(), 0);
    const rootPresetPosition = await articles.nth(1).getAttribute("style");
    await rootPreset.selectOption("oscillator-deep-sine");
    assert.equal(await rootPreset.inputValue(), "oscillator-deep-sine");
    assert.equal(await frequency.getByRole("spinbutton").inputValue(), "110");
    assert.equal(await amplitudeInput.inputValue(), "0.2");
    assert.notEqual(await articles.nth(1).locator(".node-preset-description").innerText(), "No matching preset. Choose one to load.");
    assert.equal(await articles.nth(1).getAttribute("style"), rootPresetPosition);
    await amplitudeInput.fill("0.3");
    assert.equal(await rootPreset.inputValue(), "");
    await undoButton.click();
    assert.equal(await rootPreset.inputValue(), "oscillator-deep-sine");
    await undoButton.click();
    assert.equal(await rootPreset.inputValue(), "");
    assert.equal(await frequency.getByRole("spinbutton").inputValue(), "440");
    assert.equal(await amplitudeInput.inputValue(), originalAmplitude);
    assert.equal(await undoButton.isEnabled(), false);
    const muteSource = articles.nth(0).getByRole("button", { name: /^Mute / });
    const beforeMuteTransform = await articles.nth(0).getAttribute("style");
    await muteSource.click();
    assert.equal(await muteSource.getAttribute("aria-pressed"), "true");
    assert.equal(await articles.nth(0).getAttribute("style"), beforeMuteTransform);
    await frequency.getByText("Modulation: 440–440 Hz").waitFor();
    await undoButton.click();
    assert.equal(await muteSource.getAttribute("aria-pressed"), "false");
    await frequency.getByText("Modulation: 340–540 Hz").waitFor();
    assert.equal(await articles.last().getByRole("button", { name: /^Mute / }).count(), 0);
    const bypassGain = articles.nth(2).getByRole("button", { name: "Bypass Gain" });
    const muteGain = articles.nth(2).getByRole("button", { name: "Mute Gain" });
    const gainPosition = await articles.nth(2).getAttribute("style");
    await bypassGain.click();
    assert.equal(await bypassGain.getAttribute("aria-pressed"), "true");
    assert.equal(await articles.nth(2).getAttribute("style"), gainPosition);
    await muteGain.click();
    assert.equal(await muteGain.getAttribute("aria-pressed"), "true");
    assert.equal(await bypassGain.getAttribute("aria-pressed"), "true");
    await undoButton.click();
    assert.equal(await muteGain.getAttribute("aria-pressed"), "false");
    assert.equal(await bypassGain.getAttribute("aria-pressed"), "true");
    await undoButton.click();
    assert.equal(await bypassGain.getAttribute("aria-pressed"), "false");
    assert.equal(await articles.nth(0).getByRole("button", { name: /^Bypass / }).count(), 0);
    assert.equal(await articles.last().getByRole("button", { name: /^Bypass / }).count(), 0);
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
    // History is a fresh committed revision even while the worklet is running.
    await page.getByRole("button", { name: "Undo", exact: true }).click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await frequency.getByText("Modulation: 340–540 Hz").waitFor();
    await page.getByRole("button", { name: "Redo", exact: true }).click();
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
    // The same oscillator switches waveforms during playback without losing cables or parameters.
    const waveform = articles.nth(1).getByLabel("Waveform");
    assert.equal(await waveform.inputValue(), "0");
    await waveform.selectOption("2");
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 1}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await wire.count(), 1);
    assert.equal(await articles.nth(1).getByRole("spinbutton", { name: "Frequency (Hz)" }).inputValue(), "330");
    assert.equal(await play.isEnabled(), false);
    // Switching modes keeps the frequency connection visible but dormant,
    // then restores the same live input and value when returning to free mode.
    const timing = articles.nth(1).getByLabel("Timing mode");
    await timing.selectOption("1");
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 1}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await wire.count(), 1);
    assert.equal(await page.locator(".connection-dormant").count(), 1);
    assert.equal(await articles.nth(1).locator(".port-dormant").count(), 1);
    assert.equal(await frequency.count(), 0);
    assert.equal(await articles.nth(1).getByLabel("Musical division").inputValue(), "2");
    await timing.selectOption("0");
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 1}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await page.locator(".connection-dormant").count(), 0);
    assert.equal(await wire.count(), 1);
    assert.equal(await articles.nth(1).getByRole("spinbutton", { name: "Frequency (Hz)" }).inputValue(), "330");
    await marker.waitFor({ timeout: 10000 });
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
    let droneRevision = Number((await status.innerText()).match(/Playing revision (\d+)/)[1]);
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
    assert.equal(await rack.locator(".node-preset-selector").count(), 0);
    const childPreset = page.getByRole("combobox", { name: "Preset for Oscillator" });
    await childPreset.selectOption("oscillator-deep-sine");
    await status.filter({ hasText: new RegExp(`^Playing revision ${droneRevision + 1}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await childFrequency.inputValue(), "110");
    await childFrequency.locator("xpath=../..").locator(".parameter-effective-marker").waitFor({ timeout: 10000 });
    assert.equal(await childPreset.inputValue(), "oscillator-deep-sine");
    await undoButton.click();
    await status.filter({ hasText: new RegExp(`^Playing revision ${droneRevision + 2}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await childFrequency.inputValue(), "440");
    assert.equal(await childPreset.inputValue(), "");
    droneRevision += 2;
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
    // Any oscillator can be a synchronized modulation source.
    await page.locator("#node-type").selectOption("generator.oscillator");
    const newNodeIndex = await articles.count();
    await page.getByRole("button", { name: "Add", exact: true }).click();
    const tempoLfo = articles.nth(newNodeIndex);
    await tempoLfo.getByLabel("Timing mode").selectOption("1");
    await tempoLfo.getByLabel("Waveform").selectOption("1");
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
    // Wait for the debounced save before closing the editor, then restore a nested workspace.
    await drone.getByRole("button", { name: "Open Drone" }).click();
    await childFrequency.waitFor();
    await page.getByRole("button", { name: "Zoom out" }).click();
    const savedTransform = await page.locator(".canvas-world").getAttribute("style");
    const readSavedWorkspace = async () => page.evaluate(async () => {
        const database = await new Promise((resolve, reject) => {
            const request = indexedDB.open("orbit2-patches", 1);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        try {
            const saved = await new Promise((resolve, reject) => {
                const request = database.transaction("workspace").objectStore("workspace").get("current");
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
            return saved ? JSON.parse(saved) : null;
        } finally { database.close(); }
    });
    const expectedGraph = await page.locator(".graph-workspace").getAttribute("data-active-graph");
    const values = savedTransform.match(/translate\(([^,]+)px,([^,]+)px\) scale\(([^)]+)\)/).slice(1).map(Number);
    let savedWorkspace;
    // Poll resolved IDB reads on the runner, rather than letting a browser-side
    // wait predicate treat an unresolved promise as a successful condition.
    for (let i = 0; i < 100; i++) {
        const document = await readSavedWorkspace();
        const layout = document?.canvasLayouts.find(item => item.graphId === expectedGraph);
        if (document?.activeGraphId === expectedGraph && document.beatsPerMinute === 90 && layout &&
            [layout.panX, layout.panY, layout.zoom].every((value, index) => value === values[index])) {
            savedWorkspace = document;
            break;
        }
        await page.waitForTimeout(100);
    }
    assert.ok(savedWorkspace, "Expected the latest child graph and viewport to be saved");
    assert.ok(savedWorkspace.rootGraph.nodes.some(node => node.composite?.graph.id === expectedGraph));
    await page.reload();
    await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
    assert.equal(await page.getByRole("button", { name: "Undo", exact: true }).isEnabled(), false);
    assert.equal(await page.getByRole("button", { name: "Redo", exact: true }).isEnabled(), false);
    assert.equal(await page.getByRole("alert").count(), 0, await page.getByRole("alert").allTextContents());
    assert.equal(await page.locator(".graph-workspace").getAttribute("data-active-graph"), expectedGraph);
    await breadcrumbs.getByText("Drone", { exact: true }).waitFor();
    // Input DOM IDs belong to transient editor components. The domain identity
    // is covered by the serialization round-trip test, not the DOM ID.
    await childFrequency.waitFor();
    assert.equal(await childFrequency.inputValue(), "330");
    assert.equal(await tempo.inputValue(), "90");
    assert.equal(await page.locator(".canvas-world").getAttribute("style"), savedTransform);
    await rack.getByRole("button", { name: "Output: Cutoff (Modulation)" }).waitFor();
    assert.equal(await play.isEnabled(), true);
    await breadcrumbs.getByRole("button", { name: "Patch" }).click();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    assert.equal(await division.inputValue(), "4");
    // Named saves are independent of recovery and imported/exported documents.
    await page.getByRole("button", { name: "Patches", exact: true }).click();
    const manager = page.locator(".patch-manager");
    await manager.waitFor().catch(async error => {
        console.error("Patch menu header:", await page.locator(".workspace-header").innerHTML());
        throw error;
    });
    await manager.getByLabel("Patch name").fill("Drone study");
    await manager.getByRole("button", { name: "Save as" }).click();
    await manager.getByText("Saved Drone study.", { exact: true }).waitFor();
    const downloadPromise = page.waitForEvent("download");
    await manager.getByRole("button", { name: "Export JSON" }).click();
    const download = await downloadPromise;
    const exported = await readFile(await download.path());
    assert.equal(JSON.parse(exported.toString()).beatsPerMinute, 90);
    await manager.getByRole("button", { name: "New patch" }).click();
    await manager.getByText("New workspace created.", { exact: true }).waitFor();
    await page.getByText("1 nodes · 0 connections", { exact: true }).waitFor();
    await manager.getByLabel("Patch name").fill("Empty study");
    await manager.getByRole("button", { name: "Save as" }).click();
    await manager.getByText("Saved Empty study.", { exact: true }).waitFor();
    await manager.getByLabel("Saved patch").selectOption("Drone study");
    await manager.getByRole("button", { name: "Open patch" }).click();
    await manager.getByText("Opened Drone study.", { exact: true }).waitFor();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    await status.filter({ hasText: /Ready/ }).waitFor();
    assert.equal(await tempo.inputValue(), "90");
    await manager.getByLabel("Import JSON").setInputFiles({ name: "bad.json", mimeType: "application/json", buffer: Buffer.from("{}") });
    await manager.getByRole("alert").waitFor();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    await manager.getByLabel("Saved patch").selectOption("Empty study");
    await manager.getByRole("button", { name: "Open patch" }).click();
    await manager.getByText("Opened Empty study.", { exact: true }).waitFor();
    await manager.getByLabel("Import JSON").setInputFiles({ name: "exported.json", mimeType: "application/json", buffer: exported });
    await manager.getByText("Imported patch.", { exact: true }).waitFor();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    await manager.getByRole("button", { name: "Delete saved patch" }).click();
    await manager.getByText("Saved patch deleted; current workspace unchanged.").waitFor();
    assert.equal(await manager.getByLabel("Saved patch").locator("option[value='Empty study']").count(), 0);
    assert.ok(requests.has(`/_content/Orbit.Infrastructure/persistence-${version}/patchStorage.js`));
    assert.ok([...requests].every(path => !path.startsWith("/_content/Orbit.Infrastructure/persistence/")));
    console.log("Published editor played, edited nested graphs and restored the saved workspace after reload.");
} finally {
    await browser?.close();
    server.close();
}
