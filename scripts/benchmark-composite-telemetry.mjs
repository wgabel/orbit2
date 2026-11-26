import assert from "node:assert/strict";
import { setTimeout as delay } from "node:timers/promises";

// Measure only steady-state presentation delivery and mounted frame renders.
// A remounted canvas owns new counters; never subtract across navigation.
export async function measureCompositeTelemetry({ page, canvas, status, composite, idleMs }) {
    const revision = (await status.innerText()).trim();
    assert.match(revision, /^Playing revision \d+$/);
    const path = page.getByRole("navigation", { name: "Canvas path" });
    const steps = [];
    const liveOutput = '.parameter-slider output[aria-label="Live effective value"]';
    async function snapshot() {
        return canvas.evaluate((view, selector) => {
            const data = view.dataset;
            const counter = name => data[name] === undefined || data[name] === "" ? null : Number(data[name]);
            const frames = [...view.querySelectorAll("article[data-node-id]")].map(node => ({
                id: node.dataset.nodeId,
                renders: node.dataset.nodeRenderCount === undefined ? null : Number(node.dataset.nodeRenderCount),
                liveReadouts: [...node.querySelectorAll(selector)].filter(output =>
                    Number.isFinite(Number.parseFloat(output.textContent))).length
            }));
            return { graphId: data.telemetryGraph, deliveries: counter("telemetryDeliveries"),
                effectiveValues: counter("telemetryEffective"), playheads: counter("telemetryPlayheads"),
                foreignEffectiveValues: counter("telemetryForeignEffective"),
                foreignPlayheads: counter("telemetryForeignPlayheads"), frames };
        }, liveOutput);
    }
    async function sample(name, ids) {
        await page.waitForFunction(expected => {
            const nodes = [...document.querySelectorAll(".canvas-world article[data-node-id]")];
            return nodes.length === expected.length && nodes.every(node =>
                expected.includes(node.dataset.nodeId) && node.hasAttribute("data-node-render-count"));
        }, ids, { timeout: 120000 });
        if (name === "child-open") {
            await page.waitForFunction(selector => [...document.querySelectorAll(".canvas-world " + selector)]
                .some(output => Number.isFinite(Number.parseFloat(output.textContent))), liveOutput, { timeout: 60000 });
        }
        await delay(1000);
        const before = await snapshot();
        await delay(idleMs);
        const after = await snapshot();
        for (const s of [before, after]) {
            assert.ok(s.graphId, "Canvas telemetry diagnostics not enabled");
            assert.deepEqual(s.frames.map(frame => frame.id).sort(), [...ids].sort());
            for (const n of [s.deliveries, s.effectiveValues, s.playheads, s.foreignEffectiveValues,
                s.foreignPlayheads, ...s.frames.flatMap(frame => [frame.renders, frame.liveReadouts])])
                assert.ok(Number.isSafeInteger(n) && n >= 0, "Missing or invalid diagnostic count");
        }
        assert.equal(after.graphId, before.graphId);
        assert.equal((await status.innerText()).trim(), revision);
        const prior = new Map(before.frames.map(frame => [frame.id, frame.renders]));
        const frameDeltas = after.frames.map(frame => ({ id: frame.id, renders: frame.renders - prior.get(frame.id),
            liveReadouts: frame.liveReadouts }));
        const deliveries = after.deliveries - before.deliveries;
        assert.ok(deliveries >= 0 && frameDeltas.every(frame => frame.renders >= 0));
        if (name === "child-open") {
            assert.ok(after.effectiveValues > 0 && frameDeltas.some(frame => frame.liveReadouts > 0),
                "Expected actual child effective values and live controls");
        }
        const step = { name, graphId: after.graphId, idleMs, mountedFrames: after.frames.length,
            parameterDeliveries: deliveries, effectiveValues: after.effectiveValues, playheads: after.playheads,
            foreignEffectiveValues: after.foreignEffectiveValues, foreignPlayheads: after.foreignPlayheads,
            totalFrameRenders: frameDeltas.reduce((sum, frame) => sum + frame.renders, 0),
            affectedFrames: frameDeltas.filter(frame => frame.renders > 0).length, frameDeltas };
        steps.push(step);
        console.log("EDITOR COMPOSITE TELEMETRY " + JSON.stringify({ ...step, frameDeltas: undefined }));
    }
    await sample("root-closed", composite.rootNodeIds);
    await canvas.locator(`article[data-node-id="${composite.wrapperId}"]`)
        .getByRole("button", { name: "Open " + composite.name, exact: true }).evaluate(button => button.click());
    await path.locator('[aria-current="page"]').filter({ hasText: composite.name }).waitFor({ timeout: 60000 });
    await sample("child-open", composite.childNodeIds);
    await path.getByRole("button").first().evaluate(button => button.click());
    await sample("root-reclosed", composite.rootNodeIds);
    assert.equal(steps[0].graphId, steps[2].graphId);
    assert.notEqual(steps[0].graphId, steps[1].graphId);
    return { revision, steps, allSampledInputsLocal: steps.every(step =>
        step.foreignEffectiveValues === 0 && step.foreignPlayheads === 0),
        limitations: "Canvas input snapshots and mounted frame deltas only; not upstream acquisition, transport or DSP counts." };
}
