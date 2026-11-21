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
    const status = page.locator(".playback-toolbar").getByRole("status");
    const play = page.getByRole("button", { name: "Play patch" });
    const articles = page.locator("article");
    const frequency = articles.nth(1).locator(".parameter-slider").filter({ has: page.getByRole("spinbutton", { name: "Frequency (Hz)" }) });
    const amplitude = articles.nth(1).locator(".parameter-slider").filter({ has: page.getByRole("spinbutton", { name: "Amplitude (normalized)" }) });
    const marker = frequency.locator(".parameter-effective-marker");
    await frequency.getByText("Modulation: 340–540 Hz").waitFor({ timeout: 30000 });
    // Physical frequency and visual coordinates are deliberately different.
    const frequencyRange = frequency.getByRole("slider", { name: "Frequency" });
    const frequencyInput = frequency.getByRole("spinbutton", { name: "Frequency (Hz)" });
    const frequencyPosition = value => {
        const anchors = [[0, 0.01], [0.2, 40], [0.85, 1000], [1, 20000]];
        const index = value <= 40 ? 0 : value <= 1000 ? 1 : 2;
        const [start, minimum] = anchors[index], [end, maximum] = anchors[index + 1];
        return start + (end - start) * Math.log(value / minimum) / Math.log(maximum / minimum);
    };
    assert.equal(await frequencyRange.getAttribute("min"), "0");
    assert.equal(await frequencyRange.getAttribute("max"), "1");
    assert.ok(Math.abs(Number(await frequencyRange.inputValue()) - frequencyPosition(440)) < 0.0001);
    const band = await frequency.locator(".parameter-band").evaluate(element => ({
        left: parseFloat(element.style.left), width: parseFloat(element.style.width)
    }));
    assert.ok(Math.abs(band.left - frequencyPosition(340) * 100) < 0.02);
    assert.ok(Math.abs(band.width - (frequencyPosition(540) - frequencyPosition(340)) * 100) < 0.02);
    assert.equal(await frequency.locator(".parameter-scale-tick").allTextContents().then(text => text.join(",")), "40,1k");
    // Primary controls share one compact toolbar; the canvas consumes the
    // remaining screen height rather than using a fixed viewport fraction.
    const toolbar = page.locator(".canvas-caption");
    await toolbar.getByRole("button", { name: "Zoom out" }).waitFor();
    await toolbar.getByRole("button", { name: "Add", exact: true }).waitFor();
    await toolbar.getByRole("button", { name: "Play patch" }).waitFor();
    const viewport = page.locator(".canvas-viewport");
    const panCanvas = async delta => {
        const world = page.locator(".canvas-world");
        const previous = await world.getAttribute("style");
        const view = await viewport.boundingBox();
        const pointer = { pointerId: 77, pointerType: "mouse", button: 1, buttons: 4,
            clientX: view.x + 30, clientY: view.y + 30 };
        await viewport.dispatchEvent("pointerdown", pointer);
        await viewport.dispatchEvent("pointermove", { ...pointer, clientX: pointer.clientX + delta, clientY: pointer.clientY + delta });
        await viewport.dispatchEvent("pointerup", { ...pointer, buttons: 0 });
        await page.waitForFunction(old => document.querySelector(".canvas-world").getAttribute("style") !== old, previous);
    };
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
    // Inverse mapping commits physical values and preserves ordinary history.
    await frequencyRange.fill("0.2");
    assert.equal(await frequencyInput.inputValue(), "40");
    assert.equal(await frequencyRange.getAttribute("aria-valuetext"), "40 Hz");
    await undoButton.click();
    assert.equal(await frequencyInput.inputValue(), "440");
    await frequencyRange.fill("0.85");
    assert.equal(await frequencyInput.inputValue(), "1000");
    await undoButton.click();
    assert.equal(await frequencyInput.inputValue(), "440");
    assert.equal(await undoButton.isEnabled(), false);
    // The shared catalog selector applies an entire configuration in one undo step.
    const rootPreset = articles.nth(1).getByRole("combobox", { name: "Preset for Oscillator" });
    const assertPresetControlsFit = async () => {
        const rows = await page.locator(".node-preset-heading").evaluateAll(headings => headings.map(heading => {
            const bounds = element => {
                const { left, right, width } = element.getBoundingClientRect();
                return { left, right, width };
            };
            return { node: bounds(heading.closest("article")), heading: bounds(heading),
                label: bounds(heading.querySelector("label")), select: bounds(heading.querySelector("select")),
                manage: bounds(heading.querySelector(".node-preset-manage")) };
        }));
        assert.ok(rows.length > 0, "Expected preset controls on this canvas");
        for (const { node, heading, label, select, manage } of rows) {
            assert.ok(heading.left >= node.left && heading.right <= node.right + 1, "Preset row must fit the node");
            assert.ok(label.right <= select.left + 1 && select.width > 0 && select.right <= manage.left + 1,
                "Preset selector must fit between the label and management button");
            assert.ok(manage.width > 0 && manage.right <= heading.right + 1, "Preset management button must stay inside the node");
        }
    };
    await assertPresetControlsFit();
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
    assert.ok(Math.abs(Number(await frequencyRange.inputValue()) - frequencyPosition(110)) < 0.0001);
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
    // Custom library edits are independent of the graph, with explicit management actions.
    const presetManager = page.getByRole("region", { name: "Custom preset manager" });
    const manageRootPresets = articles.nth(1).getByRole("button", { name: "Manage presets for Oscillator" });
    await manageRootPresets.click();
    const longPresetName = "My carrier with a very long descriptive name for layered atmospheric drones";
    await presetManager.getByLabel("Preset name").fill(longPresetName);
    await presetManager.getByLabel("Description (optional, for new presets)").fill("A reusable test configuration");
    await presetManager.getByRole("button", { name: "Save as" }).click();
    await presetManager.getByText(`Saved ${longPresetName}.`, { exact: true }).waitFor({ timeout: 10000 }).catch(async error => {
        console.error("Custom preset save panel:", await presetManager.innerText());
        console.error("Custom preset controls:", await presetManager.locator("input, select, button").evaluateAll(controls =>
            controls.map(control => ({ tag: control.tagName, text: control.textContent, value: control.value, disabled: control.disabled }))));
        throw error;
    });
    const customId = await presetManager.getByLabel("Saved custom preset").inputValue();
    assert.ok(customId);
    assert.equal(await rootPreset.inputValue(), customId);
    assert.equal(await articles.nth(0).locator(`optgroup[label='Custom'] option[value='${customId}']`).count(), 1);
    await assertPresetControlsFit();
    assert.equal(await undoButton.isEnabled(), false);
    await presetManager.getByLabel("Preset name").fill(longPresetName.toUpperCase());
    await presetManager.getByRole("button", { name: "Save as" }).click();
    await presetManager.getByRole("alert").waitFor();
    await presetManager.getByLabel("Preset name").fill("My renamed carrier");
    await presetManager.getByRole("button", { name: "Rename to preset name" }).click();
    await presetManager.getByText("Renamed to My renamed carrier.", { exact: true }).waitFor();
    assert.equal(await rootPreset.locator(`option[value='${customId}']`).innerText(), "My renamed carrier");
    await presetManager.getByRole("button", { name: "Close" }).click();
    await frequency.getByRole("spinbutton").fill("333");
    await manageRootPresets.click();
    await presetManager.getByLabel("Saved custom preset").selectOption(customId);
    await presetManager.getByRole("button", { name: "Overwrite with current settings" }).click();
    await presetManager.getByText("Updated My renamed carrier.", { exact: true }).waitFor();
    // Portable JSON is an independent, single-preset file, not a patch or catalog array.
    const presetDownloadPromise = page.waitForEvent("download");
    await presetManager.getByRole("button", { name: "Export selected JSON" }).click();
    const presetDownload = await presetDownloadPromise;
    const presetFile = await readFile(await presetDownload.path());
    const portablePreset = JSON.parse(presetFile.toString());
    assert.equal(portablePreset.id, customId);
    assert.equal(portablePreset.schemaVersion, 1);
    assert.equal(portablePreset.nodeType, "generator.oscillator");
    assert.equal(portablePreset.parameters.frequency, 333);
    assert.equal(portablePreset.group, "Custom");
    const importPresetFile = buffer => presetManager.getByLabel("Import preset JSON").setInputFiles({
        name: "preset.json", mimeType: "application/json", buffer
    });
    await importPresetFile(Buffer.from("{}"));
    await presetManager.getByRole("alert").waitFor();
    assert.equal(await presetManager.getByRole("button", { name: "Import preset", exact: true }).count(), 0);
    await importPresetFile(presetFile);
    await presetManager.getByText("Ready to import: My renamed carrier", { exact: false }).waitFor();
    await presetManager.getByRole("button", { name: "Import preset", exact: true }).click();
    await presetManager.getByRole("alert").filter({ hasText: "already exists" }).waitFor();
    await presetManager.getByLabel("Name for imported copy").fill("Portable copy");
    await presetManager.getByRole("button", { name: "Import as copy" }).click();
    await presetManager.getByText("Imported Portable copy.", { exact: true }).waitFor();
    const copyId = await presetManager.getByLabel("Saved custom preset").inputValue();
    assert.notEqual(copyId, customId);
    assert.equal(await rootPreset.locator(`option[value='${copyId}']`).count(), 1);
    await presetManager.getByRole("button", { name: "Delete selected preset" }).click();
    await presetManager.getByText("Custom preset deleted; current node unchanged.", { exact: true }).waitFor();
    assert.equal(await rootPreset.locator(`option[value='${copyId}']`).count(), 0);
    await importPresetFile(Buffer.from(JSON.stringify({ ...portablePreset, parameters: { ...portablePreset.parameters, frequency: 334 } })));
    await presetManager.getByRole("button", { name: "Replace My renamed carrier", exact: true }).click();
    await presetManager.getByText("Imported My renamed carrier.", { exact: true }).waitFor();
    assert.equal(await frequency.getByRole("spinbutton").inputValue(), "333");
    // Removing the library entry and importing the original file restores the original ID.
    await presetManager.getByRole("button", { name: "Delete selected preset" }).click();
    await presetManager.getByText("Custom preset deleted; current node unchanged.", { exact: true }).waitFor();
    await importPresetFile(presetFile);
    await presetManager.getByRole("button", { name: "Import preset", exact: true }).click();
    await presetManager.getByText("Imported My renamed carrier.", { exact: true }).waitFor();
    assert.equal(await presetManager.getByLabel("Saved custom preset").inputValue(), customId);
    assert.equal(await rootPreset.inputValue(), customId);
    await presetManager.getByRole("button", { name: "Close" }).click();
    await undoButton.click();
    assert.equal(await frequency.getByRole("spinbutton").inputValue(), "440");
    await rootPreset.selectOption(customId);
    assert.equal(await frequency.getByRole("spinbutton").inputValue(), "333");
    await undoButton.click();
    assert.equal(await frequency.getByRole("spinbutton").inputValue(), "440");
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
    const liveReading = await frequency.evaluate(element => ({
        live: parseFloat(element.querySelector(".parameter-modulation span:last-child").textContent.replace("Live: ", "")),
        left: parseFloat(element.querySelector(".parameter-effective-marker").style.left)
    }));
    assert.ok(Number.isFinite(liveReading.live));
    assert.ok(Math.abs(liveReading.left - frequencyPosition(liveReading.live) * 100) < 0.03,
        "Live marker must share the frequency display mapping");
    const firstPosition = await marker.getAttribute("style");
    let moved = false;
    for (let i = 0; i < 20 && !moved; i++) {
        await page.waitForTimeout(100);
        moved = (await marker.getAttribute("style")) !== firstPosition;
    }
    assert.ok(moved, "Live frequency marker did not move");
    // The shared scope renders actual browser capture without changing graph revision.
    await articles.nth(1).locator(".node-header > span").click();
    const compact = articles.nth(1).locator(".node-visualization-panel");
    await compact.getByRole("button", { name: "Show plot" }).click();
    await page.getByRole("button", { name: "Fit", exact: true }).click();
    await compact.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor({ timeout: 15000 });
    await panCanvas(4000);
    await compact.locator("[data-scope-status]").filter({ hasText: "Paused" }).waitFor();
    await panCanvas(-4000);
    await compact.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor({ timeout: 15000 });
    await compact.getByRole("combobox", { name: "Analysis view" }).selectOption("spectrum");
    await compact.locator('.signal-scope[data-configured-mode="spectrum"]').waitFor();
    await compact.locator("[data-scope-status]").filter({ hasText: /Spectrum · peak bin/ }).waitFor();
    await compact.getByRole("button", { name: "Expand plot" }).click();
    const scope = page.getByRole("region", { name: "Signal scope", exact: true });
    await scope.waitFor();
    await scope.locator('.signal-scope[data-configured-mode="spectrum"]').waitFor();
    await scope.locator('[data-view-mode="spectrum"]').waitFor();
    assert.equal(await compact.locator("canvas").count(), 0, "Expanded plot must replace its compact renderer");
    assert.equal(await scope.getByRole("combobox", { name: "Analysis view" }).inputValue(), "spectrum");
    await scope.getByRole("combobox", { name: "Analysis view" }).selectOption("waveform");
    await scope.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor({ timeout: 15000 });
    const peakText = await scope.locator("[data-peak]").innerText();
    assert.match(peakText, /-?\d+\.\d dBFS/);
    const rms = Number.parseFloat(await scope.locator("[data-rms]").innerText());
    assert.ok(Number.isFinite(rms) && rms <= Number.parseFloat(peakText));
    await scope.getByRole("combobox", { name: "Scope timebase" }).selectOption("5");
    await scope.locator("[data-scope-status]").filter({ hasText: "History" }).waitFor();
    assert.equal(await scope.getByRole("combobox", { name: "Scope trigger" }).isDisabled(), true);
    await scope.getByRole("combobox", { name: "Scope timebase" }).selectOption("0.005");
    await scope.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor();
    await scope.getByRole("combobox", { name: "Scope trigger" }).selectOption("falling");
    await scope.getByRole("combobox", { name: "Analysis view" }).selectOption("spectrum");
    await scope.locator("[data-scope-status]").filter({ hasText: /Spectrum · peak bin/ }).waitFor();
    const spectrumText = await scope.locator("[data-scope-status]").innerText();
    const peakFrequency = Number(spectrumText.match(/peak bin ([\d.]+) Hz/)[1]);
    assert.ok(peakFrequency > 250 && peakFrequency < 700, "Spectrum must measure the modulated carrier");
    // Ensure the actual spectrum renderer painted the themed trace, not just a text readout.
    const tracePixels = await scope.locator("canvas").evaluate(canvas => {
        const probe = document.createElement("canvas");
        probe.width = probe.height = 1;
        const context = probe.getContext("2d");
        context.fillStyle = getComputedStyle(canvas).getPropertyValue("--semantic-color-modulation").trim();
        context.fillRect(0, 0, 1, 1);
        const target = context.getImageData(0, 0, 1, 1).data;
        const pixels = canvas.getContext("2d").getImageData(0, 0, canvas.width, canvas.height).data;
        let count = 0;
        for (let i = 0; i < pixels.length; i += 4)
            if (Math.abs(pixels[i] - target[0]) + Math.abs(pixels[i+1] - target[1]) + Math.abs(pixels[i+2] - target[2]) < 90) count++;
        return count;
    });
    assert.ok(tracePixels > 10, "Spectrum trace was not drawn");
    assert.equal(await scope.getByRole("combobox", { name: "Scope timebase" }).isVisible(), false);
    const originalResolution = await scope.locator("[data-scale]").innerText();
    await scope.getByRole("combobox", { name: "FFT resolution" }).selectOption("1024");
    await scope.getByRole("combobox", { name: "FFT window" }).selectOption("rectangular");
    await scope.getByRole("combobox", { name: "Spectrum smoothing" }).selectOption("0");
    await page.waitForFunction(previous => {
        const value = document.querySelector("[data-scale]")?.textContent;
        return value?.includes("Hz/bin") && value !== previous;
    }, originalResolution);
    await scope.getByRole("combobox", { name: "Analysis view" }).selectOption("waveform");
    await scope.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await scope.getByRole("button", { name: "Close scope" }).click();
    await scope.waitFor({ state: "detached" });
    await compact.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor({ timeout: 15000 });
    await compact.getByRole("combobox", { name: "Analysis view" }).selectOption("level");
    await compact.locator("[data-scope-status]").filter({ hasText: "Live · level meter" }).waitFor();
    assert.equal(await compact.locator("canvas").isVisible(), false);
    const compactSource = compact.getByRole("combobox", { name: "Scope signal" });
    await compactSource.selectOption({ label: "Input: Frequency" });
    const savedScopeSource = await compactSource.inputValue();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await compact.getByRole("button", { name: "Hide plot" }).click();
    await compact.locator("canvas").waitFor({ state: "detached" });
    await page.getByRole("button", { name: "Fit", exact: true }).click();
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
    const synchronizedFrequency = articles.nth(1).locator("output[aria-label='Synchronized frequency']");
    await synchronizedFrequency.filter({ hasText: /^1\.5 Hz$/ }).waitFor();
    await tempo.fill("120");
    await applyTempo.click();
    await synchronizedFrequency.filter({ hasText: /^2 Hz$/ }).waitFor();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision + 1}`);
    await tempo.fill("90");
    await applyTempo.click();
    await synchronizedFrequency.filter({ hasText: /^1\.5 Hz$/ }).waitFor();
    await timing.selectOption("0");
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 1}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await page.locator(".connection-dormant").count(), 0);
    assert.equal(await wire.count(), 1);
    assert.equal(await articles.nth(1).getByRole("spinbutton", { name: "Frequency (Hz)" }).inputValue(), "330");
    assert.equal(await synchronizedFrequency.count(), 0);
    await marker.waitFor({ timeout: 10000 });
    // Unconnected nodes do not require a new worklet.
    await page.getByRole("button", { name: "Add", exact: true }).click();
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 2}$`) }).waitFor({ timeout: 10000 });
    // Wires run underneath node frames; viewport-aware placement may put a
    // frame over the midpoint without hiding other parts of the cable.
    const selectVisibleWire = async target => {
        await target.scrollIntoViewIfNeeded();
        const point = await target.evaluate(path => {
            const length = path.getTotalLength();
            const matrix = path.getScreenCTM();
            for (let i = 0; i <= 100; i++) {
                const fraction = 0.5 + (i % 2 ? -1 : 1) * Math.ceil(i / 2) / 100;
                const point = path.getPointAtLength(length * fraction).matrixTransform(matrix);
                if (document.elementFromPoint(point.x, point.y) === path) return { x: point.x, y: point.y };
            }
            return null;
        });
        assert.ok(point, "Expected an exposed part of the cable to select");
        await page.mouse.click(point.x, point.y);
        await target.locator("xpath=..").and(page.locator(".connection-selected")).waitFor({ state: "attached", timeout: 4000 });
    };
    // Remove the original LFO-to-frequency connection, then connect to amplitude.
    await selectVisibleWire(wire);
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
    await selectVisibleWire(masterWire);
    await page.getByRole("button", { name: "Delete selected" }).click();
    await articles.nth(0).getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await drone.getByRole("button", { name: "Input: Pitch (Modulation)" }).press("Enter");
    await drone.getByRole("button", { name: "Output: Audio (Audio)" }).press("Enter");
    await articles.nth(3).getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    assert.equal(await page.getByRole("alert").count(), 0, await page.getByRole("alert").allTextContents());
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
    await assertPresetControlsFit();
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
    await compact.getByRole("button", { name: "Show plot" }).click();
    assert.equal(await compact.getByRole("combobox", { name: "Analysis view" }).inputValue(), "level", "Plot mode survives reload and nested navigation");
    assert.equal(await compact.getByRole("combobox", { name: "Scope signal" }).inputValue(), savedScopeSource, "Plot source survives reload and nested navigation");
    await compact.getByRole("button", { name: "Hide plot" }).click();
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
    const tempoLfoFrequency = tempoLfo.locator("output[aria-label='Synchronized frequency']");
    await tempoLfoFrequency.filter({ hasText: /^1\.5 Hz$/ }).waitFor();
    await division.selectOption("3");
    await tempoLfoFrequency.filter({ hasText: /^0\.75 Hz$/ }).waitFor();
    await tempoLfo.getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await drone.getByRole("button", { name: "Input: Gain (Modulation)" }).press("Enter");
    await play.click();
    await status.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    await transportState.filter({ hasText: "Running" }).waitFor();
    await division.selectOption("4");
    assert.equal(await division.inputValue(), "4");
    await tempoLfoFrequency.filter({ hasText: /^0\.375 Hz$/ }).waitFor();
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
    // The same custom configuration remains available on reload and in a nested graph.
    await childPreset.locator(`optgroup[label='Custom'] option[value='${customId}']`).waitFor({ state: "attached" });
    await childPreset.selectOption(customId);
    assert.equal(await childFrequency.inputValue(), "333");
    await undoButton.click();
    assert.equal(await childFrequency.inputValue(), "330");
    await childPreset.selectOption("oscillator-quarter-pulse");
    await page.locator("output[aria-label='Synchronized frequency']").filter({ hasText: /^1\.5 Hz$/ }).waitFor();
    await undoButton.click();
    await childFrequency.waitFor();
    assert.equal(await childFrequency.inputValue(), "330");
    await breadcrumbs.getByRole("button", { name: "Patch" }).click();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    assert.equal(await division.inputValue(), "4");
    await tempoLfoFrequency.filter({ hasText: /^0\.375 Hz$/ }).waitFor();
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
    // Opening and importing other patches does not remove the independent custom library.
    await manager.getByRole("button", { name: "Close" }).click();
    await rootPreset.locator(`optgroup[label='Custom'] option[value='${customId}']`).waitFor({ state: "attached" });
    await rootPreset.selectOption(customId);
    await manageRootPresets.click();
    await presetManager.getByLabel("Saved custom preset").selectOption(customId);
    await presetManager.getByRole("button", { name: "Delete selected preset" }).click();
    await presetManager.getByText("Custom preset deleted; current node unchanged.", { exact: true }).waitFor();
    await presetManager.getByRole("button", { name: "Close" }).click();
    assert.equal(await rootPreset.locator(`option[value='${customId}']`).count(), 0);
    assert.equal(await frequency.getByRole("spinbutton").inputValue(), "333");
    assert.equal(await rootPreset.inputValue(), "");
    assert.ok(requests.has(`/_content/Orbit.Infrastructure/persistence-${version}/patchStorage.js`));
    assert.ok([...requests].every(path => !path.startsWith("/_content/Orbit.Infrastructure/persistence/")));
    // Interactive additions use the current viewport, not the origin grid.
    // Pan the real canvas far beyond the existing nodes on both graph levels.
    const addInPannedView = async (type, delta) => {
        const world = page.locator(".canvas-world");
        await panCanvas(delta);
        await page.getByRole("button", { name: "Zoom in", exact: true }).click();
        await page.locator("#node-type").selectOption(type);
        const transform = await world.getAttribute("style");
        const existing = await articles.evaluateAll(nodes => Object.fromEntries(nodes.map(node => [node.dataset.nodeId, node.getAttribute("style")])));
        const count = await articles.count();
        await page.getByRole("button", { name: "Add", exact: true }).click();
        const added = articles.nth(count);
        await added.waitFor();
        assert.equal(await world.getAttribute("style"), transform, "Adding must not pan or zoom the canvas");
        const result = await added.evaluate(node => {
            const header = node.querySelector(".node-header").getBoundingClientRect();
            const canvas = node.closest(".canvas-viewport").getBoundingClientRect();
            return { x: header.left, y: header.top, canvasLeft: Math.max(0, canvas.left), canvasTop: Math.max(0, canvas.top),
                canvasRight: Math.min(innerWidth, canvas.right), canvasBottom: Math.min(innerHeight, canvas.bottom) };
        });
        assert.ok(result.x >= result.canvasLeft && result.x < result.canvasRight &&
            result.y >= result.canvasTop && result.y < result.canvasBottom, `New node header outside view: ${JSON.stringify(result)}`);
        assert.deepEqual(await articles.evaluateAll((nodes, ids) => Object.fromEntries(nodes.filter(node => ids.includes(node.dataset.nodeId))
            .map(node => [node.dataset.nodeId, node.getAttribute("style")])), Object.keys(existing)), existing);
        const id = await added.getAttribute("data-node-id");
        const position = await added.getAttribute("style");
        await undoButton.click();
        await page.locator(`[data-node-id='${id}']`).waitFor({ state: "detached" });
        await redoButton.click();
        const restored = page.locator(`[data-node-id='${id}']`);
        await restored.waitFor();
        assert.equal(await restored.getAttribute("style"), position, "Redo restores the original world placement");
        return restored;
    };
    const newDrone = await addInPannedView("composite.graph", -3200);
    await newDrone.getByRole("button", { name: "Open Drone" }).click();
    await viewport.getByText("4 nodes · 4 connections", { exact: true }).waitFor();
    await addInPannedView("generator.noise", 3200);
    console.log("Published editor played, edited nested graphs and restored the saved workspace after reload.");
} finally {
    await browser?.close();
    server.close();
}
