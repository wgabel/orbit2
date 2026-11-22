import assert from "node:assert/strict";
import { fitCanvas } from "./CanvasNavigationSmoke.mjs";

// Use real Chromium touch events, not mouse emulation or synthetic DOM events.
export async function verifyTouchWire(browser, url) {
    for (const size of [{ width: 390, height: 844 }, { width: 650, height: 1000 }]) {
        const page = await browser.newPage({ viewport: size, isMobile: true, hasTouch: true });
        try {
            await page.goto(url);
            const canvas = page.locator(".canvas-viewport");
            const nodes = canvas.locator("article[data-node-id]");
            await nodes.nth(3).waitFor();
            const output = nodes.nth(0).locator(".port-output .port-marker");
            const amplitude = nodes.nth(1).locator('.port-input[aria-label^="Input: Amplitude"]');
            const frequency = nodes.nth(1).locator('.port-input[aria-label^="Input: Frequency"]');
            await fitCanvas(page);
            // overflow:hidden is a programmatically scrollable viewport. An
            // offscreen focus or browser scrolling can leave a nonzero scrollTop.
            // Force a small, reproducible offset without changing the graph.
            await canvas.evaluate(viewport => {
                const sentinel = document.createElement("span");
                sentinel.style.cssText = `position:absolute;top:${viewport.clientHeight + 200}px;left:0;width:1px;height:1px;pointer-events:none`;
                viewport.append(sentinel);
                viewport.scrollTop = 17;
            });
            assert.equal(await canvas.evaluate(viewport => viewport.scrollTop), 17);
            await page.waitForFunction(() => {
                const viewport = document.querySelector(".canvas-viewport");
                const frames = viewport.querySelectorAll("article[data-node-id]");
                const markers = [frames[0]?.querySelector(".port-output .port-marker"),
                    frames[1]?.querySelector('.port-input[aria-label^="Input: Amplitude"] .port-marker')];
                return markers.every(marker => {
                    if (!marker) return false;
                    const rect = marker.getBoundingClientRect();
                    const hit = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
                    return hit?.closest(".port-connector") === marker.closest(".port-connector");
                }) && viewport.querySelectorAll(".connection-hit").length === 3;
            });
            const center = async locator => {
                const rect = await locator.boundingBox();
                assert.ok(rect);
                return { x: rect.x + rect.width / 2, y: rect.y + rect.height / 2 };
            };
            const start = await center(output);
            const target = await center(amplitude.locator(".port-marker"));
            const bounds = await canvas.boundingBox();
            const bottom = Math.min(bounds.y + bounds.height, size.height);
            const end = { x: target.x, y: target.y + 60 <= bottom - 60 ? target.y + 60 : (target.y + bottom) / 2 };
            const cdp = await page.context().newCDPSession(page);
            const send = (type, point) => cdp.send("Input.dispatchTouchEvent", {
                type, touchPoints: point ? [{ ...point, radiusX: 10, radiusY: 10, id: 1 }] : []
            });
            const wires = canvas.locator(".connection-hit");
            const label = canvas.locator(".touch-wire-label");
            async function assertHighlightAlignment() {
                await page.waitForFunction(() => {
                    const viewport = document.querySelector(".canvas-viewport");
                    const rings = [...viewport.querySelectorAll(".touch-wire-target")];
                    return rings.length > 0 && rings.every(ring => {
                        const { targetNode, targetPort } = ring.dataset;
                        const marker = viewport.querySelector(`[data-node-id="${targetNode}"] [data-port-id="${targetPort}"]`);
                        if (!marker) return false;
                        const a = ring.getBoundingClientRect(), b = marker.getBoundingClientRect();
                        return Math.abs(a.x + a.width / 2 - b.x - b.width / 2) < 2 &&
                            Math.abs(a.y + a.height / 2 - b.y - b.height / 2) < 2;
                    });
                });
            }
            async function aim() {
                await send("touchStart", start);
                await nodes.nth(0).locator(".port-output.port-active").waitFor();
                await send("touchMove", { x: (start.x + end.x) / 2, y: (start.y + end.y) / 2 });
                await send("touchMove", end);
                await label.filter({ hasText: /→ Amplitude/ }).waitFor();
                assert.equal(await frequency.evaluate(element => element.classList.contains("port-wire-incompatible")), true);
                assert.equal(await amplitude.evaluate(element => element.classList.contains("port-wire-candidate")), true);
                await assertHighlightAlignment();
                const cursor = await center(canvas.locator(".touch-wire-cursor"));
                assert.ok(Math.abs(cursor.y - target.y) < 2 && Math.abs(cursor.x - target.x) < 2);
                assert.equal(await wires.count(), 3, "preview must not commit");
            }
            await aim();
            // Simulate a CSS-only node reflow while the finger stays still.
            // The observer must move its rings, not leave the prior highlights.
            const header = nodes.nth(1).locator(".node-header");
            await header.evaluate(element => { element.style.marginBlockEnd = "64px"; });
            const shifted = await center(amplitude.locator(".port-marker"));
            assert.ok(shifted.y > target.y + 2, "The input really moved during the drag");
            await assertHighlightAlignment();
            // Restore geometry before continuing other gesture regressions.
            await header.evaluate(element => { element.style.marginBlockEnd = ""; });
            await assertHighlightAlignment();
            await send("touchCancel");
            await label.waitFor({ state: "detached" });
            assert.equal(await wires.count(), 3);
            // A second finger on the aimed input must cancel the draft, not
            // accidentally activate the port or commit the snapped wire.
            await aim();
            await cdp.send("Input.dispatchTouchEvent", {
                type: "touchStart", touchPoints: [{ ...end, id: 1, radiusX: 10, radiusY: 10 },
                    { ...target, id: 2, radiusX: 10, radiusY: 10 }]
            });
            await page.waitForFunction(() => document.querySelector(".canvas-viewport").dataset.pinching === "true");
            await label.waitFor({ state: "detached" });
            await send("touchEnd");
            await page.waitForFunction(() => document.querySelector(".canvas-viewport").dataset.pinching === "false");
            assert.equal(await wires.count(), 3, "Pinch takeover must not commit the wire");
            await aim();
            await send("touchEnd");
            await label.waitFor({ state: "detached" });
            await page.waitForFunction(() => document.querySelectorAll(".canvas-viewport .connection-hit").length === 4);
            assert.ok((await canvas.locator(".connection-amount-badge").all()).length === 4);
            assert.ok((await canvas.locator(".connection-amount-badge").evaluateAll(badges =>
                badges.some(badge => badge.getAttribute("aria-label").includes("Amplitude")))));
        } finally {
            await page.close();
        }
    }
}
