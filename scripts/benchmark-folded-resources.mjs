import assert from "node:assert/strict";
import { setTimeout as delay } from "node:timers/promises";
import { openResourceDiagnostics } from "./benchmark-resource-diagnostics.mjs";

const eligible = "article.node-compactable[data-node-id]";
const liveOutput = '.parameter-slider output[aria-label="Live effective value"]';

export async function verifyFoldedResources({ page, canvas, status, idleMs }) {
    const revision = (await status.innerText()).trim();
    assert.match(revision, /^Playing revision \d+$/);
    const { setCollection, snapshot } = await openResourceDiagnostics(page);
    const original = await canvas.locator(eligible).evaluateAll(nodes => nodes.map(node => ({
        id: node.dataset.nodeId, expanded: node.querySelector(".node-expand-toggle").getAttribute("aria-expanded") === "true"
    })));
    assert.ok(original.length >= 5 && original.every(node => node.expanded),
        "Expected fresh all-expanded flat fixture");
    const frame = id => canvas.locator(`article[data-node-id="${id}"]`);
    const scope = page.getByRole("region", { name: "Signal scope", exact: true });
    const activate = button => button.evaluate(element => element.click());
    const steps = [];
    async function expand(id, expanded) {
        const toggle = frame(id).locator(".node-expand-toggle");
        if (await toggle.getAttribute("aria-expanded") !== String(expanded)) {
            await activate(toggle);
            await frame(id).locator(`.node-expand-toggle[aria-expanded="${expanded}"]`)
                .waitFor({ state: "attached", timeout: 30000 });
        }
    }
    async function live(panel) {
        await panel.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor({ timeout: 30000 });
    }
    async function counts() {
        return canvas.locator(eligible).evaluateAll(nodes => nodes.map(node => ({
            id: node.dataset.nodeId,
            expanded: node.querySelector(".node-expand-toggle").getAttribute("aria-expanded") === "true",
            renders: Number(node.dataset.nodeRenderCount),
            liveReadouts: node.querySelectorAll('output[aria-label="Live effective value"]').length,
            compactScopes: node.querySelectorAll(".signal-scope").length
        })));
    }
    async function canvasCounters() {
        return canvas.evaluate(view => {
            const data = view.dataset;
            const count = name => data[name] === undefined || data[name] === "" ? null : Number(data[name]);
            return { graphId: data.telemetryGraph, deliveries: count("telemetryDeliveries"),
                effectiveValues: count("telemetryEffective"), playheads: count("telemetryPlayheads"),
                foreignEffectiveValues: count("telemetryForeignEffective"), foreignPlayheads: count("telemetryForeignPlayheads") };
        });
    }
    async function sample(name, expandedIds, expectedLeases) {
        const deadline = Date.now() + 15000;
        let value, stable = 0;
        do {
            value = await snapshot();
            const match = value?.captureLeases === expectedLeases && value?.occupiedAuxSlots === expectedLeases &&
                await page.locator(".signal-scope").count() === expectedLeases;
            stable = match ? stable + 1 : 0;
            if (stable >= 3) break;
            await delay(250);
        } while (Date.now() < deadline);
        assert.ok(stable >= 3, name + ": resources did not settle: " + JSON.stringify(value));
        assert.equal(value.currentVoices, 1);
        assert.equal(value.retiringVoices, 0);
        assert.equal(value.processorErrors, 0);
        await delay(1000); // exclude fold/layout transitions from render deltas
        const before = await counts();
        const canvasBefore = await canvasCounters();
        await delay(idleMs);
        const canvasAfter = await canvasCounters();
        const after = await counts();
        assert.ok(canvasBefore.graphId && canvasBefore.graphId === canvasAfter.graphId);
        for (const sample of [canvasBefore, canvasAfter])
            for (const [key, n] of Object.entries(sample))
                if (key !== "graphId") assert.ok(Number.isSafeInteger(n) && n >= 0, "Invalid canvas counter " + key);
        const parameterDeliveries = canvasAfter.deliveries - canvasBefore.deliveries;
        assert.ok(parameterDeliveries >= 0);
        assert.equal(canvasAfter.foreignEffectiveValues + canvasAfter.foreignPlayheads, 0);
        assert.deepEqual(after.map(n => n.id).sort(), original.map(n => n.id).sort());
        assert.deepEqual(after.filter(n => n.expanded).map(n => n.id).sort(), [...expandedIds].sort());
        const prior = new Map(before.map(n => [n.id, n.renders]));
        for (const node of after) {
            assert.ok(Number.isSafeInteger(node.renders) && Number.isSafeInteger(prior.get(node.id)));
            if (!node.expanded) {
                assert.equal(node.liveReadouts, 0, name + ": folded live readout");
                assert.equal(node.compactScopes, 0, name + ": folded compact scope");
                assert.equal(node.renders, prior.get(node.id), name + ": folded frame rendered " + node.id);
            }
        }
        const final = await snapshot();
        assert.equal(final?.captureLeases, expectedLeases);
        assert.equal(final?.occupiedAuxSlots, expectedLeases);
        assert.equal(final?.currentVoices, 1);
        assert.equal(final?.retiringVoices, 0);
        assert.equal(final?.processorErrors, 0);
        assert.equal(await page.locator(".signal-scope").count(), expectedLeases);
        assert.equal((await status.innerText()).trim(), revision);
        const step = { name, expanded: expandedIds.length, folded: original.length - expandedIds.length,
            expectedLeases, ...final, idleMs, parameterDeliveries,
            effectiveValues: canvasAfter.effectiveValues, playheads: canvasAfter.playheads,
            foreignEffectiveValues: canvasAfter.foreignEffectiveValues, foreignPlayheads: canvasAfter.foreignPlayheads,
            foldedFrameRenders: 0,
            expandedFrameRenders: after.filter(n => n.expanded).reduce((sum, n) => sum + n.renders - prior.get(n.id), 0) };
        steps.push(step);
        console.log("EDITOR FOLDED RESOURCES " + JSON.stringify(step));
        if (process.env.ORBIT_EDITOR_ASSERT_FOLDED_DEMAND === "1") {
            if (expandedIds.length === 0) {
                assert.equal(step.effectiveValues + step.playheads, 0, name + ": folded canvas has unwanted telemetry");
                assert.equal(step.parameterDeliveries, 0, name + ": folded canvas receives periodic updates");
            } else {
                assert.ok(step.effectiveValues > 0 && step.effectiveValues < 13,
                    name + ": only the selected carrier's effective values should be delivered");
                assert.ok(step.parameterDeliveries > 0 && step.expandedFrameRenders > 0,
                    name + ": expanded carrier must keep updating");
            }
        }
    }
    await setCollection(true);
    try {
        // Same viewport-aware carrier criterion as the verified composite probe.
        const candidate = await page.waitForFunction(selector => {
            const view = document.querySelector(".canvas-viewport");
            if (!view) return null;
            const bounds = view.getBoundingClientRect();
            return [...view.querySelectorAll("article.node-compactable[data-node-id]")]
                .filter(node => node.querySelector('.parameter-slider input[aria-label="Frequency"]') &&
                    node.querySelector(".plot-toggle") && [...node.querySelectorAll(selector)]
                        .some(output => Number.isFinite(Number.parseFloat(output.textContent))))
                .find(node => {
                    const r = node.getBoundingClientRect();
                    return r.left >= bounds.left && r.right <= bounds.right &&
                        r.top >= bounds.top && r.bottom + 80 <= bounds.bottom;
                })?.dataset.nodeId ?? null;
        }, liveOutput, { timeout: 60000 });
        const id = await candidate.jsonValue();
        await candidate.dispose();
        const node = frame(id);
        const compact = node.locator(".node-visualization-panel");
        await activate(compact.getByRole("button", { name: "Show plot", exact: true }));
        await live(compact);
        for (const n of original) if (n.id !== id) await expand(n.id, false);
        await live(compact);
        await sample("one-expanded-compact", [id], 1);
        await expand(id, false);
        await compact.waitFor({ state: "detached" });
        await sample("all-folded-no-scope", [], 0);
        await expand(id, true);
        await live(compact);
        await page.waitForFunction(({ id, selector }) => {
            const node = document.querySelector(`article[data-node-id="${id}"]`);
            return node && [...node.querySelectorAll(selector)].some(output =>
                Number.isFinite(Number.parseFloat(output.textContent)));
        }, { id, selector: liveOutput }, { timeout: 30000 });
        await sample("one-expanded-rebound", [id], 1);
        await activate(compact.getByRole("button", { name: "Expand plot", exact: true }));
        await live(scope);
        await expand(id, false);
        await live(scope);
        await sample("all-folded-independent-scope", [], 1);
        await live(scope);
        await activate(scope.getByRole("button", { name: "Close scope", exact: true }));
        await scope.waitFor({ state: "detached" });
        await sample("all-folded-scope-closed", [], 0);
        await expand(id, true);
        await live(compact);
        await activate(compact.getByRole("button", { name: "Hide plot", exact: true }));
        await sample("one-expanded-plot-hidden", [id], 0);
        for (const n of original) await expand(n.id, n.expanded);
        assert.equal((await status.innerText()).trim(), revision);
        return { nodeId: id, revision, steps };
    } finally {
        await setCollection(false);
    }
}
