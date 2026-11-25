import assert from "node:assert/strict";
import { setTimeout as delay } from "node:timers/promises";

// A focused render-count regression, separate from the ordinary all-expanded
// timing workloads. Use actual UI toggles, not synthetic telemetry or graph edits.
export async function verifyFoldedTelemetry({ page, canvas, status, renderCounts, idleMs }) {
    const liveOutput = '.parameter-slider output[aria-label="Live effective value"]';
    const liveNodes = () => canvas.evaluate((view, selector) =>
        [...view.querySelectorAll("article.node-compactable.node-expanded")]
            .filter(node => [...node.querySelectorAll(selector)].some(output =>
                Number.isFinite(Number.parseFloat(output.textContent))))
            .map(node => node.dataset.nodeId), liveOutput);
    const revision = (await status.innerText()).trim();
    assert.match(revision, /^Playing revision \d+$/);
    const before = await renderCounts();
    await delay(1000);
    const active = await renderCounts();
    const changing = (await liveNodes()).filter(id => active[id] > before[id]);
    assert.ok(changing.length >= 3, "Expected at least three independently updating expanded modulated frames");
    const folded = changing.slice(0, 2);
    const control = changing[2];
    const frame = id => canvas.locator(`article[data-node-id="${id}"]`);
    for (const id of folded) {
        // Dense fixtures can overlap. This is a consumer lifecycle test, not
        // a pointer-accessibility test (covered separately by navigation smoke).
        await frame(id).locator('.node-expand-toggle[aria-expanded="true"]').evaluate(button => button.click());
        await frame(id).locator('.node-expand-toggle[aria-expanded="false"]').waitFor({ state: "attached" });
        await frame(id).locator(".node-parameters").waitFor({ state: "detached" });
        assert.equal(await frame(id).locator(liveOutput).count(), 0);
    }
    // Exclude the fold transition itself and its layout reconciliation.
    await delay(350);
    const settled = await renderCounts();
    await page.waitForFunction(({ id, count }) => {
        const node = document.querySelector(`article[data-node-id="${id}"]`);
        return node && Number(node.dataset.nodeRenderCount) > count;
    }, { id: control, count: settled[control] }, { timeout: 15000 });
    await delay(idleMs);
    const after = await renderCounts();
    for (const id of folded)
        assert.equal(after[id], settled[id], `Folded node ${id} rendered during playback telemetry`);
    assert.ok(after[control] > settled[control], "Expanded control must continue receiving updates");
    assert.equal((await status.innerText()).trim(), revision, "Folding must not change accepted playback revision");
    for (const id of folded) {
        await frame(id).locator('.node-expand-toggle[aria-expanded="false"]').evaluate(button => button.click());
        await frame(id).locator('.node-expand-toggle[aria-expanded="true"]').waitFor({ state: "attached" });
        await page.waitForFunction(({ id, selector }) => {
            const node = document.querySelector(`article[data-node-id="${id}"]`);
            return node && [...node.querySelectorAll(selector)].some(output =>
                Number.isFinite(Number.parseFloat(output.textContent)));
        }, { id, selector: liveOutput }, { timeout: 15000 });
    }
    assert.equal((await status.innerText()).trim(), revision, "Reopening must not change accepted playback revision");
    return { foldedNodes: folded.length, foldedFrameRenders: 0,
        expandedControlRenders: after[control] - settled[control], idleMs };
}
