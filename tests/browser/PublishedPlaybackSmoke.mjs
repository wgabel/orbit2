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
    // Exercise device-pixel bitmap resizing independently of CSS plot width.
    const page = await browser.newPage({ deviceScaleFactor: 2 });
    page.on("pageerror", error => console.error(error.message));
    await page.goto("http://127.0.0.1:" + server.address().port);
    const status = page.locator(".playback-toolbar").getByRole("status");
    const play = page.getByRole("button", { name: "Play patch" });
    const articles = page.locator(".node-frame");
    const frequency = articles.nth(1).locator(".parameter-slider").filter({ has: page.getByRole("spinbutton", { name: "Frequency (Hz)" }) });
    const amplitude = articles.nth(1).locator(".parameter-slider").filter({ has: page.getByRole("spinbutton", { name: "Amplitude (normalized)" }) });
    const marker = frequency.locator(".parameter-effective-marker");
    const predictedFrequency = frequency.getByLabel("Predicted modulation range");
    await predictedFrequency.filter({ hasText: "340–540 Hz" }).waitFor({ timeout: 30000 });
    // Probe the real readout CSS with deliberately wide values without altering
    // the live Blazor DOM or graph. The clone shares the node's fixed width.
    const readoutSizes = await frequency.evaluate(slider => {
        const copy = slider.closest("article").cloneNode(true);
        copy.style.visibility = "hidden";
        slider.closest("article").parentElement.append(copy);
        try {
            const readings = copy.querySelector(".parameter-slider:has([aria-label='Frequency']) .parameter-modulation");
            const range = readings.querySelector("[aria-label='Predicted modulation range']");
            const live = readings.querySelector("[aria-label='Live effective value']");
            return [["340–540 Hz", "—"], ["0.01–20000 Hz", "999.99 Hz"], ["19999.99–20000 Hz", "19999.99 Hz"],
                ["unknown", "-123456789.99 normalized"]].map(([predicted, effective]) => {
                range.textContent = predicted;
                live.textContent = effective;
                const row = readings.getBoundingClientRect();
                const within = [range, live].every(output => {
                    const rect = output.getBoundingClientRect();
                    return rect.left >= row.left - 1 && rect.right <= row.right + 1 && output.scrollHeight <= output.clientHeight + 1;
                });
                return { height: readings.offsetHeight, nodeHeight: copy.offsetHeight, within };
            });
        } finally { copy.remove(); }
    });
    assert.ok(readoutSizes.every(size => size.within && size.height === readoutSizes[0].height && size.nodeHeight === readoutSizes[0].nodeHeight),
        `Readout height must be independent of value length: ${JSON.stringify(readoutSizes)}`);
    // Physical frequency and visual coordinates are deliberately different.
    const frequencyRange = frequency.getByRole("slider", { name: "Frequency" });
    const frequencyInput = frequency.getByRole("spinbutton", { name: "Frequency (Hz)" });
    assert.equal(await frequency.getByLabel("Cycle duration", { exact: true }).innerText(), "2.273 ms");
    assert.equal(await frequency.getByLabel("Positive duration").innerText(), "1.136 ms");
    assert.equal(Number(await frequency.getByRole("spinbutton", { name: "Period (s)" }).inputValue()), 1 / 440);
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
    const exerciseNote = async (revision, title) => {
        const nodeCount = await articles.count();
        const undoEnabled = await page.getByRole("button", { name: "Undo", exact: true }).isEnabled();
        await page.locator("#node-type").selectOption("canvas.note");
        await toolbar.getByRole("button", { name: "Add", exact: true }).click();
        const note = page.locator(".note-card");
        await note.waitFor();
        assert.equal(await articles.count(), nodeCount);
        await note.getByRole("button", { name: "Edit", exact: true }).click();
        await note.getByLabel("Title").fill(title);
        await note.getByLabel("Body").fill("A minor\nOpen fifth");
        await note.getByLabel("Accent").selectOption("Idea");
        await note.getByRole("button", { name: "Apply", exact: true }).click();
        assert.equal(await note.locator(".note-title").innerText(), title);
        assert.equal(await note.locator(".note-body").textContent(), "A minor\nOpen fifth");
        await page.getByRole("button", { name: "Undo note", exact: true }).click();
        assert.equal(await note.locator(".note-title").innerText(), "Note");
        await page.getByRole("button", { name: "Redo note", exact: true }).click();
        assert.equal(await note.locator(".note-title").innerText(), title);
        await note.getByTitle("Collapse note").click();
        assert.equal(await note.locator(".note-body").count(), 0);
        await note.getByTitle("Expand note").click();
        await note.getByRole("button", { name: "Duplicate", exact: true }).click();
        assert.equal(await note.count(), 2);
        await page.getByRole("button", { name: "Undo note", exact: true }).click();
        assert.equal(await note.count(), 1);
        await note.getByRole("button", { name: "Delete", exact: true }).click();
        assert.equal(await note.count(), 0);
        assert.equal(await page.getByRole("button", { name: "Undo", exact: true }).isEnabled(), undoEnabled);
        assert.equal(await status.innerText(), `Playing revision ${revision}`);
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
    const periodInput = frequency.getByRole("spinbutton", { name: "Period (s)" });
    await periodInput.fill("5");
    await frequency.getByRole("button", { name: "Apply period" }).click();
    await frequency.getByLabel("Cycle duration", { exact: true }).filter({ hasText: /^5 s$/ }).waitFor();
    assert.equal(await frequencyInput.inputValue(), "0.2");
    await undoButton.click();
    assert.equal(await frequencyInput.inputValue(), "440");
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
    assert.equal(await frequencyInput.inputValue(), "110");
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
    assert.equal(await frequencyInput.inputValue(), "440");
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
    await frequencyInput.fill("333");
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
    assert.equal(await frequencyInput.inputValue(), "333");
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
    assert.equal(await frequencyInput.inputValue(), "440");
    await rootPreset.selectOption(customId);
    assert.equal(await frequencyInput.inputValue(), "333");
    await undoButton.click();
    assert.equal(await frequencyInput.inputValue(), "440");
    assert.equal(await undoButton.isEnabled(), false);
    const muteSource = articles.nth(0).getByRole("button", { name: /^Mute / });
    const beforeMuteTransform = await articles.nth(0).getAttribute("style");
    await muteSource.click();
    assert.equal(await muteSource.getAttribute("aria-pressed"), "true");
    assert.equal(await articles.nth(0).getAttribute("style"), beforeMuteTransform);
    await predictedFrequency.filter({ hasText: "440–440 Hz" }).waitFor();
    await undoButton.click();
    assert.equal(await muteSource.getAttribute("aria-pressed"), "false");
    await predictedFrequency.filter({ hasText: "340–540 Hz" }).waitFor();
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
    await exerciseNote(initialRevision, "Root harmony");
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
        live: parseFloat(element.querySelector('[aria-label="Live effective value"]').textContent),
        left: parseFloat(element.querySelector(".parameter-effective-marker").style.left)
    }));
    assert.ok(Number.isFinite(liveReading.live));
    assert.ok(Math.abs(liveReading.left - frequencyPosition(liveReading.live) * 100) < 0.03,
        "Live marker must share the frequency display mapping");
    const firstPosition = await marker.getAttribute("style");
    const readoutHeight = await frequency.locator(".parameter-modulation").evaluate(element => element.offsetHeight);
    const nodeHeight = await articles.nth(1).evaluate(element => element.offsetHeight);
    let moved = false;
    for (let i = 0; i < 10 || (i < 20 && !moved); i++) {
        await page.waitForTimeout(100);
        moved ||= (await marker.getAttribute("style")) !== firstPosition;
        assert.equal(await frequency.locator(".parameter-modulation").evaluate(element => element.offsetHeight), readoutHeight);
        assert.equal(await articles.nth(1).evaluate(element => element.offsetHeight), nodeHeight, "Live telemetry must not resize a node");
    }
    assert.ok(moved, "Live frequency marker did not move");
    // The shared scope renders actual browser capture without changing graph revision.
    const assertCompactPlotFits = async panel => {
        const result = await panel.evaluate(section => {
            const node = section.closest("article").getBoundingClientRect();
            const overflow = [section, ...section.querySelectorAll(".scope-header, button, .scope-source, select, .signal-scope, canvas, .scope-readouts, .scope-levels, meter, output")]
                .filter(element => element.getClientRects().length > 0)
                .map(element => ({ name: element.getAttribute("aria-label") || element.className || element.tagName, rect: element.getBoundingClientRect() }))
                .filter(({ rect }) => rect.left < node.left - 1 || rect.right > node.right + 1)
                .map(({ name, rect }) => ({ name, left: rect.left, right: rect.right }));
            const canvas = section.querySelector("canvas");
            return { overflow, cssWidth: canvas?.clientWidth, bitmapWidth: canvas?.width, pixelRatio: devicePixelRatio };
        });
        assert.deepEqual(result.overflow, [], "Compact scope controls and plot must fit inside the node");
        if (result.cssWidth > 0) {
            assert.ok(result.bitmapWidth <= result.cssWidth * Math.min(2, result.pixelRatio) + 1,
                "Canvas bitmap cannot cause CSS layout to grow");
        }
    };
    await articles.nth(1).locator(".node-header > span").click();
    const compact = articles.nth(1).locator(".node-visualization-panel");
    await compact.getByRole("button", { name: "Show plot" }).click();
    await page.getByRole("button", { name: "Fit", exact: true }).click();
    await compact.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor({ timeout: 15000 });
    await assertCompactPlotFits(compact);
    await panCanvas(4000);
    await compact.locator("[data-scope-status]").filter({ hasText: "Paused" }).waitFor();
    await panCanvas(-4000);
    await compact.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor({ timeout: 15000 });
    await compact.getByRole("combobox", { name: "Analysis view" }).selectOption("spectrum");
    await compact.locator('.signal-scope[data-configured-mode="spectrum"]').waitFor();
    await compact.locator("[data-scope-status]").filter({ hasText: /Spectrum · peak bin/ }).waitFor();
    await assertCompactPlotFits(compact);
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
    await scope.getByRole("combobox", { name: "Scope timebase" }).selectOption("cycle");
    await scope.locator("[data-scale]").filter({ hasText: /cycle/ }).waitFor();
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
    await assertCompactPlotFits(compact);
    assert.equal(await compact.locator("canvas").isVisible(), false);
    const compactSource = compact.getByRole("combobox", { name: "Scope signal" });
    await compactSource.selectOption({ label: "Input: Frequency" });
    await assertCompactPlotFits(compact);
    const savedScopeSource = await compactSource.inputValue();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await compact.getByRole("button", { name: "Hide plot" }).click();
    await compact.locator("canvas").waitFor({ state: "detached" });
    await page.getByRole("button", { name: "Fit", exact: true }).click();
    // Select the existing modulation wire via its keyboard-accessible badge,
    // even if a node covers it, then edit depth and retain its identity.
    const wire = page.locator(".connection-modulation .connection-hit");
    const labelMode = page.getByRole("combobox", { name: "Connection labels" });
    const badges = page.locator(".connection-amount-badge");
    assert.equal(await labelMode.inputValue(), "All");
    await badges.filter({ hasText: /^\+100 Hz$/ }).press("Enter");
    await page.locator(".connection-modulation.connection-selected").waitFor({ state: "attached" });
    const amountEditor = page.getByRole("region", { name: "Connection amount editor" });
    const amount = amountEditor.getByRole("spinbutton", { name: "Modulation depth (Hz)" });
    await amount.waitFor();
    assert.equal(await amount.inputValue(), "100");
    const assertEditorFits = async () => {
        await amountEditor.waitFor({ state: "visible" });
        await page.waitForFunction(() => {
            const panel = document.querySelector(".connection-amount-editor[data-positioned]");
            if (!panel || !panel.closest(".canvas-viewport")) return false;
            const rect = panel.getBoundingClientRect();
            const canvas = panel.closest(".canvas-viewport").getBoundingClientRect();
            return rect.left >= Math.max(0, canvas.left) && rect.right <= Math.min(innerWidth, canvas.right) &&
                rect.top >= Math.max(0, canvas.top) && rect.bottom <= Math.min(innerHeight, canvas.bottom);
        });
    };
    await assertEditorFits();
    await amount.fill("75");
    await badges.filter({ hasText: /^\+100 Hz$/ }).waitFor({ state: "attached" });
    const anchorBeforePan = await amountEditor.getAttribute("data-anchor-x");
    await panCanvas(4000);
    await page.waitForFunction(old => document.querySelector(".connection-amount-editor")?.dataset.anchorX !== old, anchorBeforePan);
    await assertEditorFits();
    assert.equal(await amount.inputValue(), "75", "Panning must preserve the uncommitted draft");
    await panCanvas(-4000);
    await assertEditorFits();
    await amount.press("Escape");
    await amountEditor.waitFor({ state: "detached" });
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await badges.filter({ hasText: /^\+100 Hz$/ }).press("Enter");
    await amount.waitFor();
    assert.equal(await amount.inputValue(), "100", "Escape discards the draft");
    await amount.fill("");
    await amountEditor.getByRole("button", { name: "Apply amount" }).click();
    await amountEditor.getByText("Enter a finite number.").waitFor();
    await amount.fill("25");
    await amountEditor.getByRole("button", { name: "Apply amount" }).click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await predictedFrequency.filter({ hasText: "415–465 Hz" }).waitFor();
    await badges.filter({ hasText: /^\+25 Hz$/ }).waitFor({ state: "attached" });
    // History is a fresh committed revision even while the worklet is running.
    await page.getByRole("button", { name: "Undo", exact: true }).click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await predictedFrequency.filter({ hasText: "340–540 Hz" }).waitFor();
    await badges.filter({ hasText: /^\+100 Hz$/ }).waitFor({ state: "attached" });
    await page.getByRole("button", { name: "Redo", exact: true }).click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await predictedFrequency.filter({ hasText: "415–465 Hz" }).waitFor();
    await badges.filter({ hasText: /^\+25 Hz$/ }).waitFor({ state: "attached" });
    await marker.waitFor({ timeout: 10000 });
    await amountEditor.getByRole("button", { name: "Cancel", exact: true }).click();
    await amountEditor.waitFor({ state: "detached" });
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
    assert.equal(await articles.nth(1).getByRole("spinbutton", { name: "Pulse width (ratio)" }).inputValue(), "0.5");
    assert.equal(await play.isEnabled(), false);
    // Switching modes keeps the frequency connection visible but dormant,
    // then restores the same live input and value when returning to free mode.
    const timing = articles.nth(1).getByLabel("Timing mode");
    await timing.selectOption("1");
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 1}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await wire.count(), 1);
    assert.equal(await page.locator(".connection-dormant").count(), 1);
    assert.match(await articles.nth(1).getByRole("button", { name: "Input: Frequency (Modulation)" }).getAttribute("class"), /port-dormant/);
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
    // With badges hidden, the cable itself must still be selectable. A badge
    // may otherwise cover the only exposed part of a short wire.
    await labelMode.selectOption("Off");
    await badges.first().waitFor({ state: "detached" });
    assert.equal(await status.innerText(), `Playing revision ${initialRevision + 2}`);
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
    await assertEditorFits();
    await page.getByRole("button", { name: "Delete selected" }).click();
    await amountEditor.waitFor({ state: "detached" });
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
    assert.equal(await labelMode.inputValue(), "All", "New nested canvas has its own label preference");
    await labelMode.selectOption("Modified");
    assert.equal(await tempo.inputValue(), "90");
    await transportState.filter({ hasText: "Running" }).waitFor();
    assert.ok(Number(await beat.innerText()) >= Number(frozenBeat));
    assert.equal(await page.locator("#node-type option[value='output.master']").count(), 0);
    assert.equal(await articles.count(), 4);
    await exerciseNote(droneRevision, "Child modulation");
    const outputRack = articles.filter({ has: page.locator(".node-header > span", { hasText: /^Output Rack$/ }) });
    await outputRack.getByText("To parent graph · exposed outputs").waitFor();
    await outputRack.getByRole("button", { name: "Input: Audio (Audio)" }).waitFor();
    await page.getByRole("button", { name: "Locate output rack" }).click();
    await outputRack.and(page.locator(".node-selected")).waitFor();
    assert.match(await outputRack.getAttribute("class"), /node-selected/);
    await page.getByRole("button", { name: "Fit", exact: true }).click();
    const rack = articles.filter({ hasText: "Input Parameters Rack" });
    await rack.getByRole("button", { name: "Output: Pitch (Modulation)" }).waitFor();
    const childFrequency = page.getByRole("spinbutton", { name: "Frequency (Hz)" });
    await page.getByLabel("Predicted modulation range").filter({ hasText: "439–441 Hz" }).waitFor({ timeout: 10000 });
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
    assert.equal(await labelMode.inputValue(), "Off", "Root label preference survives navigation");
    assert.equal(await page.locator(".canvas-world").getAttribute("style"), rootTransform);
    await page.locator(".node-role-composite").getByRole("button", { name: "Open Drone" }).click();
    await childFrequency.waitFor();
    assert.equal(await labelMode.inputValue(), "Modified", "Child label preference survives navigation");
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
    assert.equal(await labelMode.inputValue(), "Modified", "Child label preference survives reload");
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
    assert.equal(await frequencyInput.inputValue(), "333");
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
        await page.getByRole("button", { name: "Add", exact: true }).click();
        // Boundary racks paint above ordinary nodes, so DOM order no longer
        // matches addition order in nested canvases. Identify the new node by ID.
        const added = page.locator("article[data-node-id]" + Object.keys(existing)
            .map(id => `:not([data-node-id='${id}'])`).join(""));
        await added.waitFor();
        assert.equal(await added.count(), 1);
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
    // A custom exposed port can have a much longer label than built-in signals.
    await page.getByRole("button", { name: "Fit", exact: true }).click();
    await rack.getByRole("button", { name: "Edit ports" }).click();
    const longSignal = "Slow atmospheric modulation for the resonant filter in this drone";
    await editor.getByLabel("New port name").fill(longSignal);
    await editor.getByRole("button", { name: "Add port", exact: true }).click();
    await rack.getByRole("button", { name: `Output: ${longSignal} (Modulation)` }).waitFor();
    await editor.getByRole("button", { name: "Close port editor" }).click();
    const nestedPlot = rack.locator(".node-visualization-panel");
    await nestedPlot.getByRole("button", { name: "Show plot" }).click();
    await nestedPlot.getByRole("combobox", { name: "Scope signal" }).selectOption({ label: `Output: ${longSignal}` });
    await nestedPlot.locator("[data-view-mode='waveform']").waitFor();
    await assertCompactPlotFits(nestedPlot);

    // Independent fresh workspace: preview response without starting audio,
    // then verify the same plot consumes accepted modulation telemetry.
    const responsePage = await browser.newPage({ deviceScaleFactor: 2 });
    responsePage.on("pageerror", error => console.error(error.message));
    responsePage.on("console", message => { if (message.type() === "error") console.error(message.text()); });
    await responsePage.goto("http://127.0.0.1:" + server.address().port);
    const responseStatus = responsePage.locator(".playback-toolbar").getByRole("status");
    await responseStatus.filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
    await responsePage.getByRole("button", { name: "Patches", exact: true }).click();
    const responseManager = responsePage.getByRole("region", { name: "Patch manager" });
    await responseManager.getByRole("button", { name: "New patch" }).click();
    await responseManager.getByText("New workspace created.", { exact: true }).waitFor();
    await responseManager.getByRole("button", { name: "Close", exact: true }).click();
    const responseNodes = responsePage.locator("article");
    const master = responseNodes.first();
    const addResponseNode = async type => {
        const count = await responseNodes.count();
        await responsePage.locator("#node-type").selectOption(type);
        await responsePage.getByRole("button", { name: "Add", exact: true }).click();
        const node = responseNodes.nth(count);
        await node.waitFor();
        await responsePage.getByRole("button", { name: "Fit", exact: true }).click();
        return node;
    };
    const responseOsc = await addResponseNode("generator.oscillator");
    const responseFilter = await addResponseNode("processor.filter");
    const responseEq = await addResponseNode("processor.parametric-eq");
    const chooseResponse = async node => {
        const panel = node.locator(".node-visualization-panel");
        await panel.getByRole("button", { name: "Show plot" }).press("Enter");
        await panel.locator(".signal-scope[data-view-mode='waveform']").waitFor();
        await panel.getByRole("combobox", { name: "Analysis view" }).selectOption("response");
        const plot = panel.locator(".processor-response");
        await plot.waitFor();
        assert.equal(await plot.locator(".response-effective").count(), 0);
        assert.equal(await plot.locator("[data-response-rate]").getAttribute("data-response-rate"), "48000");
        assert.match(await plot.locator("[data-response-rate]").innerText(), /Reference/);
        const fits = await plot.evaluate(element => {
            const frame = element.closest("article").getBoundingClientRect();
            return [element, element.parentElement.querySelector("select[aria-label='Analysis view']"), ...element.querySelectorAll("svg, .response-legend, .response-chart")].every(child => {
                const rect = child.getBoundingClientRect();
                return rect.left >= frame.left - 1 && rect.right <= frame.right + 1;
            });
        });
        assert.ok(fits, "Response plot must not expand the node");
        return plot;
    };
    const filterPlot = await chooseResponse(responseFilter);
    const filterCurve = filterPlot.locator(".response-configured");
    const lowPass = await filterCurve.getAttribute("points");
    assert.ok(lowPass.split(" ").length > 100);
    await responseFilter.getByLabel("Filter mode").selectOption("1");
    await responsePage.waitForFunction(old => document.querySelector(".response-configured")?.getAttribute("points") !== old, lowPass);
    const highPass = await filterCurve.getAttribute("points");
    await responseFilter.getByRole("spinbutton", { name: "Cutoff (Hz)" }).fill("2000");
    await responsePage.waitForFunction(old => document.querySelector(".response-configured")?.getAttribute("points") !== old, highPass);
    const configured = await filterCurve.getAttribute("points");
    await responseFilter.getByRole("button", { name: "Expand plot" }).press("Enter");
    const expandedResponse = responsePage.getByRole("region", { name: "Signal scope", exact: true });
    await expandedResponse.locator(".response-configured").waitFor();
    assert.equal(await expandedResponse.locator(".response-configured").getAttribute("points"), configured);
    assert.equal(await filterCurve.count(), 0, "Expanded plot replaces the compact renderer");
    await expandedResponse.getByRole("button", { name: "Close scope" }).click();
    await filterCurve.waitFor();
    const eqPlot = await chooseResponse(responseEq);
    const eqCurve = eqPlot.locator(".response-configured");
    const flat = await eqCurve.getAttribute("points");
    await responseEq.getByRole("spinbutton", { name: "Gain (dB)" }).fill("12");
    await responsePage.waitForFunction(old => Array.from(document.querySelectorAll(".response-configured")).at(-1)?.getAttribute("points") !== old, flat);
    await responseEq.getByRole("button", { name: "Bypass Parametric EQ" }).press("Enter");
    await eqPlot.locator("[data-response-audition]").filter({ hasText: /Bypassed/ }).waitFor();
    await responseEq.getByRole("button", { name: "Bypass Parametric EQ" }).press("Enter");
    assert.match(await responseStatus.innerText(), /Ready/);
    await responseOsc.getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await responseFilter.getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    await responseOsc.getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await responseFilter.getByRole("button", { name: "Input: Cutoff (Modulation)" }).press("Enter");
    await responseFilter.getByRole("button", { name: "Output: Audio (Audio)" }).press("Enter");
    await responseEq.getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    await responseEq.getByRole("button", { name: "Output: Audio (Audio)" }).press("Enter");
    await master.getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    await responsePage.locator(".canvas-count").filter({ hasText: "4 nodes · 4 connections" }).waitFor();
    await responsePage.getByRole("button", { name: "Play patch" }).click();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    const responseRevision = await responseStatus.innerText();
    await filterPlot.locator(".response-effective").waitFor({ timeout: 15000 });
    await filterPlot.locator("[data-response-rate]").filter({ hasText: /Audio context/ }).waitFor();
    assert.equal(await eqPlot.locator(".response-effective").count(), 0, "Unmodulated EQ has no telemetry estimate");
    await responseFilter.getByRole("button", { name: "Expand plot" }).press("Enter");
    await expandedResponse.locator(".response-effective").waitFor({ timeout: 15000 });
    await expandedResponse.getByRole("combobox", { name: "Analysis view" }).selectOption("spectrum");
    await expandedResponse.locator(".signal-scope[data-view-mode='spectrum']").waitFor().catch(async error => {
        console.error("Response to spectrum transition:", await expandedResponse.innerHTML());
        throw error;
    });
    await expandedResponse.getByRole("combobox", { name: "Analysis view" }).selectOption("response");
    await expandedResponse.locator(".response-effective").waitFor({ timeout: 15000 });
    await expandedResponse.getByRole("button", { name: "Close scope" }).click();
    await filterPlot.locator(".response-effective").waitFor();
    assert.equal(await responseStatus.innerText(), responseRevision, "Response controls cannot change playback revision");
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor({ timeout: 10000 });
    await filterPlot.locator(".response-effective").waitFor({ state: "detached" });
    await filterCurve.waitFor();

    // Manual pitch control: shared semitones transpose a whole oscillator bank
    // without changing the base Hz fields or recompiling on live control edits.
    await responsePage.getByRole("button", { name: "Patches", exact: true }).click();
    await responseManager.getByRole("button", { name: "New patch" }).click();
    await responseManager.getByText("New workspace created.", { exact: true }).waitFor();
    await responseManager.getByRole("button", { name: "Close", exact: true }).click();
    await responsePage.locator(".canvas-count").filter({ hasText: "1 nodes · 0 connections" }).waitFor();
    const pitchControl = await addResponseNode("generator.pitch-control");
    const pitchA = await addResponseNode("generator.oscillator");
    const pitchB = await addResponseNode("generator.oscillator");
    const pitchMixer = await addResponseNode("processor.mixer");
    const frequencyA = pitchA.getByRole("spinbutton", { name: "Frequency (Hz)" });
    const frequencyB = pitchB.getByRole("spinbutton", { name: "Frequency (Hz)" });
    await frequencyA.fill("110");
    await frequencyB.fill("165");
    for (const voice of [pitchA, pitchB]) {
        await voice.getByRole("combobox", { name: "Pitch tracking (free)" }).selectOption("1");
        await pitchControl.getByRole("button", { name: "Output: Pitch (Modulation)" }).press("Enter");
        await voice.getByRole("button", { name: "Input: Pitch (Modulation)" }).press("Enter");
        await voice.getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
        await pitchMixer.getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    }
    await pitchMixer.getByRole("button", { name: "Output: Audio (Audio)" }).press("Enter");
    await master.getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    await responsePage.locator(".canvas-count").filter({ hasText: "5 nodes · 5 connections" }).waitFor();
    const transpose = pitchControl.getByRole("spinbutton", { name: "Transpose (st)" });
    const freqSliderA = pitchA.locator(".parameter-slider").filter({ has: responsePage.getByRole("spinbutton", { name: "Frequency (Hz)" }) });
    const freqSliderB = pitchB.locator(".parameter-slider").filter({ has: responsePage.getByRole("spinbutton", { name: "Frequency (Hz)" }) });
    const rangeA = freqSliderA.getByLabel("Predicted modulation range");
    const rangeB = freqSliderB.getByLabel("Predicted modulation range");
    await transpose.fill("12");
    await rangeA.filter({ hasText: "220–220 Hz" }).waitFor();
    await rangeB.filter({ hasText: "330–330 Hz" }).waitFor();
    assert.equal(await frequencyA.inputValue(), "110");
    assert.equal(await frequencyB.inputValue(), "165");
    await responsePage.getByRole("button", { name: "Fit", exact: true }).click();
    await responsePage.getByRole("button", { name: "Play patch" }).click();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    const waitForLiveHz = async (voice, expected) => {
        const nodeId = await voice.getAttribute("data-node-id");
        // Faust's smoothed single-precision controls settle within a few
        // hundredths of a Hertz, not necessarily on the exact decimal string.
        await responsePage.waitForFunction(({ nodeId, expected }) => {
            const node = document.querySelector(`article[data-node-id='${nodeId}']`);
            const slider = [...(node?.querySelectorAll('.parameter-slider') ?? [])].find(element => element.querySelector('label')?.textContent.includes('Frequency'));
            const text = slider?.querySelector('[aria-label="Live effective value"]')?.textContent;
            return Math.abs(parseFloat(text) - expected) < 0.1;
        }, { nodeId, expected }, { timeout: 15000 });
    };
    await waitForLiveHz(pitchA, 220);
    await waitForLiveHz(pitchB, 330);
    await transpose.fill("-12");
    await rangeA.filter({ hasText: "55–55 Hz" }).waitFor();
    await rangeB.filter({ hasText: "82.5–82.5 Hz" }).waitFor();
    await waitForLiveHz(pitchA, 55);
    await waitForLiveHz(pitchB, 82.5);
    assert.match(await responseStatus.innerText(), /^Playing revision \d+$/);
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor();

    // The live keyboard latches pitch across release and does not edit the graph.
    await transpose.fill("0");
    const sequencer = await addResponseNode("generator.note-sequencer");
    for (const voice of [pitchA, pitchB]) {
        await sequencer.getByRole("button", { name: "Output: Pitch (Modulation)" }).press("Enter");
        await voice.getByRole("button", { name: "Input: Pitch (Modulation)" }).press("Enter");
    }
    await responsePage.getByRole("button", { name: "Fit", exact: true }).click();
    await sequencer.locator(".node-header > span").click();
    const performance = responsePage.getByRole("region", { name: "Performance keyboard" });
    await responsePage.bringToFront();
    await responsePage.locator('.performance-keyboard[data-keyboard-ready="true"]').waitFor();
    await performance.getByRole("button", { name: "Arm keyboard" }).click();
    await performance.getByRole("button", { name: "Disarm keyboard" }).waitFor();
    await responsePage.getByRole("button", { name: "Play patch" }).click();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    const keyboardRevision = await responseStatus.innerText();
    await performance.locator('[data-live-pitch-offset="0"]').waitFor();
    assert.equal(await performance.getAttribute("data-armed"), "true", await performance.innerText());
    const middleC = performance.locator('[data-midi-note="60"]');
    assert.equal(await middleC.getAttribute("aria-label"), "Play C4 (A)");
    await middleC.click();
    await performance.locator('[data-live-pitch-offset="-9"]').waitFor();
    await responsePage.locator('.performance-keyboard[data-armed="true"]').waitFor();
    await performance.focus();
    assert.equal(await responsePage.evaluate(() => document.visibilityState), "visible");
    assert.equal(await responsePage.evaluate(() => document.querySelector('[aria-modal="true"], [role="dialog"], .workspace-library-panel, .rack-editor, .connection-amount-editor')?.className ?? null), null, "A modal or editor is blocking performance capture");
    await responsePage.keyboard.press("x");
    await performance.getByText("Octave 5 · reference A4", { exact: true }).waitFor();
    await responsePage.keyboard.down("h");
    await performance.locator('[data-live-pitch-offset="12"]').waitFor();
    await responsePage.keyboard.up("h");
    await waitForLiveHz(pitchA, 220);
    await waitForLiveHz(pitchB, 330);
    await frequencyA.focus();
    await responsePage.keyboard.press("a"); // editing focus must not capture
    assert.equal(await performance.locator('[data-live-pitch-offset]').getAttribute('data-live-pitch-offset'), "12");
    await performance.getByRole("button", { name: "Play C5 (A)" }).click();
    await performance.locator('[data-live-pitch-offset="3"]').waitFor();
    await performance.getByRole("button", { name: "Reset pitch" }).click();
    await performance.locator('[data-live-pitch-offset="0"]').waitFor();
    assert.equal(await responseStatus.innerText(), keyboardRevision);
    await performance.getByRole("button", { name: "Edit pattern", exact: true }).click();
    const pattern = performance.getByRole("region", { name: "Pitch pattern" });
    await pattern.getByRole("combobox", { name: "Step count" }).selectOption("2");
    await pattern.getByRole("combobox", { name: "Timing", exact: true }).selectOption("1");
    await pattern.getByRole("spinbutton", { name: "Step seconds" }).fill("1");
    await pattern.getByRole("spinbutton", { name: "Step seconds" }).press("Tab");
    const stepEntry = pattern.getByRole("checkbox", { name: "Step entry (arm keyboard)" });
    await stepEntry.check();
    await performance.focus();
    await responsePage.keyboard.press("h"); // octave 5, A5; edit instead of live pitch
    await pattern.getByRole("button", { name: "Step 1: A5", exact: true }).waitFor();
    await pattern.locator('[data-step="2"][aria-pressed="true"]').waitFor();
    await stepEntry.uncheck();
    await pattern.getByRole("combobox", { name: "Step note" }).selectOption("70");
    await pattern.getByRole("combobox", { name: "Pattern mode" }).selectOption("1");
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    await responsePage.getByRole("button", { name: "Reset transport", exact: true }).click();
    await pattern.locator('[data-sequence-playhead="1"]').waitFor();
    await performance.locator('[data-live-pitch-offset="12"]').waitFor();
    await pattern.locator('[data-sequence-playhead="2"]').waitFor();
    await performance.locator('[data-live-pitch-offset="0"]').waitFor();
    // A coarser two-step grid maps every onset to step one, independent of
    // runner speed. Recording must not advance graph history until commit.
    const recording = pattern.getByRole("group", { name: "Live recording" });
    await recording.getByRole("combobox", { name: "Recording quantization" }).selectOption("2");
    const takeRevision = await responseStatus.innerText();
    await recording.getByRole("button", { name: "Record take" }).click();
    await recording.locator('[data-recorded-slots="0"]').waitFor();
    await performance.getByRole("button", { name: "Play C5 (A)" }).click();
    await recording.locator('[data-recorded-slots="1"]').waitFor();
    await pattern.getByRole("button", { name: "Step 1: C5", exact: true }).waitFor();
    await pattern.getByRole("button", { name: "Step 2: A4", exact: true }).waitFor();
    assert.equal(await responseStatus.innerText(), takeRevision);
    await recording.getByRole("button", { name: "Commit take" }).click();
    await recording.getByText("Take committed.", { exact: true }).waitFor();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/, hasNotText: takeRevision }).waitFor();
    await responsePage.getByRole("button", { name: "Undo", exact: true }).click();
    await pattern.getByRole("button", { name: "Step 1: A5", exact: true }).waitFor();
    await pattern.getByRole("button", { name: "Step 2: A4", exact: true }).waitFor();
    await responsePage.getByRole("button", { name: "Redo", exact: true }).click();
    await pattern.getByRole("button", { name: "Step 1: C5", exact: true }).waitFor();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor();
    const cancelRevision = await responseStatus.innerText();
    await recording.getByRole("combobox", { name: "Take mode" }).selectOption("1");
    await recording.getByRole("button", { name: "Record take" }).click();
    await recording.locator('[data-recorded-slots="0"]').waitFor();
    await pattern.getByRole("button", { name: "Step 2: Hold", exact: true }).waitFor();
    await recording.getByRole("button", { name: "Cancel take" }).click();
    await pattern.getByRole("button", { name: "Step 2: A4", exact: true }).waitFor();
    assert.equal(await responseStatus.innerText(), cancelRevision);
    await pattern.getByRole("combobox", { name: "Pattern mode" }).selectOption("0");
    await pattern.locator('[data-sequence-playhead="none"]').waitFor();
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor();
    await performance.getByRole("button", { name: "Arm keyboard" }).waitFor();

    // The AD generator uses the normal canvas, preset, range and live-control paths.
    await responsePage.getByRole("button", { name: "Patches", exact: true }).click();
    await responseManager.getByRole("button", { name: "New patch" }).click();
    await responseManager.getByText("New workspace created.", { exact: true }).waitFor();
    await responseManager.getByRole("button", { name: "Close", exact: true }).click();
    await responsePage.locator(".canvas-count").filter({ hasText: "1 nodes · 0 connections" }).waitFor();
    const trigger = await addResponseNode("generator.oscillator");
    const envelope = await addResponseNode("generator.envelope");
    const carrier = await addResponseNode("generator.oscillator");
    await trigger.getByRole("spinbutton", { name: "Frequency (Hz)" }).fill("1");
    await trigger.getByRole("combobox", { name: "Waveform" }).selectOption({ label: "Square" });
    const attack = envelope.getByRole("spinbutton", { name: "Attack (ms)", exact: true });
    const decay = envelope.getByRole("spinbutton", { name: "Decay (ms)", exact: true });
    await envelope.getByRole("combobox", { name: "Preset for Envelope" }).selectOption("envelope-impact-pitch");
    assert.equal(await attack.inputValue(), "1");
    assert.equal(await decay.inputValue(), "120");
    await envelope.getByRole("combobox", { name: "Preset for Envelope" }).selectOption("envelope-impact-amplitude");
    assert.equal(await attack.inputValue(), "8");
    assert.equal(await decay.inputValue(), "500");
    await carrier.getByRole("spinbutton", { name: "Amplitude (normalized)", exact: true }).fill("0");
    await trigger.getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await envelope.getByRole("button", { name: "Input: Signal trigger (Modulation)" }).press("Enter");
    await envelope.getByRole("button", { name: "Output: Envelope (Modulation)" }).press("Enter");
    await carrier.getByRole("button", { name: "Input: Amplitude (Modulation)" }).press("Enter");
    await carrier.getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await master.getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    await responsePage.locator(".canvas-count").filter({ hasText: "4 nodes · 3 connections" }).waitFor();
    const ampSlider = carrier.locator(".parameter-slider").filter({ has: responsePage.getByRole("spinbutton", { name: "Amplitude (normalized)", exact: true }) });
    await ampSlider.getByLabel("Predicted modulation range").filter({ hasText: "0–1" }).waitFor();
    await envelope.locator(".node-visualization-panel").getByRole("button", { name: "Show plot" }).press("Enter");
    await envelope.locator(".signal-scope[data-view-mode='waveform']").waitFor();
    const envelopeView = envelope.getByRole("combobox", { name: "Analysis view" });
    await envelopeView.selectOption("envelope");
    const envelopePreview = envelope.locator(".envelope-preview");
    await envelopePreview.locator('[data-envelope-stage="Attack"]').filter({ hasText: "8 ms" }).waitFor();
    await envelopePreview.locator('[data-envelope-playhead="unavailable"]').waitFor();
    await envelope.getByRole("button", { name: "Expand plot" }).press("Enter");
    const expandedEnvelope = responsePage.getByRole("region", { name: "Signal scope" });
    await expandedEnvelope.locator('.envelope-preview[data-envelope-mode="Ad"]').waitFor();
    await expandedEnvelope.getByRole("button", { name: "Close scope" }).press("Enter");
    await envelopePreview.waitFor();
    await responsePage.getByRole("button", { name: "Fit", exact: true }).click();
    await responsePage.getByRole("button", { name: "Play patch" }).click();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    await ampSlider.getByLabel("Live effective value").waitFor({ timeout: 15000 });
    await envelopePreview.locator('[data-envelope-playhead]:not([data-envelope-playhead="unavailable"])').waitFor({ timeout: 15000 });
    await attack.fill("25");
    await decay.fill("250");
    await envelopePreview.locator('[data-envelope-stage="Decay"]').filter({ hasText: "250 ms" }).waitFor();
    assert.match(await responseStatus.innerText(), /^Playing revision \d+$/);
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor();
    await envelopePreview.locator('[data-envelope-playhead="unavailable"]').waitFor();
    // Explicit rhythmic routing retains the old oscillator-trigger wire but disables its DSP path.
    const edgeAdapter = await addResponseNode("generator.signal-to-trigger");
    await envelope.getByRole("combobox", { name: "Trigger source" }).selectOption("1");
    await envelope.getByRole("button", { name: "Input: Signal trigger (Modulation) — inactive in current mode" }).waitFor();
    await trigger.getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await edgeAdapter.getByRole("button", { name: "Input: Signal (Modulation)" }).press("Enter");
    await edgeAdapter.getByRole("button", { name: "Output: Trigger (Trigger)" }).press("Enter");
    await envelope.getByRole("button", { name: "Input: Trigger (Trigger)", exact: true }).press("Enter");
    await responsePage.locator(".canvas-count").filter({ hasText: "5 nodes · 5 connections" }).waitFor();
    await responsePage.locator(".connection-trigger .connection-line").waitFor({ state: "attached" });
    await responsePage.locator(".connection-modulation.connection-dormant").waitFor({ state: "attached" });
    await responsePage.locator(".connection-amount-badge").filter({ hasText: /^Trigger$/ }).press("Enter");
    const eventEditor = responsePage.getByRole("region", { name: "Connection amount editor" });
    await eventEditor.getByText("Rhythmic event · unity connection. No amount control.").waitFor();
    assert.equal(await eventEditor.getByRole("spinbutton").count(), 0);
    await eventEditor.getByRole("button", { name: "Close", exact: true }).click();
    await responsePage.getByRole("button", { name: "Play patch" }).click();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor();
    await envelope.getByRole("combobox", { name: "Trigger source" }).selectOption("0");
    await responsePage.locator(".connection-trigger.connection-dormant").waitFor({ state: "attached" });
    // Runtime buttons appear on the shared node frame, including the parameterless trigger.
    const manualTrigger = await addResponseNode("generator.manual-trigger");
    const manualGate = await addResponseNode("generator.gate");
    const rhythmClock = await addResponseNode("generator.clock");
    const manualEnvelope = await addResponseNode("generator.envelope");
    await manualEnvelope.getByRole("combobox", { name: "Trigger source" }).selectOption("1");
    await manualTrigger.getByRole("button", { name: "Output: Trigger (Trigger)" }).press("Enter");
    await manualEnvelope.getByRole("button", { name: "Input: Trigger (Trigger)", exact: true }).press("Enter");
    await manualEnvelope.getByRole("button", { name: "Output: Envelope (Modulation)" }).press("Enter");
    await carrier.getByRole("button", { name: "Input: Amplitude (Modulation)" }).press("Enter");
    await envelope.getByRole("combobox", { name: "Trigger source" }).selectOption("2");
    await manualGate.getByRole("button", { name: "Output: Gate (Gate)" }).press("Enter");
    await envelope.getByRole("button", { name: "Input: Gate (Gate)", exact: true }).press("Enter");
    await rhythmClock.getByRole("combobox", { name: "Timing mode" }).selectOption("0");
    await rhythmClock.getByRole("spinbutton", { name: "Rate (Hz)" }).waitFor();
    await manualTrigger.getByRole("button", { name: "Fire trigger" }).press("Enter");
    await manualTrigger.getByRole("status").filter({ hasText: "Connect this source" }).waitFor();
    await responsePage.getByRole("button", { name: "Play patch" }).click();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    const actionRevision = await responseStatus.innerText();
    await manualTrigger.getByRole("button", { name: "Fire trigger" }).press("Enter");
    await manualTrigger.getByRole("status").waitFor({ state: "detached" });
    await manualGate.getByRole("button", { name: "Open gate" }).press("Enter");
    await manualGate.getByRole("button", { name: "Close gate" }).press("Enter");
    assert.equal(await responseStatus.innerText(), actionRevision);
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor();
    await manualGate.getByRole("combobox", { name: "Gate source" }).selectOption("1");
    assert.equal(await manualGate.getByRole("button", { name: "Open gate" }).count(), 0);
    await rhythmClock.getByRole("button", { name: "Output: Clock (Clock)" }).press("Enter");
    await manualGate.getByRole("button", { name: "Input: Trigger (Trigger)", exact: true }).press("Enter");
    // Extend the same saved envelope in place: gated modes use the existing
    // Gate socket and Loop ignores (without deleting) all event routes.
    const envMode = envelope.getByRole("combobox", { name: "Envelope mode" });
    await envMode.selectOption("1");
    await envelopePreview.locator('[data-envelope-stage="Hold"]').filter({ hasText: "until gate closes" }).waitFor();
    await envelope.getByRole("spinbutton", { name: "Release (ms)", exact: true }).waitFor();
    assert.equal(await envelope.getByRole("spinbutton", { name: "Decay (ms)", exact: true }).count(), 0);
    assert.equal(await envelope.getByRole("combobox", { name: "Trigger source" }).count(), 0);
    await envelope.getByRole("button", { name: "Input: Gate (Gate)", exact: true }).waitFor();
    await envelope.getByRole("spinbutton", { name: "Release (ms)", exact: true }).fill("1500");
    await responsePage.getByRole("button", { name: "Play patch" }).click();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    let modeRevision = await responseStatus.innerText();
    await envMode.selectOption("2");
    await envelope.locator('.envelope-preview[data-envelope-mode="Adsr"]').waitFor();
    await envelope.getByRole("spinbutton", { name: "Sustain (normalized)", exact: true }).waitFor();
    assert.equal(await envelope.getByRole("spinbutton", { name: "Release (ms)", exact: true }).inputValue(), "1500");
    await responseStatus.filter({ hasText: /^Playing revision \d+$/, hasNotText: modeRevision }).waitFor({ timeout: 30000 });
    modeRevision = await responseStatus.innerText();
    await envelope.getByRole("spinbutton", { name: "Sustain (normalized)", exact: true }).fill("0.65");
    await responseStatus.filter({ hasText: /^Playing revision \d+$/, hasNotText: modeRevision }).waitFor({ timeout: 30000 });
    modeRevision = await responseStatus.innerText();
    await envMode.selectOption("3");
    await envelope.locator('.envelope-preview[data-envelope-mode="Loop"]').waitFor();
    await envelope.getByRole("spinbutton", { name: "Rest (ms)", exact: true }).waitFor();
    await envelope.getByRole("button", { name: "Input: Gate (Gate) — inactive in current mode" }).waitFor();
    assert.equal(await envelope.getByRole("spinbutton", { name: "Release (ms)", exact: true }).count(), 0);
    await responseStatus.filter({ hasText: /^Playing revision \d+$/, hasNotText: modeRevision }).waitFor({ timeout: 30000 });
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor();
    await envMode.selectOption("0");
    assert.equal(await envelope.getByRole("combobox", { name: "Trigger source" }).inputValue(), "2");
    await envelope.getByRole("button", { name: "Input: Gate (Gate)", exact: true }).waitFor();
    const rhythmComposite = await addResponseNode("composite.graph");
    await rhythmComposite.getByRole("button", { name: "Open Drone" }).press("Enter");
    const rhythmRack = responsePage.locator("article").filter({ hasText: "Input Parameters Rack" });
    await rhythmRack.getByRole("button", { name: "Edit ports" }).click();
    const rhythmEditor = responsePage.getByRole("region", { name: "Edit Input Parameters Rack" });
    await rhythmEditor.getByLabel("New port name").fill("Clock in");
    await rhythmEditor.getByLabel("Receiving socket").selectOption("Sum");
    await rhythmEditor.getByLabel("Signal type").selectOption("Clock");
    assert.equal(await rhythmEditor.getByLabel("Receiving socket").inputValue(), "Single");
    assert.equal(await rhythmEditor.getByLabel("Receiving socket").isDisabled(), true);
    await rhythmEditor.getByRole("button", { name: "Add port", exact: true }).click();
    await rhythmRack.getByRole("button", { name: "Output: Clock in (Clock)" }).waitFor();
    await responsePage.close();
    console.log("Published editor played, edited nested graphs and restored the saved workspace after reload.");
} finally {
    await browser?.close();
    server.close();
}
