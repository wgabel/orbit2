// Exercise the published Blazor app, not just its JavaScript components.
// Reject unversioned interop paths to model a browser holding stale assets.
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, resolve, sep } from "node:path";
import { pathToFileURL } from "node:url";
import { verifyTouchWire } from "./TouchWireSmoke.mjs";
import { verifyWireEdgePan } from "./WireEdgePanSmoke.mjs";
import { fitCanvas, resetCanvas, zoomStep, tripleTapFit, verifyCanvasNavigation } from "./CanvasNavigationSmoke.mjs";
import { verifyCanvasContext } from "./CanvasContextSmoke.mjs";

const { chromium } = await import(pathToFileURL(resolve(process.env.ORBIT_PLAYWRIGHT_DIR, "index.mjs")));
const root = resolve("publish/wwwroot");
const version = process.env.GITHUB_SHA;
assert.ok(version && process.env.ORBIT_CHROME);
const requests = new Set();
const types = { ".js": "text/javascript", ".mjs": "text/javascript", ".html": "text/html", ".css": "text/css", ".json": "application/json", ".wasm": "application/wasm", ".svg": "image/svg+xml" };
const server = createServer(async (request, response) => {
    const url = new URL(request.url, "http://localhost");
    const path = url.pathname;
    requests.add(path);
    try {
        if (["audio", "persistence"].some(directory => path.startsWith(`/_content/Orbit.Infrastructure/${directory}/`)))
            throw new Error("Unversioned browser assets are stale");
        if (path.startsWith("/css/") || path.startsWith("/js/"))
            throw new Error("Unversioned editor assets are stale");
        const file = resolve(root, "." + (path === "/" ? "/index.html" : path));
        if (!file.startsWith(root + sep)) throw new Error("Outside static assets");
        let bytes = await readFile(file);
        // Model a cached entry from the previous release, while the manifest is fresh.
        if (path === "/" && url.searchParams.has("test-stale") && !url.searchParams.has("orbit-build"))
            bytes = Buffer.from(bytes.toString("utf8").replace(`name="orbit-build" content="${version}"`,
                'name="orbit-build" content="' + "0".repeat(40) + '"'));
        response.writeHead(200, { "Content-Type": types[extname(file)] ?? "application/octet-stream" });
        response.end(bytes);
    } catch (error) { response.writeHead(404); response.end(String(error)); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const libraryRow = (page, key) => page.locator(`.node-library-panel [data-node-key="${key}"]`);
const activePanel = page => page.locator(".graph-workspace").getAttribute("data-mobile-active-panel");
async function openWorkspacePanel(page, panel) {
    const trigger = page.locator(`[data-mobile-trigger="${panel}"]`);
    if (await activePanel(page) === panel) return;
    if (panel === "edit" && await activePanel(page) === "selection") await closeWorkspacePanel(page, "selection");
    await trigger.click();
    await page.locator(`.graph-workspace[data-mobile-active-panel="${panel}"]`).waitFor();
}
async function closeWorkspacePanel(page, panel) {
    if (await activePanel(page) !== panel) return;
    await page.locator(`[data-mobile-trigger="${panel === "selection" ? "edit" : panel}"]`).click();
    await page.locator(`.graph-workspace[data-mobile-active-panel="${panel}"]`).waitFor({ state: "detached" });
}
async function clickWorkspaceTool(page, panel, name, close = true) {
    await openWorkspacePanel(page, panel);
    await page.getByRole("button", { name, exact: true }).click();
    if (close) await closeWorkspacePanel(page, panel);
}
const editButton = (page, name) => ({
    click: () => clickWorkspaceTool(page, "edit", name),
    isEnabled: () => page.locator(".workspace-edit-tools button").filter({ hasText: new RegExp(`^${name}$`) }).isEnabled()
});
async function selectView(page, control, value) {
    await openWorkspacePanel(page, "view");
    await control.selectOption(value);
    await closeWorkspacePanel(page, "view");
}
async function openNodeLibrary(page) {
    const library = page.getByRole("region", { name: "Node library", exact: true });
    if (!await library.isVisible()) {
        await openWorkspacePanel(page, "nodes");
    }
    await library.waitFor();
    return library;
}
async function closeNodeLibrary(page) {
    await page.getByRole("button", { name: "Close library", exact: true }).click();
    await page.locator(".node-library-panel").waitFor({ state: "hidden" });
}
async function addNode(page, key) {
    await openNodeLibrary(page);
    await libraryRow(page, key).locator(".node-library-add").click();
    await page.locator(".node-library-panel").waitFor({ state: "hidden" });
}
// Load an authored macro using the real schema-1 export/import path.
async function verifyMacroSliders(browser, exported, mobile) {
    const document = JSON.parse(exported.toString());
    const wrapper = document.rootGraph.nodes.find(node => node.composite?.graph.nodes.some(
        child => child.parameters.some(parameter => parameter.key === "frequency")));
    assert.ok(wrapper, "Expected an exported composite with a frequency target");
    const target = wrapper.composite.graph.nodes.find(node => node.parameters.some(parameter => parameter.key === "frequency"));
    const frequency = target.parameters.find(parameter => parameter.key === "frequency");
    wrapper.composite.macros = [{
        id: "39a8a387-19d2-4420-adf6-4fcbebc7d2c1", name: "Size", defaultValue: 0.5, baseValue: 0.5,
        bindings: [{ target: { nodeId: target.id, parameterId: frequency.id }, atZero: 220, atOne: 440, exponent: 1 }]
    }];
    document.activeGraphId = document.rootGraph.id;
    // Keep the wrapper accessible independently of the source desktop viewport.
    const layout = document.canvasLayouts.find(canvas => canvas.graphId === document.rootGraph.id);
    assert.ok(layout, "Expected exported root layout");
    Object.assign(layout, { panX: 0, panY: 0, zoom: 1 });
    layout.positions = document.rootGraph.nodes.map((node, index) => ({
        nodeId: node.id, x: node.id === wrapper.id ? 24 : 1000 + index * 350, y: 24
    }));
    const page = await browser.newPage(mobile ? { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } : {});
    page.on("pageerror", error => console.error("Macro fixture page error:", error.stack ?? error.message));
    page.on("console", message => { if (message.type() === "error") console.error("Macro fixture console:", message.text()); });
    try {
        await page.goto("http://127.0.0.1:" + server.address().port);
        await page.locator(".playback-toolbar [role=status]").filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
        await clickWorkspaceTool(page, "more", "Patches", false);
        const manager = page.locator(".patch-manager");
        await manager.getByLabel("Import JSON").setInputFiles({
            name: "macro-fixture.json", mimeType: "application/json", buffer: Buffer.from(JSON.stringify(document))
        });
        await manager.getByText("Imported patch.", { exact: true }).waitFor();
        await manager.getByRole("button", { name: "Close" }).click();
        await closeWorkspacePanel(page, "more");
        const node = page.locator(`article[data-node-id="${wrapper.id}"]`);
        const group = node.getByRole("group", { name: "Composite macro controls" });
        const base = group.getByRole("spinbutton");
        const range = group.getByRole("slider", { name: "Size" });
        await base.waitFor();
        assert.equal(await base.inputValue(), "0.5");
        assert.equal(await range.getAttribute("min"), "0");
        assert.equal(await range.getAttribute("max"), "1");
        assert.equal(await group.getByRole("slider").count(), 1);
        assert.equal(await node.locator(".node-expand-toggle").count(), 0);
        const waitBase = value => page.waitForFunction(({ id, value }) =>
            Number(document.querySelector(`article[data-node-id="${id}"] [aria-label="Composite macro controls"] input[type=number]`)?.value) === value,
            { id: wrapper.id, value });
        // Native numeric editing and normal history work on both layouts.
        await base.fill("0.75");
        await page.waitForFunction(id => document.querySelector(`article[data-node-id="${id}"] [aria-label="Composite macro controls"] input[type=range]`)?.value === "0.75", wrapper.id);
        await editButton(page, "Undo").click();
        await waitBase(0.5);
        await editButton(page, "Redo").click();
        await waitBase(0.75);
        if (!mobile) {
            // One held keyboard gesture commits multiple values but one undo entry.
            await range.focus();
            await page.keyboard.down("ArrowRight");
            await waitBase(0.76);
            await page.keyboard.down("ArrowRight");
            await page.keyboard.up("ArrowRight");
            await waitBase(0.77);
            await editButton(page, "Undo").click();
            await waitBase(0.75);
        } else {
            await range.tap({ position: { x: 12, y: 10 } });
            await page.waitForFunction(id => Number(document.querySelector(`article[data-node-id="${id}"] [aria-label="Composite macro controls"] input[type=number]`)?.value) < 0.5, wrapper.id);
            await editButton(page, "Undo").click();
            await waitBase(0.75);
        }
        await node.getByRole("button", { name: `Open ${wrapper.composite.name}`, exact: true }).click();
        await page.waitForFunction(id => document.querySelector(".graph-workspace")?.dataset.activeGraph === id, wrapper.composite.graph.id);
        assert.equal(await page.getByRole("group", { name: "Composite macro controls" }).count(), 0);
        const childNode = page.locator(`article[data-node-id="${target.id}"]`);
        const ownership = childNode.locator(`[data-macro-parameter="${frequency.id}"]`);
        await ownership.waitFor({ state: "attached" });
        assert.match(await ownership.innerText(), /Size/);
        assert.match(await ownership.innerText(), /Mapped: 385 Hz/);
        assert.equal(await ownership.getAttribute("data-macro-active"), "true");
        const manual = ownership.locator("..").getByRole("spinbutton", { name: "Frequency (Hz)", exact: true });
        assert.equal(await manual.count(), 1, "Frequency editor must be distinct from the period editor");
        assert.ok(await manual.isEnabled(), "Mapped parameter keeps its editable manual input");
        assert.ok((await ownership.innerText()).includes(`Manual: ${Number(await manual.inputValue())} Hz`));
        await page.getByRole("navigation", { name: "Canvas path" }).getByRole("button", { name: "Patch", exact: true }).click();
        await waitBase(0.75);

        // Author macros through the actual workspace panel, not just imported JSON.
        const editMacros = node.getByRole("button", { name: "Edit macros", exact: true });
        const editor = page.getByRole("region", { name: "Composite macro editor" });
        await editMacros.click();
        // Missing Razor event imports can emit literal @onclick/@oninput attributes
        // while the editor still looks interactive and the application compiles.
        const rawEvents = await editor.locator("button, input, select").evaluateAll(controls =>
            controls.flatMap(control => control.getAttributeNames().filter(name => /^@on/i.test(name))));
        assert.deepEqual(rawEvents, [], "Macro editor must compile DOM event directives");
        await editor.getByLabel("Macro name").fill("Discarded");
        // Confirm the draft event has reached Blazor before activating Cancel.
        await editor.getByRole("combobox", { name: "Macro", exact: true })
            .locator("option").filter({ hasText: /^Discarded$/ }).waitFor({ state: "attached" });
        await editor.getByRole("button", { name: "Cancel", exact: true }).click();
        try { await editor.waitFor({ state: "detached" }); }
        catch (error) {
            console.error("Macro editor after Cancel:", await editor.innerText().catch(() => "(detached)"));
            throw error;
        }
        await group.getByRole("slider", { name: "Size", exact: true }).waitFor();
        await editMacros.click();
        await editor.getByLabel("Macro name").fill("Body size");
        await editor.getByLabel("Curve exponent").fill("9");
        await editor.getByRole("alert").waitFor();
        assert.equal(await editor.getByRole("button", { name: "Apply macros", exact: true }).isEnabled(), false);
        await editor.getByLabel("Curve exponent").fill("2");
        await editor.locator('output[aria-label="Mapped preview"]').filter({ hasText: /^343\.75 Hz$/ }).waitFor();
        await editor.getByRole("button", { name: "Add macro", exact: true }).click();
        await editor.getByLabel("Macro name").fill("Second control");
        assert.equal(await editor.getByRole("group", { name: "Macro binding", exact: true }).count(), 1);
        assert.notEqual(await editor.getByLabel("Target parameter").inputValue(),
            target.id.replaceAll("-", "") + ":" + frequency.id.replaceAll("-", ""));
        await editor.getByRole("button", { name: "Apply macros", exact: true }).click();
        await editor.waitFor({ state: "detached" });
        await group.getByRole("slider", { name: "Body size", exact: true }).waitFor();
        await group.getByRole("slider", { name: "Second control", exact: true }).waitFor();
        await editButton(page, "Undo").click();
        await group.getByRole("slider", { name: "Size", exact: true }).waitFor();
        assert.equal(await group.getByRole("slider").count(), 1);
        await editButton(page, "Redo").click();
        await group.getByRole("slider", { name: "Second control", exact: true }).waitFor();
        await editMacros.click();
        await editor.getByRole("combobox", { name: "Macro", exact: true }).selectOption({ label: "Second control" });
        await editor.getByRole("button", { name: "Remove macro", exact: true }).click();
        await editor.getByRole("button", { name: "Apply macros", exact: true }).click();
        await editor.waitFor({ state: "detached" });
        await group.getByRole("slider", { name: "Second control", exact: true }).waitFor({ state: "detached" });
        assert.equal(await group.getByRole("slider").count(), 1);
        await node.getByRole("button", { name: `Open ${wrapper.composite.name}`, exact: true }).click();
        await ownership.waitFor({ state: "attached" });
        assert.match(await ownership.innerText(), /Body size/);
        assert.match(await ownership.innerText(), /Mapped: 343\.75 Hz/);
        await page.getByRole("navigation", { name: "Canvas path" }).getByRole("button", { name: "Patch", exact: true }).click();
        await editMacros.click();
        await editor.getByRole("button", { name: "Remove macro", exact: true }).click();
        await editor.getByRole("button", { name: "Apply macros", exact: true }).click();
        await editor.waitFor({ state: "detached" });
        await node.getByRole("button", { name: `Open ${wrapper.composite.name}`, exact: true }).click();
        await childNode.waitFor({ state: "attached" });
        assert.equal(await ownership.count(), 0);
    } finally {
        await page.close();
    }
}
let browser;
try {
    browser = await chromium.launch({ executablePath: process.env.ORBIT_CHROME, headless: true,
        args: ["--no-sandbox", "--mute-audio"] });
    // Exercise device-pixel bitmap resizing independently of CSS plot width.
    const page = await browser.newPage({ deviceScaleFactor: 2 });
    page.on("pageerror", error => console.error(error.message));
    page.on("console", message => { if (message.type() === "error") console.error(message.text()); });
    await page.goto("http://127.0.0.1:" + server.address().port);
    const status = page.locator(".playback-toolbar").getByRole("status");
    const play = page.getByRole("button", { name: "Play patch" });
    const articles = page.locator(".node-frame");
    const frequency = articles.nth(1).locator(".parameter-slider").filter({ has: page.getByRole("spinbutton", { name: "Frequency (Hz)" }) });
    const amplitude = articles.nth(1).locator(".parameter-slider").filter({ has: page.getByRole("spinbutton", { name: "Amplitude (normalized)" }) });
    const marker = frequency.locator(".parameter-effective-marker");
    const predictedFrequency = frequency.getByLabel("Predicted modulation range");
    await predictedFrequency.filter({ hasText: "340–540 Hz" }).waitFor({ timeout: 30000 });
    const build = page.locator(".workspace-build");
    assert.equal(await build.getAttribute("data-build-commit"), version);
    assert.equal(await build.getAttribute("data-build-number"), process.env.GITHUB_RUN_NUMBER);
    assert.equal(await build.innerText(), "b" + process.env.GITHUB_RUN_NUMBER);
    assert.equal(await page.locator('meta[name="orbit-build-number"]').getAttribute("content"), process.env.GITHUB_RUN_NUMBER);
    for (const file of ["tokens.css", "base.css", "components.css", "components/graph-workspace.css", "components/mobile-workspace.css", "components/canvas-context.css"])
        assert.ok(requests.has(`/css-${version}/${file}`), "Missing versioned stylesheet: " + file);
    assert.ok(requests.has("/build-manifest.json"), "Boot must check the deployed build");
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
    assert.equal(await toolbar.locator(".canvas-toolbar, .canvas-zoom").count(), 0);
    await page.getByRole("button", { name: "Nodes", exact: true }).waitFor();
    const closedCanvas = await page.locator(".canvas-viewport").boundingBox();
    const navBounds = await page.locator(".mobile-workspace-bar").boundingBox();
    const transportBounds = await page.locator(".playback-toolbar").boundingBox();
    assert.ok(navBounds.y === transportBounds.y && navBounds.y + navBounds.height <= closedCanvas.y, "Desktop navigation and transport occupy the same top row");
    for (const panel of ["nodes", "outline", "edit", "view", "more", "transport"]) {
        await openWorkspacePanel(page, panel);
        assert.deepEqual(await page.locator(".canvas-viewport").boundingBox(), closedCanvas, `${panel} must not resize the canvas`);
        await closeWorkspacePanel(page, panel);
    }
    await openNodeLibrary(page);
    await toolbar.getByRole("button", { name: "Play patch" }).waitFor();
    assert.equal(await libraryRow(page, "processor.resonator").locator(".node-library-name").textContent(), "Resonator");
    assert.equal(await libraryRow(page, "generator.note-source").locator(".node-library-name").textContent(), "Note Source");
    for (const [key, name] of [["organ", "Organ"], ["flute", "Flute"], ["brass", "Brass"],
        ["bowed-strings", "Bowed strings"], ["didgeridoo", "Didgeridoo"]]) {
        assert.equal(await libraryRow(page, `preset.instrument.${key}`).locator(".node-library-name").textContent(), `Instrument — ${name}`);
        assert.equal(await libraryRow(page, `preset.instrument.playable-${key}`).locator(".node-library-name").textContent(), `Playable — ${name}`);
    }
    // Search, category filters, colors and favorites share the same list.
    const library = page.getByRole("region", { name: "Node library", exact: true });
    await library.getByLabel("Search nodes").fill("echo");
    await libraryRow(page, "processor.delay").waitFor();
    await libraryRow(page, "generator.oscillator").waitFor({ state: "hidden" });
    assert.equal(await library.locator("[data-node-key]").count(), 1);
    await libraryRow(page, "processor.delay").locator(".node-library-favorite").click();
    await libraryRow(page, "processor.delay").locator('.node-library-favorite[aria-pressed="true"]').filter({ hasText: "★" }).waitFor();
    await page.waitForFunction(() => JSON.parse(localStorage.getItem("orbit2.node-library.v1") || "null")?.favorites?.includes("processor.delay"));
    await library.getByLabel("Search nodes").fill("");
    await library.locator(".node-library-results h3").first().filter({ hasText: "Favorites" }).waitFor();
    await library.getByLabel("Category", { exact: true }).selectOption("Audio");
    await libraryRow(page, "processor.delay").waitFor({ state: "hidden" });
    assert.equal(await libraryRow(page, "processor.delay").count(), 0);
    assert.ok(await libraryRow(page, "generator.oscillator").locator("svg.node-category-audio").count());
    await library.getByLabel("Search nodes").fill("no-such-node");
    await library.getByText("No matching nodes.").waitFor();
    await closeNodeLibrary(page);
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
        const undoEnabled = await editButton(page, "Undo").isEnabled();
        await addNode(page, "canvas.note");
        const note = page.locator(".note-card");
        await note.waitFor();
        assert.equal(await articles.count(), nodeCount);
        const dragNote = async (target, dx, dy) => {
            const pointer = { pointerId: 78, pointerType: "mouse", button: 0, buttons: 1, clientX: 100, clientY: 100 };
            await target.dispatchEvent("pointerdown", pointer);
            await viewport.dispatchEvent("pointermove", { ...pointer, clientX: 100 + dx, clientY: 100 + dy });
            await viewport.dispatchEvent("pointerup", { ...pointer, buttons: 0, clientX: 100 + dx, clientY: 100 + dy });
        };
        const originalBounds = await note.getAttribute("style");
        await dragNote(note.locator(".note-header"), 5, 5);
        const movedBounds = await note.getAttribute("style");
        assert.notEqual(movedBounds, originalBounds);
        await clickWorkspaceTool(page, "edit", "Undo annotation");
        assert.equal(await note.getAttribute("style"), originalBounds);
        await dragNote(note.getByRole("button", { name: "Resize note" }), 5, 5);
        assert.notEqual(await note.getAttribute("style"), originalBounds);
        await clickWorkspaceTool(page, "edit", "Undo annotation");
        assert.equal(await note.getAttribute("style"), originalBounds);
        await note.getByRole("button", { name: "Edit", exact: true }).click();
        const editor = page.getByRole("region", { name: "Edit canvas note" });
        const visibility = page.getByLabel("Annotations", { exact: true });
        await editor.waitFor();
        assert.equal(await visibility.isEnabled(), false, "Protect an open annotation draft");
        await editor.getByLabel("Title").fill(title);
        await editor.getByLabel("Body").fill("A minor\nOpen fifth");
        await editor.getByLabel("Accent").selectOption("Idea");
        await editor.getByRole("button", { name: "Apply", exact: true }).click();
        assert.equal(await note.locator(".note-title").innerText(), title);
        assert.equal(await note.locator(".note-body").textContent(), "A minor\nOpen fifth");
        await note.getByRole("button", { name: "Edit", exact: true }).click();
        await editor.getByLabel("Title").fill("Uncommitted");
        await editor.getByLabel("Title").press("Escape");
        assert.equal(await note.locator(".note-title").innerText(), title);
        await clickWorkspaceTool(page, "edit", "Undo annotation");
        assert.equal(await note.locator(".note-title").innerText(), "Note");
        await clickWorkspaceTool(page, "edit", "Redo annotation");
        assert.equal(await note.locator(".note-title").innerText(), title);
        const badgeCount = await viewport.locator(".connection-amount-badge").count();
        await selectView(page, visibility, "TitlesOnly");
        await note.getByRole("button", { name: `Reveal note: ${title}`, exact: true }).waitFor();
        assert.equal(await note.locator(".note-body").count(), 0);
        const compactNote = note.getByRole("button", { name: `Reveal note: ${title}`, exact: true });
        await compactNote.focus();
        assert.equal(await compactNote.evaluate(button => document.activeElement === button), true, "Compact note accepts keyboard focus");
        await compactNote.press("Enter");
        await note.and(page.locator(".note-selected")).waitFor();
        await note.locator(".note-body").waitFor();
        assert.equal(await note.locator(".note-body").textContent(), "A minor\nOpen fifth");
        await selectView(page, visibility, "Hide");
        await note.waitFor({ state: "detached" });
        assert.equal(await viewport.locator(".connection-amount-badge").count(), badgeCount);
        await clickWorkspaceTool(page, "view", "Find annotation", false);
        await viewport.getByRole("region", { name: "Find annotation" }).getByRole("option", { name: `Note: ${title}`, exact: true }).waitFor({ state: "attached" });
        await clickWorkspaceTool(page, "view", "Find annotation", false);
        await selectView(page, visibility, "Show");
        await note.waitFor();
        if (await note.locator(".annotation-marker").count())
            await note.locator(".annotation-marker").press("Enter");
        assert.equal(await visibility.inputValue(), "Show");
        await note.getByTitle("Collapse note").click();
        assert.equal(await note.locator(".note-body").count(), 0);
        await note.getByTitle("Expand note").click();
        await note.getByRole("button", { name: "Duplicate", exact: true }).click();
        assert.equal(await note.count(), 2);
        await clickWorkspaceTool(page, "edit", "Undo annotation");
        assert.equal(await note.count(), 1);
        if (await note.locator(".annotation-marker").count())
            await note.locator(".annotation-marker").press("Enter");
        await note.getByRole("button", { name: "Delete", exact: true }).click();
        assert.equal(await note.count(), 0);
        assert.equal(await editButton(page, "Undo").isEnabled(), undoEnabled);
        assert.equal(await status.innerText(), `Playing revision ${revision}`);
    };
    const exerciseAlias = async (node, revision) => {
        const canonical = await node.locator(".node-alias").innerText();
        await node.locator(".node-display-name").click();
        await clickWorkspaceTool(page, "edit", "Describe selected");
        const editor = page.getByRole("region", { name: "Edit canvas description" });
        await editor.getByLabel("Alias").fill("Harmonic layer");
        await editor.getByLabel("Description").fill("A minor\nOpen fifth");
        await editor.getByRole("button", { name: "Apply", exact: true }).click();
        assert.equal(await node.locator(".node-alias").innerText(), "Harmonic layer");
        assert.equal(await node.locator(".node-canonical-name").innerText(), canonical);
        await node.getByRole("button", { name: "Read node description" }).click();
        assert.equal(await editor.getByLabel("Description").inputValue(), "A minor\nOpen fifth");
        await editor.getByLabel("Alias").fill("Discard me");
        await editor.getByLabel("Alias").press("Escape");
        assert.equal(await node.locator(".node-alias").innerText(), "Harmonic layer");
        await clickWorkspaceTool(page, "edit", "Undo annotation");
        assert.equal(await node.locator(".node-alias").innerText(), canonical);
        await clickWorkspaceTool(page, "edit", "Redo annotation");
        assert.equal(await node.locator(".node-alias").innerText(), "Harmonic layer");
        await clickWorkspaceTool(page, "edit", "Undo annotation");
        assert.equal(await status.innerText(), `Playing revision ${revision}`);
    };
    const exerciseGroup = async (node, revision) => {
        const nodeCount = await articles.count();
        const undoEnabled = await editButton(page, "Undo").isEnabled();
        await node.locator(".node-display-name").click();
        await clickWorkspaceTool(page, "edit", "Create visual group");
        const group = page.locator(".visual-group");
        await group.waitFor();
        assert.equal(await group.locator(".visual-group-count").innerText(), "1 member");
        const frame = await group.getAttribute("style");
        const position = await node.getAttribute("style");
        const drag = async (target) => {
            const pointer = { pointerId: 79, pointerType: "mouse", button: 0, buttons: 1, clientX: 100, clientY: 100 };
            await target.dispatchEvent("pointerdown", pointer);
            await viewport.dispatchEvent("pointermove", { ...pointer, clientX: 108, clientY: 105 });
            await viewport.dispatchEvent("pointerup", { ...pointer, buttons: 0, clientX: 108, clientY: 105 });
        };
        await drag(group.locator(".visual-group-header"));
        assert.notEqual(await group.getAttribute("style"), frame);
        assert.notEqual(await node.getAttribute("style"), position);
        await clickWorkspaceTool(page, "edit", "Undo annotation");
        assert.equal(await group.getAttribute("style"), frame);
        assert.equal(await node.getAttribute("style"), position);
        await drag(group.getByRole("button", { name: "Resize visual group" }));
        assert.notEqual(await group.getAttribute("style"), frame);
        assert.equal(await node.getAttribute("style"), position);
        await clickWorkspaceTool(page, "edit", "Undo annotation");
        await group.getByRole("button", { name: "Edit visual group" }).press("Enter");
        const editor = page.getByRole("region", { name: "Edit visual group" });
        await editor.getByLabel("Title").fill("Harmonic voices");
        await editor.getByLabel("Description").fill("Arrangement layer");
        await editor.getByLabel("Accent").selectOption("Idea");
        await editor.getByRole("button", { name: "Apply", exact: true }).click();
        assert.equal(await group.locator(".visual-group-title").innerText(), "Harmonic voices");
        await group.getByRole("button", { name: "Delete visual group" }).press("Enter");
        assert.equal(await group.count(), 0);
        assert.equal(await articles.count(), nodeCount);
        assert.equal(await editButton(page, "Undo").isEnabled(), undoEnabled);
        assert.equal(await status.innerText(), `Playing revision ${revision}`);
    };
    await viewport.locator(".canvas-count").filter({ hasText: /^4 nodes · 3 connections$/ }).waitFor();
    await viewport.locator(".canvas-patch-value").filter({ hasText: /^4 nodes · 3 connections$/ }).waitFor();
    const canvasBounds = await viewport.boundingBox();
    assert.ok(canvasBounds.height >= page.viewportSize().height * 0.75, "Canvas must fill most of the viewport");
    const countBounds = await viewport.locator(".canvas-count").boundingBox();
    assert.ok(countBounds.x > canvasBounds.x + canvasBounds.width / 2 && countBounds.y < canvasBounds.y + 40,
        "Node and connection count must remain in the upper right of the canvas");
    assert.equal(await marker.count(), 0);
    // Bulk selection does not move the canvas or enter graph history. Keyboard
    // users can toggle a particular eligible node from the shared selection panel.
    const selectionToggle = { click: () => clickWorkspaceTool(page, "edit", "Selection actions", false) };
    const originalSelectionViewport = await viewport.locator(".canvas-world").getAttribute("style");
    const selectionUndo = await editButton(page, "Undo").isEnabled();
    await selectionToggle.click();
    const selectionPanel = viewport.getByRole("region", { name: "Node selection" });
    await selectionPanel.getByRole("button", { name: "Select all module nodes" }).press("Enter");
    await selectionPanel.getByRole("status").filter({ hasText: "3 nodes selected · 3 eligible" }).waitFor();
    assert.equal(await viewport.locator(".node-selected").count(), 3);
    await selectionPanel.getByLabel("Individual module node").selectOption({ index: 1 });
    await selectionPanel.getByRole("button", { name: "Toggle node selection" }).press("Enter");
    await selectionPanel.getByRole("status").filter({ hasText: "2 nodes selected · 2 eligible" }).waitFor();
    await selectionPanel.getByRole("button", { name: "Clear selection" }).click();
    await selectionPanel.getByRole("button", { name: "Marquee mode" }).click();
    await selectionPanel.waitFor({ state: "detached" });
    const targetNode = await articles.nth(1).boundingBox();
    const region = { left: targetNode.x + targetNode.width / 4, top: targetNode.y + targetNode.height / 4,
        right: targetNode.x + targetNode.width * 3 / 4, bottom: targetNode.y + targetNode.height * 3 / 4 };
    const pointer = { pointerId: 87, pointerType: "mouse", button: 0, buttons: 1 };
    await viewport.dispatchEvent("pointerdown", { ...pointer, clientX: region.right, clientY: region.bottom });
    await viewport.locator(".canvas-marquee").waitFor({ state: "attached" });
    await viewport.dispatchEvent("pointermove", { ...pointer, clientX: region.left, clientY: region.top });
    await viewport.dispatchEvent("pointerup", { ...pointer, buttons: 0, clientX: region.left, clientY: region.top });
    await viewport.locator(`.node-selected[data-node-id="${await articles.nth(1).getAttribute("data-node-id")}"]`).waitFor();
    assert.equal(await viewport.locator(".canvas-world").getAttribute("style"), originalSelectionViewport);
    assert.equal(await editButton(page, "Undo").isEnabled(), selectionUndo);
    await viewport.press("Escape"); // leave marquee mode; selection remains until explicitly cleared
    await viewport.press("Escape");
    assert.equal(await viewport.locator(".node-selected").count(), 0);
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
    const undoButton = editButton(page, "Undo");
    const redoButton = editButton(page, "Redo");
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
    const transport = page.locator(".transport-controls");
    const tempo = transport.getByLabel("Tempo (BPM)");
    const beat = transport.getByTestId("transport-beat");
    const transportState = transport.getByTestId("transport-state");
    const applyTempo = transport.getByRole("button", { name: "Apply tempo" });
    assert.equal(await tempo.inputValue(), "120");
    await openWorkspacePanel(page, "transport");
    await tempo.fill("0");
    await applyTempo.click();
    await transport.getByText("Enter a tempo from 20 to 300 BPM.").waitFor();
    await openWorkspacePanel(page, "transport");
    await tempo.fill("60");
    await applyTempo.click();
    await transport.getByText("Synchronized rates: 1/4 note = 1 Hz · 1 bar (4/4) = 0.25 Hz").waitFor();
    assert.equal(await beat.innerText(), "0");
    assert.equal(await transport.getByTestId("transport-sample-rate").count(), 0);
    const performanceToggle = page.getByRole("button", { name: /^Performance ·/ });
    await openWorkspacePanel(page, "more");
    assert.equal(await performanceToggle.innerText(), "Performance · Off");
    await performanceToggle.click();
    const performancePanel = page.getByRole("region", { name: "Audio performance diagnostics" });
    await performancePanel.locator("[data-performance-session]").filter({ hasText: "idle" }).waitFor({ timeout: 10000 });
    assert.match(await performancePanel.innerText(), /not-created/);
    await performancePanel.locator("[data-performance-editor-fps]").filter({ hasText: /[0-9]+(?:[.,][0-9]+)? FPS/ }).waitFor({ timeout: 15000 });
    await performancePanel.locator("[data-performance-editor-history]").filter({ hasText: /[1-9][0-9]* \/ 120/ }).waitFor({ timeout: 10000 });
    assert.match(await performanceToggle.innerText(), /UI [0-9]+ FPS/);
    await performancePanel.getByRole("button", { name: "Close" }).click();
    assert.equal(await performanceToggle.innerText(), "Performance · Off");
    await closeWorkspacePanel(page, "more");
    await play.click({ timeout: 30000 });
    await status.filter({ hasText: /Playing revision/ }).waitFor({ timeout: 30000 });
    let initialRevision = Number((await status.innerText()).match(/Playing revision (\d+)/)[1]);
    await openWorkspacePanel(page, "more");
    await performanceToggle.click();
    await performancePanel.locator("[data-performance-session]").filter({ hasText: "active" }).waitFor({ timeout: 10000 });
    assert.match(await performancePanel.innerText(), /\d+ Hz/);
    await performancePanel.locator("[data-performance-worklets]").filter({ hasText: "1 / 0" }).waitFor({ timeout: 10000 });
    await performancePanel.locator("[data-performance-source-builds]").filter({ hasText: /^0$/ }).waitFor({ timeout: 10000 });
    assert.doesNotMatch(await performancePanel.innerText(), /Compiler worker round trip/);
    await performancePanel.getByRole("button", { name: "Reset session counters" }).click();
    await performancePanel.getByRole("button", { name: "Reset session counters" }).waitFor({ state: "visible" });
    await page.waitForFunction(() => !document.querySelector("#audio-performance-details button:last-child")?.disabled);
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await performancePanel.getByRole("button", { name: "Close" }).click();
    await closeWorkspacePanel(page, "more");
    await exerciseNote(initialRevision, "Root harmony");
    await exerciseAlias(articles.nth(1), initialRevision);
    await exerciseGroup(articles.nth(1), initialRevision);
    // Patch-wide metadata is a committed presentation edit, even during playback.
    await clickWorkspaceTool(page, "more", "Patches", false);
    const liveManager = page.getByRole("region", { name: "Patch manager" });
    const undoBeforeOverview = await editButton(page, "Undo").isEnabled();
    await liveManager.getByRole("button", { name: "Edit patch overview" }).click();
    const overviewEditor = liveManager.getByRole("region", { name: "Edit patch overview" });
    await overviewEditor.getByLabel("Title", { exact: true }).fill("Distant horizon");
    await overviewEditor.getByLabel("Description", { exact: true }).fill("Dark evolving space");
    await overviewEditor.getByLabel("Usage instructions").fill("Change the timbre slowly.");
    await overviewEditor.getByLabel("Tags (comma-separated)").fill("Ambient, space, ambient");
    await overviewEditor.getByRole("button", { name: "Apply", exact: true }).click();
    await liveManager.getByRole("region", { name: "Current patch overview" }).getByText("Distant horizon", { exact: true }).waitFor();
    await liveManager.getByRole("button", { name: "Edit patch overview" }).click();
    await overviewEditor.getByLabel("Title", { exact: true }).fill("Discard this draft");
    await overviewEditor.getByLabel("Title", { exact: true }).press("Escape");
    await liveManager.getByRole("region", { name: "Current patch overview" }).getByText("Distant horizon", { exact: true }).waitFor();
    await liveManager.getByRole("button", { name: "Close", exact: true }).click();
    await closeWorkspacePanel(page, "more");
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    assert.equal(await editButton(page, "Undo").isEnabled(), undoBeforeOverview);
    await transportState.filter({ hasText: "Running" }).waitFor({ state: "attached" });
    await transport.getByTestId("transport-sample-rate").waitFor({ state: "attached" });
    const firstBeat = Number(await beat.innerText());
    await page.waitForFunction(previous => Number(document.querySelector('[data-testid="transport-beat"]').textContent) > previous, firstBeat);
    await openWorkspacePanel(page, "transport");
    await tempo.fill("90");
    await applyTempo.click();
    await transport.getByText("Synchronized rates: 1/4 note = 1.5 Hz · 1 bar (4/4) = 0.375 Hz").waitFor();
    await closeWorkspacePanel(page, "transport");
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
    await fitCanvas(page);
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
    await fitCanvas(page);
    // Use the direct rail without selecting the cable or opening the inspector.
    // The focusable wire remains the entry point for exact inspector edits.
    const wire = page.locator(".connection-modulation .connection-hit");
    const labelMode = page.locator("#connection-label-mode");
    const badges = page.locator(".connection-amount-badge");
    assert.equal(await labelMode.inputValue(), "All");
    const amountLabel = value => badges.filter({ has: page.locator(`input[aria-valuetext="${value}"]`) });
    const rail = amountLabel("+100 Hz").getByRole("slider");
    // All four visibility modes are presentation-only. Verify the default
    // modulation edge (100 Hz) remains in Modified while unity audio edges do not.
    const allRailCount = await badges.count();
    assert.equal(allRailCount, 3);
    await selectView(page, labelMode, "Modified");
    await amountLabel("+100 Hz").waitFor();
    assert.equal(await badges.count(), 1);
    await selectView(page, labelMode, "Selected");
    assert.equal(await badges.count(), 0);
    await selectView(page, labelMode, "Off");
    assert.equal(await badges.count(), 0);
    assert.equal(await wire.count(), 1);
    await selectView(page, labelMode, "All");
    await rail.waitFor();
    assert.equal(await badges.count(), allRailCount);
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);

    // Verify the exact cubic midpoint rather than the path-length midpoint,
    // and screen-space size independently of the current canvas zoom.
    const railGeometry = () => amountLabel("+100 Hz").evaluate(badge => {
        const path = badge.closest(".canvas-world").querySelector(
            `.connection-hit[data-connection-id="${badge.dataset.connectionId}"]`);
        const coordinates = path.getAttribute("d").match(/[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?/g).map(Number);
        if (coordinates.length !== 12) throw new Error("Expected M L C L cable geometry");
        const x = (coordinates[2] + 3 * coordinates[4] + 3 * coordinates[6] + coordinates[8]) / 8;
        const y = (coordinates[3] + 3 * coordinates[5] + 3 * coordinates[7] + coordinates[9]) / 8;
        const midpoint = new DOMPoint(x, y).matrixTransform(path.getScreenCTM());
        const rect = badge.getBoundingClientRect();
        return { width: rect.width, height: rect.height,
            error: Math.hypot(rect.left + rect.width / 2 - midpoint.x, rect.top + rect.height / 2 - midpoint.y) };
    });
    const firstGeometry = await railGeometry();
    assert.ok(firstGeometry.error < 2, JSON.stringify(firstGeometry));
    await zoomStep(page, -1);
    const zoomedGeometry = await railGeometry();
    assert.ok(zoomedGeometry.error < 2, JSON.stringify(zoomedGeometry));
    assert.ok(Math.abs(zoomedGeometry.width - firstGeometry.width) < 2, "Rail width must remain screen-sized");
    assert.ok(Math.abs(zoomedGeometry.height - firstGeometry.height) < 2, "Rail height must remain screen-sized");
    await fitCanvas(page);

    // Keep diagnostics enabled across the complete draft and release, rather
    // than sampling from different monitoring lifetimes. Keyboard focus can
    // exercise the real native range while the diagnostic panel is open.
    await openWorkspacePanel(page, "more");
    await performanceToggle.click();
    const sourceBuilds = performancePanel.locator("[data-performance-source-builds]");
    const replacements = performancePanel.getByText("Accepted replacements", { exact: true }).locator("xpath=following-sibling::dd[1]");
    await sourceBuilds.filter({ hasText: /^0$/ }).waitFor({ timeout: 10000 });
    await replacements.filter({ hasText: /^0$/ }).waitFor({ timeout: 10000 });
    await rail.focus();
    await page.keyboard.down("ArrowRight");
    await page.keyboard.down("ArrowRight");
    await page.locator('.connection-amount-slider input[aria-valuetext="+104 Hz"]').waitFor();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    assert.equal(await page.locator(".connection-amount-editor").count(), 0);
    // Allow at least one diagnostics publication while the draft is held.
    await page.waitForTimeout(1200);
    assert.equal(await sourceBuilds.innerText(), "0");
    assert.equal(await replacements.innerText(), "0");
    await page.keyboard.press("Escape");
    await page.keyboard.up("ArrowRight");
    await rail.waitFor();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await rail.press("ArrowRight");
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await amountLabel("+102 Hz").waitFor();
    await sourceBuilds.filter({ hasText: /^1$/ }).waitFor({ timeout: 10000 });
    await replacements.filter({ hasText: /^1$/ }).waitFor({ timeout: 10000 });
    await performancePanel.getByRole("button", { name: "Close" }).click();
    await closeWorkspacePanel(page, "more");
    await editButton(page, "Undo").click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await rail.waitFor();
    // Exact entry is local, unquantized, and uses the same history boundary.
    const directId = await amountLabel("+100 Hz").getAttribute("data-connection-id");
    const direct = page.locator(`.connection-amount-slider[data-connection-id="${directId}"]`);
    const exactButton = direct.getByRole("button", { name: "Edit exact connection amount" });
    const exact = direct.getByRole("textbox", { name: "Exact connection amount" });
    await exactButton.press("Enter");
    await exact.waitFor();
    await exact.fill("-123.456789");
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await exact.press("Enter");
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await exactButton.press("Enter");
    assert.equal(await exact.inputValue(), "-123.456789");
    await exact.fill("NaN");
    await exact.press("Enter");
    await direct.getByRole("alert").waitFor();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await exact.press("Escape");
    await exactButton.waitFor();
    await exactButton.press("Enter");
    await exact.fill("101.23456789");
    await wire.focus(); // valid blur commits exactly once
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await editButton(page, "Undo").click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await editButton(page, "Undo").click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await rail.waitFor();
    await wire.press("Enter");
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
    // Authored label edits are independent of both the amount draft and graph history.
    await amountEditor.getByRole("button", { name: "Edit connection label" }).click();
    const descriptionEditor = page.getByRole("region", { name: "Edit canvas description" });
    await descriptionEditor.getByLabel("Connection label").fill("Brightness sweep");
    await descriptionEditor.getByRole("button", { name: "Apply", exact: true }).click();
    await amountEditor.getByText("Brightness sweep", { exact: true }).waitFor();
    assert.equal(await amount.inputValue(), "75");
    // Fit may enter the automatic compact annotation band. At full zoom the
    // authored tooltip is visible, and titles-only suppresses it without
    // changing the numeric badge, selected cable, or pending amount draft.
    await resetCanvas(page);
    const labeledBadge = amountLabel("+100 Hz");
    await labeledBadge.and(page.locator('[title*="Brightness sweep"]')).waitFor();
    const annotationDisplay = viewport.getByLabel("Annotations", { exact: true });
    await selectView(page, annotationDisplay, "TitlesOnly");
    await labeledBadge.and(page.locator(':not([title*="Brightness sweep"])')).waitFor();
    assert.equal(await amount.inputValue(), "75");
    assert.equal(await amountEditor.getByText("Brightness sweep", { exact: true }).count(), 1);
    await selectView(page, annotationDisplay, "Show");
    await labeledBadge.and(page.locator('[title*="Brightness sweep"]')).waitFor();
    await fitCanvas(page);
    await clickWorkspaceTool(page, "edit", "Undo annotation");
    assert.equal(await amountEditor.getByText("Brightness sweep", { exact: true }).count(), 0);
    assert.equal(await amount.inputValue(), "75");
    assert.equal(await status.innerText(), `Playing revision ${initialRevision}`);
    await amountLabel("+100 Hz").waitFor({ state: "attached" });
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
    await wire.press("Enter");
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
    await amountLabel("+25 Hz").waitFor({ state: "attached" });
    // History is a fresh committed revision even while the worklet is running.
    await editButton(page, "Undo").click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await predictedFrequency.filter({ hasText: "340–540 Hz" }).waitFor();
    await amountLabel("+100 Hz").waitFor({ state: "attached" });
    await editButton(page, "Redo").click();
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision}$`) }).waitFor({ timeout: 15000 });
    await predictedFrequency.filter({ hasText: "415–465 Hz" }).waitFor();
    await amountLabel("+25 Hz").waitFor({ state: "attached" });
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
    await openWorkspacePanel(page, "transport");
    await tempo.fill("120");
    await applyTempo.click();
    await synchronizedFrequency.filter({ hasText: /^2 Hz$/ }).waitFor();
    assert.equal(await status.innerText(), `Playing revision ${initialRevision + 1}`);
    await openWorkspacePanel(page, "transport");
    await tempo.fill("90");
    await applyTempo.click();
    await synchronizedFrequency.filter({ hasText: /^1\.5 Hz$/ }).waitFor();
    await closeWorkspacePanel(page, "transport");
    await timing.selectOption("0");
    initialRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 1}$`) }).waitFor({ timeout: 15000 });
    assert.equal(await page.locator(".connection-dormant").count(), 0);
    assert.equal(await wire.count(), 1);
    assert.equal(await articles.nth(1).getByRole("spinbutton", { name: "Frequency (Hz)" }).inputValue(), "330");
    assert.equal(await synchronizedFrequency.count(), 0);
    await marker.waitFor({ timeout: 10000 });
    // Unconnected nodes do not require a new worklet.
    await addNode(page, "generator.oscillator");
    await status.filter({ hasText: new RegExp(`^Playing revision ${initialRevision + 2}$`) }).waitFor({ timeout: 10000 });
    // With badges hidden, the cable itself must still be selectable. A badge
    // may otherwise cover the only exposed part of a short wire.
    await selectView(page, labelMode, "Off");
    await badges.first().waitFor({ state: "detached" });
    assert.equal(await status.innerText(), `Playing revision ${initialRevision + 2}`);
    // Wires run underneath node frames; viewport-aware placement may put a
    // frame over the midpoint without hiding other parts of the cable.
    // Use actual hit testing, not the bounding-box center of a curved path.
    // Round to pixel coordinates because touchscreen.tap uses integer points.
    const exposedWirePoint = target => target.evaluate(path => {
        const length = path.getTotalLength();
        const matrix = path.getScreenCTM();
        if (!matrix) return null;
        for (let i = 0; i <= 100; i++) {
            const fraction = 0.5 + (i % 2 ? -1 : 1) * Math.ceil(i / 2) / 100;
            const point = path.getPointAtLength(length * fraction).matrixTransform(matrix);
            const x = Math.round(point.x), y = Math.round(point.y);
            if (document.elementFromPoint(x, y) === path) return { x, y };
        }
        return null;
    });
    const selectVisibleWire = async target => {
        await target.scrollIntoViewIfNeeded();
        const point = await exposedWirePoint(target);
        assert.ok(point, "Expected an exposed part of the cable to select");
        await page.mouse.click(point.x, point.y);
        await target.locator("xpath=..").and(page.locator(".connection-selected")).waitFor({ state: "attached", timeout: 4000 });
    };
    // Remove the original LFO-to-frequency connection, then connect to amplitude.
    await selectVisibleWire(wire);
    await assertEditorFits();
    await clickWorkspaceTool(page, "edit", "Delete selected");
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
    await transportState.filter({ hasText: "Stopped" }).waitFor({ state: "attached" });
    const frozenBeat = await beat.innerText();
    await page.waitForTimeout(300);
    assert.equal(await beat.innerText(), frozenBeat);
    assert.equal(await amplitude.locator(".parameter-effective-marker").count(), 0);
    assert.equal(await amplitude.locator(".parameter-band").count(), 1);

    // Create an editable drone and connect its public sockets using the same
    // graph editor. Only the root has a master output.
    await addNode(page, "composite.graph");
    const drone = page.locator(".node-role-composite");
    await drone.waitFor();
    await fitCanvas(page);
    const masterWire = page.locator(".connection-audio .connection-hit").nth(1);
    await selectVisibleWire(masterWire);
    await clickWorkspaceTool(page, "edit", "Delete selected");
    await articles.nth(0).getByRole("button", { name: "Output: Signal (Audio)" }).press("Enter");
    await drone.getByRole("button", { name: "Input: Pitch (Modulation)" }).press("Enter");
    await drone.getByRole("button", { name: "Output: Audio (Audio)" }).press("Enter");
    await articles.nth(3).getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    assert.equal(await page.getByRole("alert").count(), 0, await page.getByRole("alert").allTextContents());
    const rootTransform = await page.locator(".canvas-world").getAttribute("style");
    await play.click();
    await status.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    let droneRevision = Number((await status.innerText()).match(/Playing revision (\d+)/)[1]);
    const wholePatchCount = `${await articles.count() + 4} nodes · ${await viewport.locator(".connection-hit").count() + 4} connections`;
    assert.equal(await viewport.locator(".canvas-patch-value").innerText(), wholePatchCount);
    await page.getByRole("button", { name: "Outline", exact: true }).click();
    const playingOutline = page.getByRole("region", { name: "Patch outline" });
    // Razor event directives must compile to Blazor callbacks, not literal DOM attributes.
    assert.equal(await playingOutline.locator("button").evaluateAll(buttons =>
        buttons.some(button => button.hasAttribute("@onclick"))), false, "Outline buttons must have compiled event handlers");
    await playingOutline.getByLabel("Search patch").fill("Drone");
    await playingOutline.getByLabel("Filter outline").selectOption("Composite");
    await playingOutline.getByRole("button", { name: "Open canvas: Drone", exact: true }).click();
    assert.equal(await page.getByRole("alert").count(), 0, await page.getByRole("alert").allTextContents());
    await playingOutline.waitFor({ state: "detached" });
    const breadcrumbs = page.getByRole("navigation", { name: "Canvas path" });
    await breadcrumbs.getByText("Drone", { exact: true }).waitFor();
    assert.equal(await labelMode.inputValue(), "All", "New nested canvas has its own label preference");
    // Exercise a real child edge via its graph-local slider, then restore it
    // before the existing nested graph assertions. A root edge must not change.
    const childRail = page.locator('.connection-amount-slider input[aria-valuetext="+1 Hz"]');
    await childRail.waitFor();
    const childEdgeId = await childRail.locator("xpath=../..").getAttribute("data-connection-id");
    const childAmount = page.locator(`.connection-amount-slider[data-connection-id="${childEdgeId}"]`);
    await childAmount.getByRole("button", { name: "Edit exact connection amount" }).press("Enter");
    const childExact = childAmount.getByRole("textbox", { name: "Exact connection amount" });
    assert.equal(await childExact.inputValue(), "1");
    await childExact.fill("0.375");
    assert.equal(await status.innerText(), `Playing revision ${droneRevision}`);
    await childExact.press("Enter");
    droneRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${droneRevision}$`) }).waitFor({ timeout: 15000 });
    await childAmount.locator('input[aria-valuetext="+0.375 Hz"]').waitFor();
    await editButton(page, "Undo").click();
    droneRevision++;
    await status.filter({ hasText: new RegExp(`^Playing revision ${droneRevision}$`) }).waitFor({ timeout: 15000 });
    await childRail.waitFor();
    await selectView(page, labelMode, "Modified");
    assert.equal(await tempo.inputValue(), "90");
    await transportState.filter({ hasText: "Running" }).waitFor({ state: "attached" });
    assert.ok(Number(await beat.innerText()) >= Number(frozenBeat));
    await openNodeLibrary(page);
    assert.equal(await libraryRow(page, "output.master").count(), 0);
    await closeNodeLibrary(page);
    assert.equal(await articles.count(), 4);
    assert.equal(await viewport.locator(".canvas-count").innerText(), "4 nodes · 4 connections");
    assert.equal(await viewport.locator(".canvas-patch-value").innerText(), wholePatchCount);
    await exerciseNote(droneRevision, "Child modulation");
    await exerciseAlias(articles.nth(1), droneRevision);
    await exerciseGroup(articles.nth(1), droneRevision);
    const outputRack = articles.filter({ has: page.locator(".node-header > span", { hasText: /^Output Rack$/ }) });
    await outputRack.getByText("To parent graph · exposed outputs").waitFor();
    await outputRack.getByRole("button", { name: "Input: Audio (Audio)" }).waitFor();
    await clickWorkspaceTool(page, "view", "Locate output rack");
    await outputRack.and(page.locator(".node-selected")).waitFor();
    assert.match(await outputRack.getAttribute("class"), /node-selected/);
    await fitCanvas(page);
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
    await zoomStep(page, -1);
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
    await clickWorkspaceTool(page, "transport", "Reset transport");
    await beat.filter({ hasText: /^0$/ }).waitFor({ state: "attached" });
    assert.equal(await tempo.inputValue(), "90");
    // Any oscillator can be a synchronized modulation source.
    const newNodeIndex = await articles.count();
    await addNode(page, "generator.oscillator");
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
    await transportState.filter({ hasText: "Running" }).waitFor({ state: "attached" });
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
    await zoomStep(page, -1);
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
    assert.equal(savedWorkspace.overview.title, "Distant horizon");
    assert.ok(savedWorkspace.rootGraph.nodes.some(node => node.composite?.graph.id === expectedGraph));
    await page.reload();
    await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
    assert.equal(await editButton(page, "Undo").isEnabled(), false);
    assert.equal(await editButton(page, "Redo").isEnabled(), false);
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
    await clickWorkspaceTool(page, "more", "Patches", false);
    const manager = page.locator(".patch-manager");
    // The overview region also contains "Saved patch" in its accessible name.
    const savedPatchPicker = manager.getByRole("combobox", { name: "Saved patch", exact: true });
    await manager.waitFor().catch(async error => {
        console.error("Patch menu header:", await page.locator(".workspace-header").innerHTML());
        throw error;
    });
    await manager.getByLabel("Patch name").fill("Drone study");
    await manager.getByRole("button", { name: "Save as" }).click();
    await manager.getByText("Saved Drone study.", { exact: true }).waitFor();
    await manager.getByRole("region", { name: "Saved patch overview" }).getByText("Distant horizon", { exact: true }).waitFor();
    const downloadPromise = page.waitForEvent("download");
    await manager.getByRole("button", { name: "Export JSON" }).click();
    const download = await downloadPromise;
    const exported = await readFile(await download.path());
    assert.equal(JSON.parse(exported.toString()).beatsPerMinute, 90);
    assert.deepEqual(JSON.parse(exported.toString()).overview.tags, ["Ambient", "space"]);
    assert.equal(JSON.parse(exported.toString()).overview.title, "Distant horizon");
    await verifyMacroSliders(browser, exported, false);
    await verifyMacroSliders(browser, exported, true);
    await manager.getByRole("button", { name: "New patch" }).click();
    await manager.getByText("New workspace created.", { exact: true }).waitFor();
    await manager.getByRole("region", { name: "Current patch overview" }).getByText("Untitled patch", { exact: true }).waitFor();
    await page.locator(".canvas-count").filter({ hasText: /^1 nodes · 0 connections$/ }).waitFor();
    await manager.getByLabel("Patch name").fill("Empty study");
    await manager.getByRole("button", { name: "Save as" }).click();
    await manager.getByText("Saved Empty study.", { exact: true }).waitFor();
    await manager.getByLabel("Search library").fill("HORIZON evolving");
    await savedPatchPicker.locator("option[value='Empty study']").waitFor({ state: "detached" });
    await manager.getByLabel("Tag", { exact: true }).selectOption("Ambient");
    await savedPatchPicker.locator("option[value='Drone study']").waitFor({ state: "attached" });
    await manager.getByLabel("Search library").fill("no-match");
    await manager.getByText("No matching patches.", { exact: true }).waitFor();
    await manager.getByRole("button", { name: "Clear filters" }).click();
    await savedPatchPicker.selectOption("Drone study");
    await manager.getByRole("button", { name: "Open patch" }).click();
    await manager.getByText("Opened Drone study.", { exact: true }).waitFor();
    await manager.getByRole("region", { name: "Current patch overview" }).getByText("Distant horizon", { exact: true }).waitFor();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    await status.filter({ hasText: /Ready/ }).waitFor();
    assert.equal(await tempo.inputValue(), "90");
    await manager.getByLabel("Import JSON").setInputFiles({ name: "bad.json", mimeType: "application/json", buffer: Buffer.from("{}") });
    await manager.getByRole("alert").waitFor();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    await savedPatchPicker.selectOption("Empty study");
    await manager.getByRole("button", { name: "Open patch" }).click();
    await manager.getByText("Opened Empty study.", { exact: true }).waitFor();
    await manager.getByLabel("Import JSON").setInputFiles({ name: "exported.json", mimeType: "application/json", buffer: exported });
    await manager.getByText("Imported patch.", { exact: true }).waitFor();
    await manager.getByRole("region", { name: "Current patch overview" }).getByText("Distant horizon", { exact: true }).waitFor();
    await drone.getByRole("button", { name: "Input: Cutoff (Modulation)" }).waitFor();
    await manager.getByRole("button", { name: "Delete saved patch" }).click();
    await manager.getByText("Saved patch deleted; current workspace unchanged.").waitFor();
    assert.equal(await savedPatchPicker.locator("option[value='Empty study']").count(), 0);
    // Opening and importing other patches does not remove the independent custom library.
    await manager.getByRole("button", { name: "Close" }).click();
    await closeWorkspacePanel(page, "more");
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
        await zoomStep(page, 1);
        const transform = await world.getAttribute("style");
        const existing = await articles.evaluateAll(nodes => Object.fromEntries(nodes.map(node => [node.dataset.nodeId, node.getAttribute("style")])));
        await addNode(page, type);
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
    await viewport.locator(".canvas-count").filter({ hasText: /^4 nodes · 4 connections$/ }).waitFor();
    await addInPannedView("generator.noise", 3200);
    // A custom exposed port can have a much longer label than built-in signals.
    await fitCanvas(page);
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
    await clickWorkspaceTool(responsePage, "more", "Patches", false);
    const responseManager = responsePage.getByRole("region", { name: "Patch manager" });
    await responseManager.getByRole("button", { name: "New patch" }).click();
    await responseManager.getByText("New workspace created.", { exact: true }).waitFor();
    await responseManager.getByRole("button", { name: "Close", exact: true }).click();
    await closeWorkspacePanel(responsePage, "more");
    const responseNodes = responsePage.locator("article");
    const master = responseNodes.first();
    const addResponseNode = async type => {
        const count = await responseNodes.count();
        await addNode(responsePage, type);
        const node = responseNodes.nth(count);
        await node.waitFor();
        await fitCanvas(responsePage);
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
    await clickWorkspaceTool(responsePage, "more", "Patches", false);
    await responseManager.getByRole("button", { name: "New patch" }).click();
    await responseManager.getByText("New workspace created.", { exact: true }).waitFor();
    await responseManager.getByRole("button", { name: "Close", exact: true }).click();
    await closeWorkspacePanel(responsePage, "more");
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
    await fitCanvas(responsePage);
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
    await fitCanvas(responsePage);
    await sequencer.locator(".node-header > span").click();
    const performance = responsePage.getByRole("region", { name: "Performance keyboard" });
    await responsePage.bringToFront();
    await responsePage.locator('.performance-keyboard[data-keyboard-ready="true"]').waitFor();
    await performance.getByRole("button", { name: "Collapse keyboard" }).waitFor();
    assert.equal(await performance.getAttribute("data-target-node"), await sequencer.getAttribute("data-node-id"));
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
    const pattern = performance.getByRole("region", { name: "Melody editor" });
    await pattern.getByRole("combobox", { name: "Pattern length" }).selectOption("2");
    await pattern.getByRole("button", { name: "Settings", exact: true }).click();
    await pattern.getByRole("combobox", { name: "Timing", exact: true }).selectOption("1");
    await pattern.getByRole("spinbutton", { name: "Step seconds" }).fill("1");
    await pattern.getByRole("spinbutton", { name: "Step seconds" }).press("Tab");
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    await performance.focus();
    await responsePage.keyboard.press("h"); // octave 5, A5; edit instead of live pitch
    const steps = pattern.locator(".pitch-step-strip");
    await steps.getByRole("button", { name: "Select step 1: A5", exact: true }).waitFor();
    await steps.locator('[data-step="2"][aria-pressed="true"]').waitFor();
    await pattern.getByRole("combobox", { name: "Selected step note" }).selectOption("70");
    await pattern.getByRole("combobox", { name: "Sequencer playback type" }).selectOption("1");
    const noteLength = pattern.getByRole("spinbutton", { name: "Selected note length" });
    await noteLength.fill("0.25");
    await noteLength.press("Tab");
    await steps.getByRole("button", { name: "Select step 1: A5", exact: true }).click();
    assert.equal(await noteLength.inputValue(), "0");
    await steps.getByRole("button", { name: "Select step 2: A4", exact: true }).click();
    assert.equal(await noteLength.inputValue(), "0.25");
    const noteVelocity = pattern.getByRole("combobox", { name: "Selected note velocity" });
    assert.equal(await noteVelocity.inputValue(), "-1");
    await noteVelocity.selectOption("0.73");
    await steps.getByRole("button", { name: "Select step 1: A5", exact: true }).click();
    assert.equal(await noteVelocity.inputValue(), "-1");
    await noteVelocity.selectOption("0");
    await editButton(responsePage, "Undo").click();
    assert.equal(await noteVelocity.inputValue(), "-1");
    await editButton(responsePage, "Redo").click();
    assert.equal(await noteVelocity.inputValue(), "0");
    await steps.getByRole("button", { name: "Select step 2: A4", exact: true }).click();
    assert.equal(await noteVelocity.inputValue(), "0.73");
    assert.equal(await noteLength.inputValue(), "0.25");
    const noteTiming = pattern.getByRole("combobox", { name: "Selected note timing offset" });
    const timingReadout = pattern.getByLabel("Selected note timing in milliseconds");
    assert.equal(await noteTiming.inputValue(), "0");
    assert.equal(await timingReadout.innerText(), "0 ms · on grid");
    await noteTiming.selectOption("-0.2");
    assert.equal(await timingReadout.innerText(), "-200 ms · early");
    await steps.getByRole("button", { name: "Select step 1: A5", exact: true }).click();
    assert.equal(await noteTiming.inputValue(), "0");
    await noteTiming.selectOption("0.15");
    assert.equal(await timingReadout.innerText(), "+150 ms · late");
    await editButton(responsePage, "Undo").click();
    assert.equal(await noteTiming.inputValue(), "0");
    await editButton(responsePage, "Redo").click();
    assert.equal(await noteTiming.inputValue(), "0.15");
    await steps.getByRole("button", { name: "Select step 2: A4", exact: true }).click();
    assert.equal(await noteTiming.inputValue(), "-0.2");
    // Free step duration changes the displayed milliseconds, not the stored fraction.
    await pattern.getByRole("button", { name: "Settings", exact: true }).click();
    await pattern.getByRole("spinbutton", { name: "Step seconds" }).fill("0.5");
    await pattern.getByRole("spinbutton", { name: "Step seconds" }).press("Tab");
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    assert.equal(await noteTiming.inputValue(), "-0.2");
    assert.equal(await timingReadout.innerText(), "-100 ms · early");
    await pattern.getByRole("button", { name: "Settings", exact: true }).click();
    await pattern.getByRole("spinbutton", { name: "Step seconds" }).fill("1");
    await pattern.getByRole("spinbutton", { name: "Step seconds" }).press("Tab");
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    // Humanize uses one atomic edit of the chosen slots, without changing notes.
    await pattern.getByRole("button", { name: "Humanize", exact: true }).click();
    const humanize = pattern.getByRole("region", { name: "Note humanization" });
    await humanize.getByLabel("Humanization timing profile").selectOption("Behind");
    await humanize.getByLabel("Humanization timing amount").selectOption("10");
    await humanize.getByLabel("Humanization velocity amount").selectOption("0");
    await humanize.getByLabel("Humanization scope").selectOption("range");
    await humanize.getByLabel("Humanization first step").selectOption("2");
    assert.equal(await humanize.getByRole("button", { name: "Apply humanization" }).isEnabled(), false);
    await humanize.getByLabel("Humanization last step").selectOption("2");
    await humanize.getByLabel("Humanization seed").fill("77");
    await humanize.getByLabel("Humanization seed").press("Tab");
    await humanize.getByRole("button", { name: "Apply humanization" }).click();
    await humanize.getByText("Humanization applied. Undo restores the previous values.", { exact: true }).waitFor();
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    const humanizedOffset = Number(await noteTiming.inputValue());
    assert.ok(humanizedOffset > -.2 && humanizedOffset <= -.1);
    assert.equal(await noteVelocity.inputValue(), "0.73");
    await steps.getByRole("button", { name: "Select step 1: A5", exact: true }).click();
    assert.equal(await noteTiming.inputValue(), "0.15");
    await steps.getByRole("button", { name: "Select step 2: A4", exact: true }).click();
    await editButton(responsePage, "Undo").click();
    assert.equal(await noteTiming.inputValue(), "-0.2");
    await editButton(responsePage, "Redo").click();
    assert.equal(Number(await noteTiming.inputValue()), humanizedOffset);
    await editButton(responsePage, "Undo").click();
    const velocityDesktopViewport = responsePage.viewportSize();
    await responsePage.setViewportSize({width:390,height:844});
    await pattern.getByRole("button", { name: "Humanize", exact: true }).click();
    await humanize.getByLabel("Humanization scope").selectOption("all");
    await humanize.getByLabel("Humanization timing amount").selectOption("0");
    assert.equal(await humanize.getByRole("button", { name: "Apply humanization" }).isEnabled(), false);
    await humanize.getByLabel("Humanization timing amount").selectOption("10");
    await humanize.getByRole("button", { name: "Apply humanization" }).click();
    await editButton(responsePage, "Undo").click();
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    assert.equal(await noteTiming.inputValue(), "-0.2");
    await noteVelocity.selectOption("-1");
    assert.equal(await noteVelocity.inputValue(), "-1");
    await noteVelocity.selectOption("0.73");
    await noteTiming.selectOption("0.45");
    assert.equal(await timingReadout.innerText(), "+450 ms · late");
    await noteTiming.selectOption("-0.2");
    await responsePage.setViewportSize(velocityDesktopViewport);
    await pattern.getByRole("combobox", { name: "Selected step note" }).selectOption("129");
    await noteVelocity.waitFor({state:"hidden"});
    await noteTiming.waitFor({state:"hidden"});
    await pattern.getByRole("combobox", { name: "Selected step note" }).selectOption("0");
    await noteTiming.waitFor({state:"hidden"});
    await pattern.getByRole("combobox", { name: "Selected step note" }).selectOption("70");
    await noteVelocity.waitFor();
    assert.equal(await noteVelocity.inputValue(), "0.73"); // retained slot override
    assert.equal(await noteTiming.inputValue(), "-0.2");
    await pattern.getByRole("combobox", { name: "Sequencer playback type" }).selectOption("0");
    await noteVelocity.waitFor({state:"hidden"});
    await noteTiming.waitFor({state:"hidden"});
    await pattern.getByRole("combobox", { name: "Pattern playback" }).selectOption("1");
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    await clickWorkspaceTool(responsePage, "transport", "Reset transport");
    await pattern.locator('[data-sequence-playhead="1"]').waitFor();
    await performance.locator('[data-live-pitch-offset="12"]').waitFor();
    await pattern.locator('[data-sequence-playhead="2"]').waitFor();
    await performance.locator('[data-live-pitch-offset="0"]').waitFor();
    // Quantized live capture lives in its own Record tab, without editing
    // committed steps before Commit take. The two-step coarse grid maps every
    // onset to step one independent of runner timing.
    await pattern.getByRole("button", { name: "Record", exact: true }).click();
    const recording = pattern.locator(".pitch-record-view");
    await recording.getByRole("combobox", { name: "Recording quantization" }).selectOption("2");
    const takeRevision = await responseStatus.innerText();
    await recording.getByRole("button", { name: "Record take" }).click();
    await recording.locator('[data-recorded-slots="0"]').waitFor();
    await performance.getByRole("button", { name: "Play C5 (A)" }).click();
    await recording.locator('[data-recorded-slots="1"]').waitFor();
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    await steps.getByRole("button", { name: "Select step 1: C5", exact: true }).waitFor();
    await steps.getByRole("button", { name: "Select step 2: A4", exact: true }).waitFor();
    assert.equal(await responseStatus.innerText(), takeRevision);
    await pattern.getByRole("button", { name: "Record", exact: true }).click();
    await recording.getByRole("button", { name: "Commit take" }).click();
    await recording.getByText("Take committed.", { exact: true }).waitFor();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/, hasNotText: takeRevision }).waitFor();
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    await editButton(responsePage, "Undo").click();
    await steps.getByRole("button", { name: "Select step 1: A5", exact: true }).waitFor();
    await steps.getByRole("button", { name: "Select step 2: A4", exact: true }).waitFor();
    await editButton(responsePage, "Redo").click();
    await steps.getByRole("button", { name: "Select step 1: C5", exact: true }).waitFor();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor();
    const cancelRevision = await responseStatus.innerText();
    await pattern.getByRole("button", { name: "Record", exact: true }).click();
    await recording.getByRole("combobox", { name: "Take mode" }).selectOption("1");
    await recording.getByRole("button", { name: "Record take" }).click();
    await recording.locator('[data-recorded-slots="0"]').waitFor();
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    await steps.getByRole("button", { name: "Select step 2: Hold", exact: true }).waitFor();
    await pattern.getByRole("button", { name: "Record", exact: true }).click();
    await recording.getByRole("button", { name: "Cancel take" }).click();
    await pattern.getByRole("button", { name: "Edit notes", exact: true }).click();
    await steps.getByRole("button", { name: "Select step 2: A4", exact: true }).waitFor();
    assert.equal(await responseStatus.innerText(), cancelRevision);
    await pattern.getByRole("combobox", { name: "Pattern playback" }).selectOption("0");
    await pattern.locator('[data-sequence-playhead="none"]').waitFor();
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor();
    await performance.getByRole("button", { name: "Collapse keyboard" }).waitFor();

    // The held-note keyboard can play the real modal composite. Overlapping keys
    // follow last-note priority; fallback and release do not edit the graph.
    await clickWorkspaceTool(responsePage, "more", "Patches", false);
    await responseManager.getByRole("button", { name: "New patch" }).click();
    await responseManager.getByText("New workspace created.", { exact: true }).waitFor();
    await responseManager.getByRole("button", { name: "Close", exact: true }).click();
    await closeWorkspacePanel(responsePage, "more");
    await responsePage.locator(".canvas-count").filter({ hasText: "1 nodes · 0 connections" }).waitFor();
    const noteSource = await addResponseNode("generator.note-source");
    await openNodeLibrary(responsePage);
    for (const [key, name] of [["handpan", "Handpan"], ["bell", "Bell"], ["gong", "Gong"], ["wood", "Wood percussion"]])
        assert.equal(await libraryRow(responsePage, `preset.modal.playable-${key}`).locator(".node-library-name").textContent(), `Playable — ${name}`);
    await closeNodeLibrary(responsePage);
    const handpan = await addResponseNode("preset.modal.playable-handpan");
    await noteSource.getByRole("button", { name: "Output: Gate (Gate)" }).waitFor();
    await noteSource.getByRole("button", { name: "Output: Velocity (Modulation)" }).waitFor();
    await noteSource.getByRole("button", { name: "Output: Pitch (Modulation)" }).press("Enter");
    await handpan.getByRole("button", { name: "Input: Pitch (st) (Modulation)" }).press("Enter");
    await noteSource.getByRole("button", { name: "Output: Trigger (Trigger)" }).press("Enter");
    await handpan.getByRole("button", { name: "Input: Trigger (Trigger)" }).press("Enter");
    await noteSource.getByRole("button", { name: "Output: Velocity (Modulation)" }).press("Enter");
    await handpan.getByRole("button", { name: "Input: Velocity (Modulation)" }).press("Enter");
    await handpan.getByRole("button", { name: "Output: Audio (Audio)" }).press("Enter");
    await master.getByRole("button", { name: "Input: Audio (Audio)" }).press("Enter");
    await responsePage.locator(".canvas-count").filter({ hasText: "3 nodes · 4 connections" }).waitFor();
    await fitCanvas(responsePage);
    await noteSource.locator(".node-header > span").click();
    await responsePage.locator('.performance-keyboard[data-keyboard-ready="true"]').waitFor();
    await performance.getByRole("button", { name: "Collapse keyboard" }).waitFor();
    assert.equal(await performance.getAttribute("data-target-node"), await noteSource.getAttribute("data-node-id"));
    await responsePage.getByRole("button", { name: "Play patch" }).click();
    await responseStatus.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 30000 });
    const noteRevision = await responseStatus.innerText();
    // Regression: F must play the Note Source even when the canvas itself is focused.
    // It must not trigger the old Fit shortcut or change the viewport.
    const canvasViewport = responsePage.locator(".canvas-viewport");
    const fitSequenceBeforeF = await canvasViewport.getAttribute("data-fit-sequence");
    await canvasViewport.press("f");
    await performance.locator('[data-live-pitch-offset="-4"]').waitFor();
    assert.equal(await canvasViewport.getAttribute("data-fit-sequence"), fitSequenceBeforeF,
        "Armed piano F must not trigger canvas Fit");
    // Ordinary node selection must not discard the keyboard target.
    // Fit uses triple-click while F is reserved for musical input.
    await fitCanvas(responsePage);
    await handpan.locator(".node-header > span").click();
    assert.equal(await performance.getAttribute("data-target-node"), await noteSource.getAttribute("data-node-id"));
    await performance.focus();
    await responsePage.keyboard.down("f");
    await performance.locator('[data-live-pitch-offset="-4"]').waitFor();
    await responsePage.keyboard.up("f");
    await responsePage.keyboard.down("a");
    await performance.locator('[data-live-pitch-offset="-9"]').waitFor();
    await responsePage.keyboard.down("s");
    await performance.locator('[data-live-pitch-offset="-7"]').waitFor();
    await responsePage.keyboard.up("s");
    await performance.locator('[data-live-pitch-offset="-9"]').waitFor();
    await responsePage.keyboard.up("a");
    assert.equal(await responseStatus.innerText(), noteRevision);

    // The mobile live offset used to wrap at +10 st and move every key
    // under the finger. Compare every key's bounds across real note actions.
    await responsePage.keyboard.press("x");
    await performance.getByText("Octave 5 · reference A4", { exact: true }).waitFor();
    const desktopKeyboardViewport = responsePage.viewportSize();
    const readPianoBounds = () => performance.locator("[data-midi-note]:visible").evaluateAll(keys =>
        keys.map(key => {
            const { x, y, width, height } = key.getBoundingClientRect();
            return { note: key.dataset.midiNote, x, y, width, height };
        }));
    try {
        for (const width of [360, 390, 430]) {
            await responsePage.setViewportSize({ width, height: 844 });
            await responsePage.locator(".mobile-workspace-bar").waitFor();
            let baseline;
            for (const [note, offset] of [[72, 3], [79, 10], [72, 3], [81, 12]]) {
                const key = performance.locator(`[data-midi-note="${note}"]`);
                const pointer = { pointerId: 1, pointerType: "touch", isPrimary: true, button: 0, bubbles: true };
                await key.dispatchEvent("pointerdown", { ...pointer, buttons: 1 });
                await performance.locator(`[data-live-pitch-offset="${offset}"]`).waitFor();
                const bounds = await readPianoBounds();
                assert.equal(bounds.length, 13);
                if (baseline) assert.deepEqual(bounds, baseline, `Piano shifted at ${width}px, pitch +${offset} st`);
                else baseline = bounds;
                const dock = await performance.boundingBox();
                const nav = await responsePage.locator(".mobile-workspace-bar").boundingBox();
                const canvas = await responsePage.locator(".graph-canvas").boundingBox();
                assert.ok(dock.y + dock.height <= nav.y + 1, "Dock must not occlude mobile navigation");
                assert.ok(canvas.y + canvas.height <= dock.y + 1, "Dock must reserve canvas space");
                await key.dispatchEvent("pointerup", { ...pointer, buttons: 0 });
                await performance.locator(`[data-midi-note="${note}"][data-held="false"]`).waitFor();
            }
        }
        const mobilePiano = performance.getByRole("group", { name: "On-screen piano" });
        const compact = await mobilePiano.boundingBox();
        const whiteC = performance.locator('[data-midi-note="72"]');
        const blackCs = performance.locator('[data-midi-note="73"]');
        const whiteD = performance.locator('[data-midi-note="74"]');
        const cBounds = await whiteC.boundingBox(), csBounds = await blackCs.boundingBox(), dBounds = await whiteD.boundingBox();
        assert.ok(csBounds.height < cBounds.height && csBounds.y === cBounds.y && dBounds.y === cBounds.y, "Piano geometry must overlay short black keys on the white row");
        assert.ok(csBounds.x < dBounds.x && csBounds.x + csBounds.width > dBounds.x, "Black key must straddle C and D");
        // Capture a touch, slide to another key, and release outside the keyboard.
        const slidePointer = { pointerId: 7, pointerType: "touch", isPrimary: true, button: 0, bubbles: true };
        await whiteC.dispatchEvent("pointerdown", { ...slidePointer, buttons: 1 });
        await performance.locator('[data-live-pitch-offset="3"]').waitFor();
        await responsePage.locator("body").dispatchEvent("pointermove", { ...slidePointer, buttons: 1,
            clientX: dBounds.x + dBounds.width / 2, clientY: dBounds.y + dBounds.height * 0.9 });
        await performance.locator('[data-live-pitch-offset="5"]').waitFor();
        await responsePage.locator("body").dispatchEvent("pointerup", { ...slidePointer, buttons: 0, clientX: 1, clientY: 1 });
        await performance.locator('[data-midi-note="74"][data-held="false"]').waitFor();
        await performance.getByRole("button", { name: "Expand keyboard" }).click();
        assert.ok((await mobilePiano.boundingBox()).height > compact.height);
        await performance.getByRole("button", { name: "Compact keyboard" }).click();
        await performance.getByRole("button", { name: "Collapse keyboard" }).click();
        await mobilePiano.waitFor({ state: "detached" });
        assert.equal(await performance.getAttribute("data-armed"), "false");
        assert.equal(await performance.getAttribute("data-target-node"), await noteSource.getAttribute("data-node-id"));
        await performance.getByRole("button", { name: "Show keyboard" }).click();
        await mobilePiano.waitFor();
    } finally {
        await responsePage.setViewportSize(desktopKeyboardViewport);
    }
    assert.equal(await responseStatus.innerText(), noteRevision);
    await performance.getByRole("button", { name: "Release all notes" }).click();
    // Navigate inside the played instrument; the root note source stays targeted.
    const noteTargetId = await noteSource.getAttribute("data-node-id");
    await fitCanvas(responsePage);
    await handpan.getByRole("button", { name: /^Open / }).click();
    assert.equal(await performance.getAttribute("data-target-node"), noteTargetId);
    await performance.getByRole("button", { name: "Play C5 (A)" }).click();
    await performance.locator('[data-live-pitch-offset="3"]').waitFor();
    await responsePage.getByRole("navigation", { name: "Canvas path" }).getByRole("button", { name: "Patch", exact: true }).click();
    assert.equal(await performance.getAttribute("data-target-node"), await noteSource.getAttribute("data-node-id"));
    await responsePage.getByRole("button", { name: "Stop", exact: true }).click();
    await responseStatus.filter({ hasText: /Ready/ }).waitFor();
    const otherNoteSource = await addResponseNode("generator.note-source");
    await fitCanvas(responsePage);
    await otherNoteSource.locator(".node-header > span").click();
    await responsePage.waitForFunction(id => document.querySelector(".performance-keyboard")?.dataset.targetNode === id, await otherNoteSource.getAttribute("data-node-id"));

    // The AD generator uses the normal canvas, preset, range and live-control paths.
    await clickWorkspaceTool(responsePage, "more", "Patches", false);
    await responseManager.getByRole("button", { name: "New patch" }).click();
    await responseManager.getByText("New workspace created.", { exact: true }).waitFor();
    await responseManager.getByRole("button", { name: "Close", exact: true }).click();
    await closeWorkspacePanel(responsePage, "more");
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
    await fitCanvas(responsePage);
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
    // Instrument templates use the same add-node and editable nested canvas path.
    const instrumentPage = await browser.newPage();
    await instrumentPage.goto("http://127.0.0.1:" + server.address().port);
    await addNode(instrumentPage, "preset.instrument.flute");
    const flute = instrumentPage.locator(".node-role-composite").filter({ has: instrumentPage.getByRole("button", { name: "Open Flute" }) });
    await flute.getByRole("button", { name: "Input: Pitch (st) (Modulation)" }).waitFor();
    await instrumentPage.getByRole("button", { name: "Outline", exact: true }).click();
    const instrumentOutline = instrumentPage.getByRole("region", { name: "Patch outline" });
    await instrumentOutline.getByLabel("Search patch").fill("Flute Output Rack");
    await instrumentOutline.getByLabel("Filter outline").selectOption("Node");
    await instrumentOutline.getByRole("button", { name: "Locate node: Output Rack", exact: true }).click();
    await instrumentOutline.waitFor({ state: "detached" });
    await instrumentPage.locator(".node-selected").filter({ hasText: "Output Rack" }).waitFor();
    await instrumentPage.waitForFunction(() => {
        const rack = document.querySelector(".node-selected");
        const viewport = document.querySelector(".canvas-viewport");
        if (!rack || !viewport) return false;
        const rect = rack.getBoundingClientRect(), canvas = viewport.getBoundingClientRect();
        return rect.left >= canvas.left && rect.right <= canvas.right && rect.top >= canvas.top && rect.bottom <= canvas.bottom;
    });
    await instrumentPage.getByRole("button", { name: "Output: Pitch (st) (Modulation)" }).waitFor();
    assert.equal(await instrumentPage.getByRole("combobox", { name: "Pitch tracking (free)" }).count(), 3);
    await instrumentPage.close();
    // Exercise automatic detail and temporary reveal independently of the main playback viewport.
    const annotationPage = await browser.newPage();
    await annotationPage.goto("http://127.0.0.1:" + server.address().port);
    const annotationViewport = annotationPage.locator(".canvas-viewport");
    await resetCanvas(annotationPage);
    await addNode(annotationPage, "canvas.note");
    await annotationPage.locator(".note-card").waitFor();
    await annotationPage.locator(".note-card").getByRole("button", { name: "Edit", exact: true }).click();
    const outlineDraft = annotationPage.getByRole("region", { name: "Edit canvas note" });
    await outlineDraft.waitFor();
    await annotationPage.getByRole("button", { name: "Outline", exact: true }).click();
    const draftOutline = annotationPage.getByRole("region", { name: "Patch outline" });
    await draftOutline.getByLabel("Filter outline").selectOption("Note");
    assert.equal(await draftOutline.getByRole("button", { name: "Locate note: Note", exact: true }).isDisabled(), true);
    await draftOutline.getByRole("button", { name: "Close outline", exact: true }).click();
    await outlineDraft.getByRole("button", { name: "Cancel", exact: true }).click();
    await clickWorkspaceTool(annotationPage, "edit", "Create visual group");
    await annotationPage.locator(".visual-group").waitFor();
    const annotationMode = annotationViewport.getByLabel("Annotations", { exact: true });
    await selectView(annotationPage, annotationMode, "TitlesOnly");
    await annotationViewport.getByRole("button", { name: "Reveal note: Note", exact: true }).waitFor();
    await annotationViewport.getByRole("button", { name: "Reveal group: Visual group", exact: true }).waitFor();
    assert.equal(await annotationViewport.locator(".annotation-marker").count(), 2);
    await annotationViewport.getByRole("button", { name: "Reveal group: Visual group", exact: true }).press("Space");
    await annotationViewport.locator(".visual-group-selected .visual-group-header").waitFor();
    await annotationViewport.press("Escape");
    await annotationViewport.getByRole("button", { name: "Reveal group: Visual group", exact: true }).waitFor();
    await selectView(annotationPage, annotationMode, "Show");
    for (let i = 1; i <= 6; i++) {
        await zoomStep(annotationPage, -1);
    }
    const markerButton = annotationViewport.getByRole("button", { name: "Reveal note: Note", exact: true });
    await markerButton.waitFor();
    assert.ok(Math.abs((await markerButton.boundingBox()).height - 30) < 2, "Compact marker should remain screen readable");
    await selectView(annotationPage, annotationMode, "Hide");
    await annotationViewport.locator(".note-card, .visual-group").first().waitFor({ state: "detached" });
    assert.equal(await annotationViewport.locator(".note-card, .visual-group").count(), 0);
    await clickWorkspaceTool(annotationPage, "view", "Find annotation", false);
    const finder = annotationViewport.getByRole("region", { name: "Find annotation" });
    await finder.getByLabel("Note or group").selectOption({ label: "Group: Visual group" });
    await finder.getByRole("button", { name: "Reveal and fit" }).click();
    await annotationViewport.locator(".visual-group").waitFor();
    assert.equal(await annotationMode.inputValue(), "Hide");
    assert.equal(await annotationViewport.locator(".note-card").count(), 0);
    // An outline can locate hidden notes even when the canvas has no visible note marker.
    await annotationPage.getByRole("button", { name: "Outline", exact: true }).click();
    const annotationOutline = annotationPage.getByRole("region", { name: "Patch outline" });
    await annotationOutline.getByLabel("Filter outline").selectOption("Note");
    await annotationOutline.getByRole("button", { name: "Locate note: Note", exact: true }).click();
    await annotationOutline.waitFor({ state: "detached" });
    await annotationViewport.locator(".note-selected .note-body").waitFor();
    assert.equal(await annotationMode.inputValue(), "Hide");
    await annotationViewport.press("Escape");
    await annotationViewport.locator(".note-card").waitFor({ state: "detached" });
    await clickWorkspaceTool(annotationPage, "view", "Fit signals", false);
    assert.equal(await annotationMode.inputValue(), "Hide");
    await annotationPage.close();
    // Group the default demo's eligible nodes, inspect the child, undo while
    // inside it, then redo and reopen the same composite.
    const groupPage = await browser.newPage();
    await groupPage.goto("http://127.0.0.1:" + server.address().port);
    const groupViewport = groupPage.locator(".canvas-viewport");
    await clickWorkspaceTool(groupPage, "edit", "Selection actions", false);
    const groupPanel = groupViewport.getByRole("region", { name: "Node selection" });
    await groupPanel.getByRole("button", { name: "Select all module nodes" }).click();
    await groupPanel.getByRole("status").filter({ hasText: "3 nodes selected · 3 eligible" }).waitFor();
    await groupPanel.getByLabel("Composite name").fill("Grouped demo");
    await groupPanel.getByRole("button", { name: "Create composite", exact: true }).click();
    const openGroup = groupViewport.getByRole("button", { name: "Open Grouped demo", exact: true });
    await openGroup.waitFor();
    assert.equal(await groupViewport.locator("article[data-node-id]").count(), 2);
    await openGroup.click();
    await groupViewport.getByRole("button", { name: "Edit ports" }).first().waitFor();
    assert.equal(await groupViewport.locator("article[data-node-id]").count(), 5);
    await editButton(groupPage, "Undo").click();
    await groupViewport.locator("article[data-node-id]").nth(3).waitFor();
    assert.equal(await groupViewport.locator("article[data-node-id]").count(), 4);
    await editButton(groupPage, "Redo").click();
    await openGroup.waitFor();
    await openGroup.click();
    await groupViewport.getByRole("button", { name: "Edit ports" }).first().waitFor();
    assert.equal(await groupViewport.locator("article[data-node-id]").count(), 5);
    // Expand from the parent selection rather than undoing the original grouping.
    await groupPage.getByRole("navigation", { name: "Canvas path" }).getByRole("button", { name: "Patch", exact: true }).click();
    await openGroup.waitFor();
    await clickWorkspaceTool(groupPage, "edit", "Selection actions", false);
    await groupPanel.getByRole("button", { name: "Clear selection" }).click();
    await groupPanel.getByLabel("Individual module node").selectOption({ index: 1 });
    await groupPanel.getByRole("button", { name: "Toggle node selection" }).click();
    await groupPanel.getByRole("button", { name: "Ungroup composite", exact: true }).click();
    await openGroup.waitFor({ state: "detached" });
    await groupViewport.locator("article[data-node-id]").nth(3).waitFor();
    assert.equal(await groupViewport.locator("article[data-node-id]").count(), 4);
    assert.equal(await groupViewport.locator(".node-selected").count(), 3);
    await editButton(groupPage, "Undo").click();
    await openGroup.waitFor();
    await editButton(groupPage, "Redo").click();
    await openGroup.waitFor({ state: "detached" });
    assert.equal(await groupViewport.locator("article[data-node-id]").count(), 4);
    await groupPage.close();

    // Exercise the same published workspace at phone dimensions, including
    // responsive reflow without recreating the graph or playback controller.
    const mobilePage = await browser.newPage({ viewport: { width: 360, height: 640 }, isMobile: true, hasTouch: true });
    await mobilePage.goto("http://127.0.0.1:" + server.address().port + "/?test-stale=1", { waitUntil: "domcontentloaded" });
    await mobilePage.waitForURL(url => url.searchParams.get("orbit-build") === version);
    const mobileCanvas = mobilePage.locator(".canvas-viewport");
    const mobileBar = mobilePage.getByRole("navigation", { name: "Workspace tools" });
    await mobilePage.getByRole("button", { name: "Play patch" }).waitFor();
    assert.equal(await mobilePage.locator(".workspace-build").innerText(), "b" + process.env.GITHUB_RUN_NUMBER);
    await mobileCanvas.locator("article[data-node-id]").first().waitFor();
    for (const [width, height] of [[360, 640], [390, 844], [800, 390]]) {
        await mobilePage.setViewportSize({ width, height });
        await mobileBar.waitFor();
        const metrics = await mobilePage.evaluate(() => {
            const workspace = document.querySelector(".graph-workspace").getBoundingClientRect();
            const canvas = document.querySelector(".canvas-viewport").getBoundingClientRect();
            const bar = document.querySelector(".mobile-workspace-bar").getBoundingClientRect();
            const header = document.querySelector(".workspace-header").getBoundingClientRect();
            const build = document.querySelector(".workspace-build").getBoundingClientRect();
            return { ratio: canvas.height / workspace.height, canvasBottom: canvas.bottom, barTop: bar.top,
                width: document.documentElement.scrollWidth, viewport: innerWidth,
                buildFits: build.right <= header.right + 1 && build.bottom <= header.bottom + 1 };
        });
        assert.ok(metrics.ratio >= (height > width ? .84 : .75), JSON.stringify(metrics));
        assert.ok(metrics.canvasBottom <= metrics.barTop + 1);
        assert.ok(metrics.width <= metrics.viewport);
        assert.ok(metrics.buildFits, "Build identifier must fit the compact mobile header");
    }
    await mobilePage.setViewportSize({ width: 390, height: 844 });
    await mobileBar.getByRole("button", { name: "Nodes", exact: true }).click();
    const previousMobileCount = await mobileCanvas.locator("article[data-node-id]").count();
    await addNode(mobilePage, "generator.oscillator");
    await mobileCanvas.locator("article[data-node-id]").nth(previousMobileCount).waitFor();
    await mobilePage.locator(".node-library-panel").waitFor({ state: "hidden" });
    await openNodeLibrary(mobilePage);
    await mobilePage.locator(".node-library-results h3").first().filter({ hasText: "Recently used" }).waitFor();
    await closeNodeLibrary(mobilePage);
    await mobileBar.getByRole("button", { name: "Edit", exact: true }).click();
    await mobilePage.getByRole("button", { name: "Undo", exact: true }).click();
    await mobileCanvas.locator("article[data-node-id]").nth(previousMobileCount).waitFor({ state: "detached" });
    await mobilePage.getByRole("button", { name: "Selection actions", exact: true }).click();
    const mobileSelection = mobilePage.getByRole("region", { name: "Node selection" });
    await mobileSelection.getByRole("button", { name: "Select all module nodes" }).click();
    await mobileSelection.getByRole("status").filter({ hasText: "3 nodes selected" }).waitFor();
    await mobileSelection.getByRole("button", { name: "Marquee mode" }).click();
    await mobileSelection.waitFor({ state: "hidden" });
    await mobileBar.getByRole("button", { name: "Edit", exact: true }).click();
    await mobilePage.getByRole("button", { name: "Selection actions", exact: true }).click();
    await mobileSelection.getByRole("button", { name: "Return to pan" }).click();
    await mobileSelection.waitFor({ state: "hidden" });
    await mobileBar.getByRole("button", { name: "View", exact: true }).click();
    await mobilePage.getByLabel("Connection labels", { exact: true }).selectOption("Off");
    await mobilePage.getByLabel("Annotations", { exact: true }).selectOption("Hide");
    await mobilePage.keyboard.press("Escape");
    await mobilePage.getByLabel("Connection labels", { exact: true }).waitFor({ state: "hidden" });
    await mobilePage.waitForFunction(() => !history.state?.orbitMobilePanel);
    assert.equal(await mobileBar.getByRole("button", { name: "View", exact: true }).evaluate(button => document.activeElement === button), true);
    // A touch user must be able to remove an individual connection without a
    // keyboard, mouse, or amount badge. Hide labels (above), fit the patch,
    // then tap a real exposed point on the LFO-to-frequency cable.
    // Fit is asynchronous; force a different transform and await the actual
    // DOM updates before calculating a tap coordinate.
    const mobileWorld = mobileCanvas.locator(".canvas-world");
    const beforeZoom = await mobileWorld.getAttribute("style");
    await zoomStep(mobilePage, 1);
    await mobilePage.waitForFunction(old => document.querySelector(".canvas-world")?.getAttribute("style") !== old, beforeZoom);
    const zoomed = await mobileWorld.getAttribute("style");
    await tripleTapFit(mobilePage);
    await mobilePage.waitForFunction(old => document.querySelector(".canvas-world")?.getAttribute("style") !== old, zoomed);
    const mobileWire = mobileCanvas.locator(".connection-modulation .connection-hit");
    const mobileWires = mobileCanvas.locator(".connection-path");
    const mobileNodes = mobileCanvas.locator("article[data-node-id]");
    const originalNodeIds = await mobileNodes.evaluateAll(nodes => nodes.map(node => node.dataset.nodeId).sort());
    const originalWireCount = await mobileWires.count();
    const originalAudioWireCount = await mobileCanvas.locator(".connection-audio").count();
    assert.equal(await mobileCanvas.locator(".connection-amount-badge").count(), 0);
    assert.equal(await mobileWire.count(), 1);
    assert.equal(originalWireCount, 3);
    let touchPoint = await exposedWirePoint(mobileWire);
    // Fit measures DOM geometry asynchronously. Wait for the new transform
    // to expose a real path rather than guessing its coordinates.
    for (let attempt = 0; !touchPoint && attempt < 30; attempt++) {
        await new Promise(resolve => setTimeout(resolve, 50));
        touchPoint = await exposedWirePoint(mobileWire);
    }
    assert.ok(touchPoint, "Expected an exposed cable point on the fitted phone canvas");
    // Log actual touch dispatch on failure, including browser target adjustment.
    await mobilePage.evaluate(() => {
        window.orbitConnectionTapEvents = [];
        for (const type of ["pointerdown", "pointerup", "click"])
            document.addEventListener(type, event => window.orbitConnectionTapEvents.push({
                type, target: event.target?.outerHTML?.slice(0, 240),
                x: event.clientX, y: event.clientY, button: event.button, pointerType: event.pointerType
            }), { capture: true, once: true });
    });
    await mobilePage.touchscreen.tap(touchPoint.x, touchPoint.y);
    try {
        await mobileCanvas.locator(".connection-modulation.connection-selected").waitFor({ state: "attached", timeout: 5000 });
    } catch (error) {
        console.error("Mobile connection tap", touchPoint, await mobilePage.evaluate(() => window.orbitConnectionTapEvents));
        throw error;
    }
    const mobileAmountEditor = mobilePage.getByRole("region", { name: "Connection amount editor" });
    await mobileAmountEditor.waitFor();
    await mobileBar.getByRole("button", { name: "Edit", exact: true }).tap();
    const mobileEdit = mobilePage.getByRole("region", { name: "Editing tools", exact: true });
    await mobileEdit.getByRole("status").filter({ hasText: "0 nodes · 1 connections selected" }).waitFor();
    await mobileEdit.getByRole("button", { name: "Delete selected", exact: true }).tap();
    await mobileWire.waitFor({ state: "detached" });
    await mobileAmountEditor.waitFor({ state: "detached" });
    await mobileCanvas.locator(".canvas-count").filter({ hasText: /^4 nodes · 2 connections$/ }).waitFor();
    assert.equal(await mobileWires.count(), originalWireCount - 1);
    assert.equal(await mobileCanvas.locator(".connection-audio").count(), originalAudioWireCount);
    assert.deepEqual(await mobileNodes.evaluateAll(nodes => nodes.map(node => node.dataset.nodeId).sort()), originalNodeIds);
    await mobileEdit.getByRole("status").filter({ hasText: "No selection" }).waitFor();
    await mobileEdit.getByRole("button", { name: "Undo", exact: true }).tap();
    await mobileWire.waitFor({ state: "attached" });
    await mobileCanvas.locator(".canvas-count").filter({ hasText: /^4 nodes · 3 connections$/ }).waitFor();
    assert.equal(await mobileWires.count(), originalWireCount);
    assert.deepEqual(await mobileNodes.evaluateAll(nodes => nodes.map(node => node.dataset.nodeId).sort()), originalNodeIds);
    await mobileBar.getByRole("button", { name: "Edit", exact: true }).tap();
    await mobileEdit.waitFor({ state: "hidden" });
    await mobilePage.waitForFunction(() => !history.state?.orbitMobilePanel);
    await mobileBar.getByRole("button", { name: "Outline", exact: true }).click();
    await mobilePage.getByRole("region", { name: "Patch outline" }).waitFor();
    await mobilePage.waitForFunction(() => !!history.state?.orbitMobilePanel);
    await mobilePage.goBack();
    await mobilePage.getByRole("region", { name: "Patch outline" }).waitFor({ state: "detached" });
    await mobilePage.getByRole("button", { name: /BPM$/ }).click();
    await mobilePage.getByRole("spinbutton", { name: "Tempo (BPM)" }).waitFor();
    await mobilePage.getByRole("button", { name: /BPM$/ }).click();
    await mobileBar.getByRole("button", { name: "More", exact: true }).click();
    await mobilePage.getByRole("button", { name: "Patches", exact: true }).waitFor();
    const mobilePerformanceToggle = mobilePage.getByRole("button", { name: "Performance · Off" });
    await mobilePerformanceToggle.click();
    await mobilePage.getByRole("region", { name: "Audio performance diagnostics" }).waitFor();
    await mobileBar.getByRole("button", { name: "View", exact: true }).click();
    await mobilePage.getByRole("region", { name: "Audio performance diagnostics" }).waitFor({ state: "detached" });
    await mobileBar.getByRole("button", { name: "More", exact: true }).click();
    await mobilePerformanceToggle.waitFor();
    await mobilePage.getByRole("button", { name: "Patches", exact: true }).click();
    const mobileManager = mobilePage.getByRole("region", { name: "Patch manager" });
    await mobileManager.getByRole("button", { name: "Edit patch overview" }).click();
    assert.equal(await mobileBar.getByRole("button", { name: "View", exact: true }).isEnabled(), false);
    await mobilePage.waitForFunction(() => !!history.state?.orbitMobilePanel);
    await mobilePage.goBack();
    await mobileManager.waitFor();
    await mobilePage.waitForFunction(() => !!history.state?.orbitMobilePanel);
    await mobileManager.getByRole("button", { name: "Cancel", exact: true }).click();
    await mobileBar.getByRole("button", { name: "View", exact: true }).click();
    await mobileManager.waitFor({ state: "detached" });
    await mobilePage.getByRole("button", { name: "Close view tools" }).click();
    await mobilePage.getByLabel("Connection labels", { exact: true }).waitFor({ state: "hidden" });
    const mobilePlay = mobilePage.getByRole("button", { name: "Play patch" });
    await mobilePlay.click();
    const mobileStatus = mobilePage.locator(".playback-toolbar [role=status]");
    await mobilePage.waitForFunction(() => document.querySelector(".playback-toolbar [role=status]")?.textContent.startsWith("Playing revision"), null, { timeout: 30000 });
    const playingRevision = await mobileStatus.textContent();
    await mobilePage.setViewportSize({ width: 1000, height: 800 });
    await mobileBar.waitFor();
    assert.ok((await mobileBar.boundingBox()).y < (await mobileCanvas.boundingBox()).y);
    await mobilePage.setViewportSize({ width: 390, height: 844 });
    await mobileBar.waitFor();
    assert.equal(await mobileStatus.textContent(), playingRevision, "Responsive reflow must not restart playback");
    await mobilePage.getByRole("button", { name: "Stop", exact: true }).click();
    await mobilePage.close();
    await verifyTouchWire(browser, "http://127.0.0.1:" + server.address().port);
    await verifyWireEdgePan(browser, "http://127.0.0.1:" + server.address().port);
    await verifyCanvasNavigation(browser, "http://127.0.0.1:" + server.address().port);
    await verifyCanvasContext(browser, "http://127.0.0.1:" + server.address().port);
    for (const file of ["canvasDom.js", "canvasContext.js", "workspacePanels.js", "nodeLibraryStorage.js", "performanceKeyboard.js",
        "scopeView.js", "scopeAnalysis.js", "signalPlot.js", "spectrumAnalysis.js", "spectrumPlot.js"])
        assert.ok(requests.has(`/js-${version}/${file}`), "Missing versioned editor module: " + file);
    console.log("Published editor played, edited nested graphs and restored the saved workspace after reload.");
} finally {
    await browser?.close();
    server.close();
}
