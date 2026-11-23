import assert from "node:assert/strict";
import { fitCanvas } from "./CanvasNavigationSmoke.mjs";

// Use real Chromium touch events, not mouse emulation or synthetic DOM events.
export async function verifyTouchWire(browser, url) {
    await verifyMouseWire(browser, url);
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
            // A committed cable has two integrated, thicker rigid ends.
            // Verify the rendered SVG and token widths, not only presentation math.
            const committedCaps = canvas.locator(".connection-path .connection-rigid");
            assert.equal(await committedCaps.count(), 3);
            const capPath = await committedCaps.first().getAttribute("d");
            assert.equal((capPath?.match(/\bM\b/g) ?? []).length, 2);
            assert.equal((capPath?.match(/\bL\b/g) ?? []).length, 2);
            assert.equal(await committedCaps.first().evaluate(element => getComputedStyle(element).strokeWidth), "5px");
            assert.equal(await canvas.locator(".connection-path .connection-line:not(.connection-rigid)").first()
                .evaluate(element => getComputedStyle(element).strokeWidth), "3px");
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
                const previewCap = canvas.locator(".connection-draft.connection-rigid");
                await previewCap.waitFor();
                const previewPath = await previewCap.getAttribute("d");
                assert.equal((previewPath?.match(/\bM\b/g) ?? []).length, 1, "the free preview end has no rigid sleeve");
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
            assert.equal(await committedCaps.count(), 4, "the connected cable gets two rigid sleeves");
            assert.ok((await canvas.locator(".connection-amount-badge").all()).length === 4);
            assert.ok((await canvas.locator(".connection-amount-badge").evaluateAll(badges =>
                badges.some(badge => badge.getAttribute("aria-label").includes("Amplitude")))));
        } finally {
            await page.close();
        }
    }
}

// The same published canvas must assist a real mouse drag without a touch
// offset. Drop beside the port, outside its ordinary DOM hit target.
async function verifyMouseWire(browser, url) {
    const page = await browser.newPage({ viewport: { width: 1200, height: 800 }, hasTouch: false });
    try {
        await page.goto(url);
        const canvas = page.locator(".canvas-viewport");
        const nodes = canvas.locator("article[data-node-id]");
        await nodes.nth(3).waitFor();
        await fitCanvas(page);
        const output = nodes.nth(0).locator(".port-output .port-marker");
        const amplitude = nodes.nth(1).locator('.port-input[aria-label^="Input: Amplitude"]');
        const frequency = nodes.nth(1).locator('.port-input[aria-label^="Input: Frequency"]');
        const marker = amplitude.locator(".port-marker");
        const start = await output.evaluate(element => {
            const r = element.getBoundingClientRect();
            return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
        });
        const target = await marker.evaluate(element => {
            const r = element.getBoundingClientRect();
            const x = r.x + r.width / 2, y = r.y + r.height / 2;
            if (document.elementFromPoint(x, y)?.closest(".port-input") !== element.closest(".port-input"))
                throw Error("Amplitude marker must be visible");
            for (const distance of [12, 16, 20, 23]) {
                const point = { x: x - distance, y };
                if (document.elementFromPoint(point.x, point.y)?.closest(".port-connector") == null &&
                    element.closest(".canvas-viewport").contains(document.elementFromPoint(point.x, point.y)))
                    return { x, y, point };
            }
            throw Error("No nearby point outside the direct port hit area");
        });
        const label = canvas.locator(".touch-wire-label");
        const wires = canvas.locator(".connection-hit");
        async function aim() {
            await page.mouse.move(start.x, start.y);
            await page.mouse.down();
            await nodes.nth(0).locator(".port-output.port-active").waitFor();
            await page.mouse.move(target.point.x, target.point.y);
            await label.filter({ hasText: /→ Amplitude/ }).waitFor();
            assert.ok(await amplitude.evaluate(el => el.classList.contains("port-wire-candidate")));
            assert.ok(await frequency.evaluate(el => el.classList.contains("port-wire-incompatible")));
            const ring = canvas.locator(".touch-wire-target.is-snapped");
            await ring.waitFor();
            const position = await ring.boundingBox();
            assert.ok(Math.abs(position.x + position.width / 2 - target.x) < 2);
            assert.ok(Math.abs(position.y + position.height / 2 - target.y) < 2);
            assert.equal(await wires.count(), 3, "Mouse preview must not commit");
        }
        await aim();
        const empty = await canvas.evaluate(viewport => {
            const bounds = viewport.getBoundingClientRect();
            const rings = [...viewport.querySelectorAll(".touch-wire-target")].map(ring => ring.getBoundingClientRect());
            for (let y = bounds.top + 48; y < bounds.bottom - 48; y += 24)
                for (let x = bounds.left + 48; x < bounds.right - 48; x += 24)
                    if (document.elementFromPoint(x, y) === viewport && rings.every(r =>
                        Math.hypot(x - r.x - r.width / 2, y - r.y - r.height / 2) > 40))
                        return { x, y };
            throw Error("No empty point beyond the snap release radius");
        });
        await page.mouse.move(empty.x, empty.y);
        await label.waitFor({ state: "detached" });
        await page.mouse.up();
        await canvas.locator(".touch-wire-assist").waitFor({ state: "detached" });
        assert.equal(await wires.count(), 3, "Unsnapped mouse release must not commit");
        await aim();
        await page.mouse.up();
        await label.waitFor({ state: "detached" });
        await page.waitForFunction(() => document.querySelectorAll(".canvas-viewport .connection-hit").length === 4);
        assert.ok(await canvas.locator(".connection-amount-badge").evaluateAll(badges =>
            badges.some(badge => badge.getAttribute("aria-label").includes("Amplitude"))));
    } finally {
        await page.close();
    }
}
