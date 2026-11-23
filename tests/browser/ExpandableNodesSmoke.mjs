import assert from 'node:assert/strict';
import { fitCanvas } from './CanvasNavigationSmoke.mjs';

const editable = page => page.locator('.node-frame.node-compact, .node-frame.node-expanded');

async function relativePortCoordinates(node) {
    return node.evaluate(element => {
        const bounds = element.getBoundingClientRect();
        return [...element.querySelectorAll('[data-port-id]')].map(port => {
            const rect = port.getBoundingClientRect();
            return { id: port.dataset.portId, x: rect.left + rect.width / 2 - bounds.left,
                y: rect.top + rect.height / 2 - bounds.top };
        });
    });
}

async function expand(node) {
    await node.locator('.node-expand-toggle').evaluate(button => button.click());
    await node.locator('.node-expand-toggle[aria-expanded="true"]').waitFor();
}

async function emptyCanvasPoint(page) {
    return page.locator('.canvas-viewport').evaluate(viewport => {
        const bounds = viewport.getBoundingClientRect();
        for (let y = bounds.top + 5; y < bounds.bottom - 5; y += 15)
            for (let x = bounds.left + 5; x < bounds.right - 5; x += 15)
                if (document.elementFromPoint(x, y) === viewport)
                    return { x, y };
        throw Error('Cannot find an empty canvas point');
    });
}

export async function verifyExpandableNodes(browser, url) {
    const desktop = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    try {
        await desktop.goto(url);
        await editable(desktop).first().waitFor();
        await fitCanvas(desktop);
        const ids = await editable(desktop).evaluateAll(nodes => nodes.map(node => node.dataset.nodeId));
        assert.ok(ids.length >= 2, 'Fixture needs at least two editable leaf nodes');
        assert.equal(await desktop.locator('.node-frame.node-expanded').count(), 0,
            'Editable nodes start compact');
        const first = desktop.locator(`.node-frame[data-node-id="${ids[0]}"]`);
        const second = desktop.locator(`.node-frame[data-node-id="${ids[1]}"]`);
        const before = await relativePortCoordinates(first);
        assert.ok(before.length > 0, 'All sockets are present even on compact nodes');
        await expand(first);
        const after = await relativePortCoordinates(first);
        assert.equal(after.length, before.length, 'Expansion must not remove sockets');
        for (let i = 0; i < before.length; i++) {
            assert.equal(after[i].id, before[i].id);
            assert.ok(Math.abs(after[i].y - before[i].y) < 1,
                'Expanded controls must not move cable attachment anchors');
        }
        await first.locator('.node-pin-toggle').evaluate(button => button.click());
        await first.locator('.node-pin-toggle[aria-pressed="true"]').waitFor();
        await expand(second);
        assert.ok(await first.locator('.node-expanded-body').count(), 'Pinned node remains expanded');

        // Direct editing on a compact node must not change its expansion state.
        const third = desktop.locator('.node-frame.node-compact').first();
        if (await third.count()) {
            const input = third.locator('.node-compact-parameter input[type="number"]:not(:disabled)').first();
            if (await input.count()) {
                const original = await input.inputValue();
                const { min, max } = await input.evaluate(input => ({ min: Number(input.min), max: Number(input.max) }));
                const next = String(min + (max - min) * 0.33);
                await input.fill(next);
                await input.press('Tab');
                await desktop.waitForFunction(({ id, prior }) => {
                    const node = document.querySelector(`[data-node-id="${id}"]`);
                    const first = node?.querySelector('.node-compact-parameter input[type="number"]:not(:disabled)');
                    return first && first.value !== prior;
                }, { id: await third.getAttribute('data-node-id'), prior: original });
                assert.equal(await third.locator('.node-expanded-body').count(), 0);
            }
        }
        await desktop.mouse.click(...Object.values(await emptyCanvasPoint(desktop)));
        assert.ok(await first.locator('.node-expanded-body').count(), 'Pinned node survives canvas click');
        assert.equal(await second.locator('.node-expanded-body').count(), 0,
            'Unpinned node collapses after clicking empty canvas');
        assert.equal(await desktop.locator('.node-frame').count(),
            await desktop.locator('.canvas-world [data-node-id]').count(),
            'No node is hidden when compact');
    } finally { await desktop.close(); }

    const mobile = await browser.newPage({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    try {
        await mobile.goto(url);
        await editable(mobile).first().waitFor();
        await fitCanvas(mobile);
        const node = editable(mobile).first();
        const before = await relativePortCoordinates(node);
        await node.locator('.node-expand-toggle').tap();
        await node.locator('.node-expand-toggle[aria-expanded="true"]').waitFor();
        const after = await relativePortCoordinates(node);
        assert.deepEqual(after.map(port => port.id), before.map(port => port.id));
        assert.ok(after.every((port, i) => Math.abs(port.y - before[i].y) < 1),
            'Touch expansion does not shift port anchors');
    } finally { await mobile.close(); }
}
