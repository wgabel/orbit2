// Exercise a native nested fixture through the published editor and real AudioWorklet.
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

export async function verifyConnectedMacro(browser, url, sampleRate, tools) {
    const fixture = JSON.parse(await readFile(resolve(process.env.ORBIT_GRAPH_PROBE_DIR, "connected-macro-patch.json"), "utf8"));
    const outer = fixture.rootGraph.nodes.find(node => node.composite);
    const middle = outer.composite.graph;
    const inner = middle.nodes.find(node => node.composite);
    const macro = inner.composite.macros[0];
    const edge = middle.connections.find(connection => connection.destination.nodeId === inner.id);
    const child = inner.composite.graph;
    const carrier = child.nodes.find(node => node.parameters.some(parameter => parameter.key === "frequency"));
    const frequency = carrier.parameters.find(parameter => parameter.key === "frequency");
    const amplitude = carrier.parameters.find(parameter => parameter.key === "amplitude");
    fixture.canvasLayouts = [{
        graphId: middle.id, zoom: 1, panX: 0, panY: 0,
        positions: [{ nodeId: inner.id, x: 40, y: 100 }, { nodeId: edge.source.nodeId, x: 400, y: 300 }]
    }];

    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    try {
        await page.addInitScript(rate => {
            window.AudioContext = class extends window.AudioContext {
                constructor(options) { super({ ...options, sampleRate: rate }); }
            };
        }, sampleRate);
        await page.goto(url);
        const status = page.locator(".playback-toolbar [role=status]");
        await status.filter({ hasText: /Ready/ }).waitFor({ timeout: 30000 });
        await tools.clickWorkspaceTool(page, "more", "Patches", false);
        const manager = page.locator(".patch-manager");
        await manager.getByLabel("Import JSON").setInputFiles({
            name: "connected-macro.json", mimeType: "application/json", buffer: Buffer.from(JSON.stringify(fixture))
        });
        await manager.getByText("Imported patch.", { exact: true }).waitFor();
        await manager.getByRole("button", { name: "Close" }).click();
        await tools.closeWorkspacePanel(page, "more");

        const node = page.locator(`article[data-node-id="${inner.id}"]`);
        const group = node.getByRole("group", { name: "Composite macro controls" });
        const base = group.getByRole("spinbutton");
        const range = group.locator('output[aria-label="Predicted modulation range"]');
        const live = group.locator('output[aria-label="Live effective value"]');
        await node.getByRole("button", { name: "Input: Unused (Modulation) — controls Reference", exact: true }).waitFor();
        await range.filter({ hasText: /^0\.25–0\.75/ }).waitFor();
        assert.equal(Number(await base.inputValue()), 0.5);

        await node.getByRole("button", { name: "Edit macros", exact: true }).click();
        const editor = page.getByRole("region", { name: "Composite macro editor" });
        assert.equal(await editor.getByLabel("Modulation input").inputValue(), macro.modulationInputId);
        await editor.getByRole("button", { name: "Cancel", exact: true }).click();
        await editor.waitFor({ state: "detached" });

        await tools.openWorkspacePanel(page, "more");
        await page.getByRole("button", { name: /^Performance ·/ }).click();
        const monitor = page.getByRole("region", { name: "Audio performance diagnostics" });
        await monitor.locator("[data-performance-session]").filter({ hasText: /^idle$/ }).waitFor();
        await page.getByRole("button", { name: "Play patch" }).click();
        await status.filter({ hasText: /Playing revision/ }).waitFor({ timeout: 30000 });
        const metric = name => monitor.getByText(name, { exact: true }).locator("xpath=following-sibling::dd[1]");
        await metric("Sample rate").filter({ hasText: new RegExp(`^${sampleRate} Hz$`) }).waitFor();
        const voices = monitor.locator("[data-performance-worklets]");
        await voices.filter({ hasText: /^1 \/ 0$/ }).waitFor();
        const builds = metric("Faust source builds");
        const replacements = metric("Accepted replacements");
        await builds.filter({ hasText: /^1$/ }).waitFor();
        await live.filter({ hasText: /^0\.(25|75)/ }).waitFor();

        await base.fill("0.6");
        await range.filter({ hasText: /^0\.35–0\.85/ }).waitFor();
        await live.filter({ hasText: /^0\.(35|85)/ }).waitFor();
        assert.equal(Number(await base.inputValue()), 0.6);
        await metric("Live control updates").filter({ hasText: /^[1-9]\d*$/ }).waitFor();

        // Navigate into the child during playback. Its meters use child addresses,
        // and its editable manual bases remain separate from the mapped values.
        await node.getByRole("button", { name: `Open ${inner.composite.name}`, exact: true }).click();
        const childCanvas = page.locator(`.graph-workspace[data-active-graph="${child.id}"]`);
        await childCanvas.waitFor();
        assert.equal(await group.count(), 0);
        const carrierNode = childCanvas.locator(`article[data-node-id="${carrier.id}"]`);
        for (const [parameter, targetRange, targetLive] of [
            [frequency, /^246\.95–378\.95 Hz$/, /^(246\.95|378\.95) Hz$/],
            [amplitude, /^0\.26–0\.46/, /^0\.(26|46)/]
        ]) {
            const ownership = carrierNode.locator(`[data-macro-parameter="${parameter.id}"]`);
            await ownership.filter({ hasText: /Reference/ }).waitFor({ state: "attached" });
            const parameterEditor = ownership.locator("..");
            await parameterEditor.locator('output[aria-label="Predicted modulation range"]').filter({ hasText: targetRange }).waitFor();
            await parameterEditor.locator('output[aria-label="Live effective value"]').filter({ hasText: targetLive }).waitFor();
            assert.equal(Number(await parameterEditor.locator('input[id^="parameter-"]').inputValue()), parameter.baseValue);
        }
        const path = page.getByRole("navigation", { name: "Canvas path" });
        await path.getByRole("button", { name: outer.composite.name, exact: true }).first().click();
        await live.filter({ hasText: /^0\.(35|85)/ }).waitFor();
        assert.deepEqual([Number(await builds.innerText()), Number(await replacements.innerText())], [1, 0]);

        const wire = page.locator(`.connection-hit[data-connection-id="${edge.id}"]`);
        // Use the canvas context actions so the performance session stays open.
        const contextAction = async (target, name) => {
            await target.press("Shift+F10");
            const menu = page.locator(".canvas-context-menu");
            await menu.getByRole("button", { name, exact: true }).click();
            await menu.waitFor({ state: "detached" });
        };
        const viewport = page.locator(".canvas-viewport");
        await contextAction(wire, "Delete connection");
        await wire.waitFor({ state: "detached" });
        await range.waitFor({ state: "detached" });
        await live.waitFor({ state: "detached" });
        await replacements.filter({ hasText: /^1$/ }).waitFor();

        await contextAction(viewport, "Undo");
        await live.filter({ hasText: /^0\.(35|85)/ }).waitFor();
        await replacements.filter({ hasText: /^2$/ }).waitFor();
        await contextAction(viewport, "Redo");
        await range.waitFor({ state: "detached" });
        await replacements.filter({ hasText: /^3$/ }).waitFor();
        await voices.filter({ hasText: /^1 \/ 0$/ }).waitFor();
        assert.equal(Number(await builds.innerText()), 4);
        await page.getByRole("button", { name: "Stop", exact: true }).click();
        await voices.filter({ hasText: /^0 \/ 0$/ }).waitFor();
        assert.equal(Number(await metric("Capture leases").innerText()), 0);

        await monitor.getByRole("button", { name: "Close" }).click();
        await tools.clickWorkspaceTool(page, "more", "Patches", false);
        const downloaded = page.waitForEvent("download");
        await manager.getByRole("button", { name: "Export JSON", exact: true }).click();
        const exported = JSON.parse(await readFile(await (await downloaded).path(), "utf8"));
        const savedMiddle = exported.rootGraph.nodes.find(saved => saved.id === outer.id).composite.graph;
        const savedMacro = savedMiddle.nodes.find(saved => saved.id === inner.id).composite.macros[0];
        assert.deepEqual(savedMacro, { ...macro, baseValue: 0.6 });
        assert.ok(!savedMiddle.connections.some(connection => connection.id === edge.id));
        console.log(`Connected macro at ${sampleRate} Hz: initial build 1, live edit/navigation +0 builds, cable edit/undo/redo +3 replacements, stopped worklets and leases 0`);
    } finally {
        await page.close();
    }
}
