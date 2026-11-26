// Dedicated resource pass; do not enable diagnostics inside timing windows.
import assert from "node:assert/strict";
import { setTimeout as delay } from "node:timers/promises";
import { openResourceDiagnostics } from "./benchmark-resource-diagnostics.mjs";

export async function measureCompositeResources({ page, canvas, status, composite }) {
    const revision = (await status.innerText()).trim();
    assert.match(revision, /^Playing revision \d+$/);
    const { setCollection, snapshot } = await openResourceDiagnostics(page);
    const steps = [];
    const path = page.getByRole("navigation", { name: "Canvas path" });
    const wrapper = canvas.locator(`article[data-node-id="${composite.wrapperId}"]`);
    const activate = async button => button.evaluate(element => element.click());
    async function sample(name, expectedLeases) {
        let value = null, stable = 0;
        const started = Date.now();
        // A mismatch is baseline evidence, not a reason to discard the report.
        // Allow asynchronous scope disposal/rebinding to settle before recording.
        do {
            value = await snapshot();
            if (value && value.captureLeases === expectedLeases) stable++;
            else stable = 0;
            if (stable >= 3) break;
            await delay(250);
        } while (Date.now() - started < 6000);
        assert.ok(value, "Runtime resource diagnostics unavailable");
        for (const n of Object.values(value)) assert.ok(Number.isInteger(n) && n >= 0);
        assert.equal(value.currentVoices, 1, "Navigation must preserve one accepted voice");
        assert.equal(value.retiringVoices, 0);
        assert.equal((await status.innerText()).trim(), revision);
        const mounted = await page.locator(".signal-scope").count();
        steps.push({ name, expectedLeases, ...value, mountedScopes: mounted,
            matchesExpectedLeases: value.captureLeases === expectedLeases,
            settledExpected: stable >= 3, settleMs: Date.now() - started });
        console.log("EDITOR COMPOSITE RESOURCE " + JSON.stringify(steps.at(-1)));
    }
    async function openChild() {
        await activate(wrapper.getByRole("button", { name: "Open " + composite.name, exact: true }));
        await path.locator('[aria-current="page"]').filter({ hasText: composite.name }).waitFor({ timeout: 60000 });
        await canvas.locator("article[data-node-id]").nth(composite.childNodeIds.length - 1).waitFor({ state: "attached", timeout: 60000 });
    }
    async function closeChild() {
        await activate(path.getByRole("button").first());
        await wrapper.waitFor({ state: "attached", timeout: 60000 });
    }
    async function live(scope) {
        await scope.locator("[data-scope-status]").filter({ hasText: /Live/ }).waitFor({ timeout: 30000 });
    }
    await setCollection(true);
    try {
        await sample("root-no-plots", 0);
        await openChild();
        // Use an in-viewport modulated carrier, not an arbitrary offscreen
        // oscillator: IntersectionObserver deliberately releases hidden plots.
        const candidate = await page.waitForFunction(() => {
            const view = document.querySelector(".canvas-viewport");
            if (!view) return null;
            const bounds = view.getBoundingClientRect();
            return [...view.querySelectorAll("article.node-compactable[data-node-id]")]
                .filter(node => node.querySelector('.parameter-slider input[aria-label="Frequency"]') &&
                    node.querySelector(".plot-toggle") &&
                    [...node.querySelectorAll('output[aria-label="Live effective value"]')]
                        .some(output => Number.isFinite(Number.parseFloat(output.textContent.trim()))))
                .find(node => {
                    const r = node.getBoundingClientRect();
                    return r.left >= bounds.left && r.right <= bounds.right &&
                        r.top >= bounds.top && r.bottom + 80 <= bounds.bottom;
                })?.dataset.nodeId ?? null;
        }, null, { timeout: 60000 });
        const id = await candidate.jsonValue();
        await candidate.dispose();
        assert.ok(id, "No visible modulated carrier available for resource probe");
        const node = canvas.locator(`article[data-node-id="${id}"]`);
        const compact = node.locator(".node-visualization-panel");
        const scope = page.getByRole("region", { name: "Signal scope", exact: true });
        await sample("child-no-plots", 0);
        await activate(compact.getByRole("button", { name: "Show plot", exact: true }));
        await live(compact);
        await sample("compact-live", 1);
        await activate(node.locator(".node-expand-toggle"));
        await node.locator('.node-expand-toggle[aria-expanded="false"]').waitFor();
        await compact.waitFor({ state: "detached" });
        await sample("folded-compact", 0);
        await activate(node.locator(".node-expand-toggle"));
        await node.locator('.node-expand-toggle[aria-expanded="true"]').waitFor();
        await live(compact);
        await sample("compact-reopened", 1);
        await activate(compact.getByRole("button", { name: "Expand plot", exact: true }));
        await live(scope);
        await sample("independent-scope", 1);
        await activate(node.locator(".node-expand-toggle"));
        await node.locator('.node-expand-toggle[aria-expanded="false"]').waitFor();
        await live(scope);
        await sample("folded-independent-scope", 1);
        await activate(scope.getByRole("button", { name: "Close scope", exact: true }));
        await scope.waitFor({ state: "detached" });
        await sample("folded-scope-closed", 0);
        await activate(node.locator(".node-expand-toggle"));
        await live(compact);
        await sample("compact-before-navigation", 1);
        await closeChild();
        await sample("root-child-plot-inactive", 0);
        await openChild();
        await live(compact);
        await sample("child-plot-rebound", 1);
        await activate(compact.getByRole("button", { name: "Hide plot", exact: true }));
        await compact.locator(".signal-scope").waitFor({ state: "detached" });
        await sample("child-plot-hidden", 0);
        await closeChild();
        await sample("root-final", 0);
        return { nodeId: id, revision, steps, diagnostics: "existing runtime collection; no timing windows",
            allLeaseExpectationsMet: steps.every(step => step.settledExpected) };
    } finally {
        await setCollection(false);
    }
}
