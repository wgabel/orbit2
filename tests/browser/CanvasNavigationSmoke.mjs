import assert from "node:assert/strict";

const canvas = page => page.locator(".canvas-viewport");
export const currentViewport = page => canvas(page).evaluate(element => {
    const matrix = new DOMMatrix(getComputedStyle(element.querySelector(".canvas-world")).transform);
    return { x: matrix.e, y: matrix.f, zoom: matrix.a };
});
const waitZoom = (page, target) => page.waitForFunction(expected => {
    const matrix = new DOMMatrix(getComputedStyle(document.querySelector(".canvas-world")).transform);
    return Math.abs(matrix.a - expected) < 0.00001;
}, target);
const waitFit = (page, sequence) => page.waitForFunction(previous =>
    document.querySelector(".canvas-viewport").dataset.fitSequence !== previous, sequence);
export async function fitCanvas(page) {
    const sequence = await canvas(page).getAttribute("data-fit-sequence");
    await canvas(page).press("f");
    await waitFit(page, sequence);
}
export async function resetCanvas(page) {
    await canvas(page).press("0");
    await page.waitForFunction(() => {
        const matrix = new DOMMatrix(getComputedStyle(document.querySelector(".canvas-world")).transform);
        return matrix.a === 1 && matrix.e === 0 && matrix.f === 0;
    });
}
export async function zoomStep(page, direction) {
    const { zoom } = await currentViewport(page);
    await canvas(page).press(direction > 0 ? "=" : "-");
    await waitZoom(page, Math.max(0.25, Math.min(4, zoom * (direction > 0 ? 1.25 : 0.8))));
}

async function emptyPoints(page) {
    return canvas(page).evaluate(element => {
        const rect = element.getBoundingClientRect();
        for (const y of [rect.top + 12, rect.bottom - 15, rect.top + rect.height / 2,
            rect.top + rect.height * 0.25, rect.top + rect.height * 0.75]) {
            const first = { x: Math.round(rect.left + rect.width * 0.35), y: Math.round(y) };
            const second = { x: Math.round(rect.left + rect.width * 0.65), y: Math.round(y) };
            if ([first, second].every(point => document.elementFromPoint(point.x, point.y) === element))
                return [first, second];
        }
        throw new Error("No clear two-finger navigation surface");
    });
}

export async function tripleTapFit(page, touch = true) {
    const point = await canvas(page).evaluate(element => {
        const rect = element.getBoundingClientRect();
        for (let y = rect.top + 8; y < rect.bottom - 6; y += 16)
            for (let x = rect.left + 8; x < rect.right - 6; x += 16)
                if (document.elementFromPoint(x, y) === element)
                    return { x: Math.round(x), y: Math.round(y) };
        throw new Error("No empty canvas point for Fit");
    });
    const sequence = await canvas(page).getAttribute("data-fit-sequence");
    if (touch) {
        const cdp = await page.context().newCDPSession(page);
        try {
            for (let i = 0; i < 3; i++) {
                await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ ...point, id: 1 }] });
                await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
            }
        } finally { await cdp.detach(); }
    } else {
        await page.mouse.click(point.x, point.y, { clickCount: 3, delay: 30 });
    }
    await waitFit(page, sequence);
}

export async function verifyCanvasNavigation(browser, url) {
    const desktop = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    try {
        await desktop.goto(url);
        await canvas(desktop).locator("article[data-node-id]").nth(3).waitFor();
        await fitCanvas(desktop);
        const fitted = await currentViewport(desktop);
        const [point] = await emptyPoints(desktop);
        await desktop.mouse.move(point.x, point.y);
        await desktop.mouse.wheel(0, -180);
        await desktop.waitForFunction(zoom =>
            new DOMMatrix(getComputedStyle(document.querySelector(".canvas-world")).transform).a > zoom, fitted.zoom);
        await tripleTapFit(desktop, false);
        const restored = await currentViewport(desktop);
        assert.ok(Math.abs(restored.zoom - fitted.zoom) < 0.00001);
        assert.equal(await desktop.locator(".canvas-toolbar, .canvas-zoom").count(), 0);
    } finally { await desktop.close(); }

    for (const size of [{ width: 390, height: 844 }, { width: 650, height: 1000 }]) {
        const page = await browser.newPage({ viewport: size, isMobile: true, hasTouch: true });
        try {
            await page.goto(url);
            await canvas(page).locator("article[data-node-id]").nth(3).waitFor();
            await fitCanvas(page);
            const fitted = await currentViewport(page);
            const positions = () => canvas(page).locator("article[data-node-id]").evaluateAll(nodes =>
                nodes.map(node => [node.dataset.nodeId, node.getAttribute("style")]));
            const before = await positions();
            const wires = await canvas(page).locator(".connection-hit").count();
            const [a, b] = await emptyPoints(page);
            const cdp = await page.context().newCDPSession(page);
            const send = (type, points) => cdp.send("Input.dispatchTouchEvent", {
                type, touchPoints: points.map((point, i) => ({ ...point, id: i + 1, radiusX: 8, radiusY: 8 }))
            });
            try {
                await send("touchStart", [a, b]);
                await page.waitForFunction(() => document.querySelector(".canvas-viewport").dataset.pinching === "true");
                const expanded = [{ x: a.x - 20, y: a.y + 24 }, { x: b.x + 20, y: b.y + 24 }];
                await send("touchMove", expanded);
                const expected = Math.min(4, fitted.zoom * (b.x - a.x + 40) / (b.x - a.x));
                await waitZoom(page, expected);
                const first = await currentViewport(page);
                const panned = expanded.map(point => ({ x: point.x + 15, y: point.y + 18 }));
                await send("touchMove", panned);
                await page.waitForFunction(old => {
                    const m = new DOMMatrix(getComputedStyle(document.querySelector(".canvas-world")).transform);
                    return Math.abs(m.e - old.x - 15) < 1 && Math.abs(m.f - old.y - 18) < 1;
                }, first);
                const beforeLift = await currentViewport(page);
                // Retain finger #1; lifting #2 must not change the transform.
                // CDP touchMove describes the complete active set; omitting id 2
                // generates its release. touchEnd itself requires an empty set.
                await send("touchMove", [panned[0]]);
                await page.waitForFunction(() => document.querySelector(".canvas-viewport").dataset.pinching === "false");
                assert.deepEqual(await currentViewport(page), beforeLift);
                await send("touchMove", [{ x: panned[0].x + 10, y: panned[0].y + 12 }]);
                await page.waitForFunction(old => {
                    const m = new DOMMatrix(getComputedStyle(document.querySelector(".canvas-world")).transform);
                    return Math.abs(m.e - old.x - 10) < 1 && Math.abs(m.f - old.y - 12) < 1;
                }, beforeLift);
                await send("touchEnd", []);
            } finally { await cdp.detach(); }
            assert.deepEqual(await positions(), before);
            assert.equal(await canvas(page).locator(".connection-hit").count(), wires);
            await tripleTapFit(page);
            assert.ok(Math.abs((await currentViewport(page)).zoom - fitted.zoom) < 0.00001);
            assert.equal(await page.locator(".canvas-toolbar, .canvas-zoom").count(), 0);
        } finally { await page.close(); }
    }
}
