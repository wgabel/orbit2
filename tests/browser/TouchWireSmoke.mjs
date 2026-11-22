import assert from "node:assert/strict";

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
            await page.getByRole("button", { name: "Fit", exact: true }).click();
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
            async function aim() {
                await send("touchStart", start);
                await nodes.nth(0).locator(".port-output.port-active").waitFor();
                await send("touchMove", { x: (start.x + end.x) / 2, y: (start.y + end.y) / 2 });
                await send("touchMove", end);
                await label.filter({ hasText: /→ Amplitude/ }).waitFor();
                assert.equal(await frequency.evaluate(element => element.classList.contains("port-wire-incompatible")), true);
                assert.equal(await amplitude.evaluate(element => element.classList.contains("port-wire-candidate")), true);
                const cursor = await center(canvas.locator(".touch-wire-cursor"));
                assert.ok(Math.abs(cursor.y - target.y) < 2 && Math.abs(cursor.x - target.x) < 2);
                assert.equal(await wires.count(), 3, "preview must not commit");
            }
            await aim();
            await send("touchCancel");
            await label.waitFor({ state: "detached" });
            assert.equal(await wires.count(), 3);
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
