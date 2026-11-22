import assert from 'node:assert/strict';
import { currentViewport, fitCanvas, resetCanvas, zoomStep } from './CanvasNavigationSmoke.mjs';

// Seed only the saved layout of the real demo graph. No synthetic nodes,
// DOM transforms or application commands are used during the drag itself.
async function placeOffscreenDestination(page) {
    const canvas = page.locator('.canvas-viewport');
    const nodes = canvas.locator('article[data-node-id]');
    await nodes.nth(3).waitFor();
    await fitCanvas(page);
    await zoomStep(page, 1); // trigger the ordinary debounced workspace save
    const expected = await currentViewport(page);
    let saved;
    for (let i = 0; i < 80; i++) {
        saved = await page.evaluate(async () => {
            const db = await new Promise((resolve, reject) => {
                const request = indexedDB.open('orbit2-patches', 1);
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
            try {
                return await new Promise((resolve, reject) => {
                    const request = db.transaction('workspace').objectStore('workspace').get('current');
                    request.onsuccess = () => resolve(request.result ? JSON.parse(request.result) : null);
                    request.onerror = () => reject(request.error);
                });
            } finally { db.close(); }
        });
        const layout = saved?.canvasLayouts.find(item => item.graphId === saved.activeGraphId);
        if (layout && Math.abs(layout.zoom - expected.zoom) < 0.00001) break;
        saved = null;
        await page.waitForTimeout(100);
    }
    assert.ok(saved, 'Expected saved demo');
    const bounds = await canvas.boundingBox();
    const ids = await nodes.evaluateAll(elements => elements.map(node => node.dataset.nodeId));
    const layout = saved.canvasLayouts.find(item => item.graphId === saved.activeGraphId);
    Object.assign(layout, { panX: 0, panY: 0, zoom: 1, positions: ids.map((nodeId, i) => ({
        nodeId, x: i === 0 ? 18 : bounds.width + 160 + (i - 1) * 450, y: 68
    })) });
    await page.evaluate(async document => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open('orbit2-patches', 1);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        try {
            await new Promise((resolve, reject) => {
                const transaction = db.transaction('workspace', 'readwrite');
                transaction.objectStore('workspace').put(JSON.stringify(document), 'current');
                transaction.oncomplete = resolve;
                transaction.onerror = () => reject(transaction.error);
            });
        } finally { db.close(); }
    }, saved);
    await page.reload();
    await nodes.nth(3).waitFor();
    await resetCanvas(page);
    return { source: nodes.nth(0).locator('.port-output .port-marker'),
        target: nodes.nth(1).locator('.port-input[aria-label^="Input: Amplitude"] .port-marker') };
}

const center = async locator => {
    // boundingClientRect remains meaningful when the element is clipped.
    return locator.evaluate(element => {
        const rect = element.getBoundingClientRect();
        return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
    });
};

export async function verifyWireEdgePan(browser, url) {
    for (const touch of [true, false]) {
        const page = await browser.newPage({ viewport: touch ? { width: 390, height: 844 } : { width: 1100, height: 800 },
            isMobile: touch, hasTouch: touch });
        const cdp = touch ? await page.context().newCDPSession(page) : null;
        try {
            await page.goto(url);
            const { source, target } = await placeOffscreenDestination(page);
            const canvas = page.locator('.canvas-viewport');
            const wires = canvas.locator('.connection-hit');
            const bounds = await canvas.boundingBox();
            const right = Math.min(bounds.x + bounds.width, touch ? 390 : 1100);
            const bottom = Math.min(bounds.y + bounds.height, 800 + (touch ? 44 : 0));
            const touchEvent = (type, point) => cdp.send('Input.dispatchTouchEvent', {
                type, touchPoints: point ? [{ ...point, id: 1 }] : []
            });
            const move = point => touch ? touchEvent('touchMove', point) : page.mouse.move(point.x, point.y);
            const fingerY = y => touch ? (y + 60 <= bottom - 60 ? y + 60 : (y + bottom) / 2) : y;
            async function dragToEdge() {
                const start = await center(source), destination = await center(target);
                assert.ok(destination.x > right, 'Destination must begin outside viewport');
                assert.ok(start.x < right && start.y < bottom);
                assert.ok(await source.evaluate(marker => { const box = marker.getBoundingClientRect();
                    return document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2)?.closest('.port-output') === marker.closest('.port-output'); }));
                const before = await currentViewport(page);
                if (touch) await touchEvent('touchStart', start);
                else { await page.mouse.move(start.x, start.y); await page.mouse.down(); }
                await move({ x: right - 5, y: fingerY(destination.y) });
                await page.waitForFunction(old => {
                    const matrix = new DOMMatrix(getComputedStyle(document.querySelector('.canvas-world')).transform);
                    return matrix.e < old.x - 30 && matrix.a === old.zoom;
                }, before);
                assert.equal(await wires.count(), 3, 'Edge pan is a preview, not a graph edit');
                return before;
            }
            await dragToEdge();
            if (touch) await touchEvent('touchCancel');
            else { await canvas.press('Escape'); await page.mouse.up(); }
            const stopped = await currentViewport(page);
            await page.waitForTimeout(250);
            assert.deepEqual(await currentViewport(page), stopped);
            assert.equal(await wires.count(), 3);
            await resetCanvas(page);
            const before = await dragToEdge();
            // Keep holding at the edge, with no new pointer movement. The
            // destination must enter the viewport and become a real hit target.
            await target.evaluate(marker => marker.dataset.edgePanTarget = 'true');
            await page.waitForFunction(() => {
                const marker = document.querySelector('[data-edge-pan-target]');
                if (!marker) return false;
                const rect = marker.getBoundingClientRect(), x = rect.left + rect.width / 2, y = rect.top + rect.height / 2;
                return document.elementFromPoint(x, y)?.closest('.port-input') === marker.closest('.port-input');
            }, null, { timeout: 10000 });
            const end = await center(target);
            await move({ x: end.x, y: fingerY(end.y) });
            if (touch) await canvas.locator('.touch-wire-label').filter({ hasText: /→ Amplitude/ }).waitFor();
            await page.waitForTimeout(80);
            const panned = await currentViewport(page);
            assert.equal(panned.zoom, before.zoom);
            assert.ok(panned.x < before.x - 100);
            // The valid destination holds the viewport still for precise release.
            await page.waitForTimeout(250);
            assert.deepEqual(await currentViewport(page), panned);
            if (touch) await touchEvent('touchEnd'); else await page.mouse.up();
            await page.waitForFunction(() => document.querySelectorAll('.canvas-viewport .connection-hit').length === 4);
            assert.equal((await currentViewport(page)).zoom, 1);
        } finally { if (cdp) await cdp.detach(); await page.close(); }
    }
}
