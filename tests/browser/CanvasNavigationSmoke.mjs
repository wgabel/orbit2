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

async function verifyCoveredNodePinch(page) {
    // Focus a parameter-rich node, then zoom around the viewport centre until
    // there is no exposed background to start a navigation gesture on.
    const id = await canvas(page).locator('article[data-node-id]').evaluateAll(nodes => nodes
        .filter(node => node.querySelector('input[type="range"]'))
        .sort((a, b) => b.offsetHeight - a.offsetHeight)[0].dataset.nodeId);
    const node = canvas(page).locator(`[data-node-id="${id}"]`);
    await node.locator('.node-alias').click({ button: 'right' });
    await page.locator('.canvas-context-menu').getByRole('button', { name: 'Focus node', exact: true }).click();
    for (let i = 0; i < 20 && (await currentViewport(page)).zoom < 4; i++) await zoomStep(page, 1);
    await waitZoom(page, 4);
    const target = await node.evaluate(node => {
        const viewport = node.closest('.canvas-viewport').getBoundingClientRect();
        const rect = node.getBoundingClientRect();
        if (rect.left > viewport.left + 1 || rect.right < viewport.right - 1 || rect.top > viewport.top + 1 || rect.bottom < viewport.bottom - 1)
            throw Error('The zoomed node must cover the viewport');
        for (const slider of node.querySelectorAll('input[type="range"]')) {
            const bounds = slider.getBoundingClientRect();
            const left = Math.max(bounds.left, viewport.left) + 16, right = Math.min(bounds.right, viewport.right) - 16;
            const y = Math.round(Math.max(bounds.top, viewport.top) + Math.min(bounds.bottom, viewport.bottom) / 2 - Math.max(bounds.top, viewport.top) / 2);
            const a = { x: Math.round(left + 12), y }, b = { x: Math.round(right - 12), y };
            if (b.x - a.x >= 100 && [a, b].every(p => document.elementFromPoint(p.x, p.y) === slider))
                return { a, b, label: slider.getAttribute('aria-label'), value: slider.closest('.parameter-slider').querySelector('input[type="number"]').value };
        }
        throw Error('No two visible contact points on a slider in the covered node');
    });
    const slider = node.getByRole('slider', { name: target.label, exact: true });
    const number = slider.locator('xpath=ancestor::div[contains(@class,"parameter-slider")]').locator('input[type="number"]');
    const cdp = await page.context().newCDPSession(page);
    const send = (type, points) => cdp.send('Input.dispatchTouchEvent', { type, touchPoints: points.map((p, i) => ({ ...p, id: i + 1 })) });
    try {
        await send('touchStart', [target.a]);
        await page.waitForTimeout(80);
        await send('touchStart', [target.a, target.b]);
        await page.waitForFunction(() => document.querySelector('.canvas-viewport').dataset.pinching === 'true');
        const contracted = [{ x: target.a.x + 24, y: target.a.y }, { x: target.b.x - 24, y: target.b.y }];
        await send('touchMove', contracted);
        await waitZoom(page, 4 * (target.b.x - target.a.x - 48) / (target.b.x - target.a.x));
        const beforePan = await currentViewport(page);
        await send('touchMove', contracted.map(p => ({ x: p.x + 10, y: p.y + 12 })));
        await page.waitForFunction(old => {
            const m = new DOMMatrix(getComputedStyle(document.querySelector('.canvas-world')).transform);
            return Math.abs(m.e - old.x - 10) < 1 && Math.abs(m.f - old.y - 12) < 1;
        }, beforePan);
        await send('touchEnd', []);
        await page.waitForFunction(() => document.querySelector('.canvas-viewport').dataset.pinching === 'false');
        assert.equal(await number.inputValue(), target.value, 'Pinch takeover must restore the original slider value');
        assert.equal(await page.locator('.canvas-context-menu').count(), 0);
    } finally { await cdp.detach(); }
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
                await canvas(page).evaluate(element => {
                    element.navigationTestPointers = [];
                    window.addEventListener("pointerdown", event => {
                        if (event.pointerType === "touch" && element.contains(event.target))
                            element.navigationTestPointers.push({ id: event.pointerId, x: event.clientX });
                    }, true);
                });
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
                // Chromium CDP can only release all touch contacts together.
                // Exercise the partial-release PointerEvent handler using the
                // real browser's second pointer ID, then move the first contact
                // using CDP while the other contact stays still.
                await canvas(page).evaluate((element, { point, initialX }) => {
                    if (element.navigationTestPointers.length !== 2) throw Error("Expected two real touch pointers");
                    const second = element.navigationTestPointers.reduce((nearest, pointer) =>
                        Math.abs(pointer.x - initialX) < Math.abs(nearest.x - initialX) ? pointer : nearest);
                    element.dispatchEvent(new PointerEvent("pointerup", {
                        bubbles: true, pointerType: "touch", pointerId: second.id,
                        clientX: point.x, clientY: point.y, button: 0, buttons: 0
                    }));
                }, { point: panned[1], initialX: b.x });
                await page.waitForFunction(() => document.querySelector(".canvas-viewport").dataset.pinching === "false");
                assert.deepEqual(await currentViewport(page), beforeLift);
                await send("touchMove", [{ x: panned[0].x + 10, y: panned[0].y + 12 }, panned[1]]);
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
            if (size.width === 390) await verifyCoveredNodePinch(page);
        } finally { await page.close(); }
    }
}
