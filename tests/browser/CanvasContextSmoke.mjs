import assert from 'node:assert/strict';
import { fitCanvas } from './CanvasNavigationSmoke.mjs';

const menu = page => page.locator('.canvas-context-menu');
const emptyPoint = page => page.locator('.canvas-viewport').evaluate(element => {
    const rect = element.getBoundingClientRect();
    for (let y = rect.top + 10; y < rect.bottom - 10; y += 16)
        for (let x = rect.left + 10; x < rect.right - 10; x += 16)
            if (document.elementFromPoint(x, y) === element) return { x: Math.round(x), y: Math.round(y) };
    throw new Error('No empty context gesture target');
});

async function visibleHeaderPoint(page, selector) {
    return page.locator('.canvas-viewport').evaluate((viewport, selector) => {
        const bounds = viewport.getBoundingClientRect();
        for (const header of viewport.querySelectorAll(selector)) {
            const rect = header.getBoundingClientRect();
            for (let y = Math.ceil(Math.max(rect.top, bounds.top)); y < Math.min(rect.bottom, bounds.bottom); y += 3)
                for (let x = Math.ceil(Math.max(rect.left, bounds.left)); x < Math.min(rect.right, bounds.right); x += 3) {
                    const hit = document.elementFromPoint(x, y);
                    if (hit && header.contains(hit) && !hit.closest('button, input, select, label')) return { x, y };
                }
        }
        throw Error('No exposed header for ' + selector);
    }, selector);
}

async function rightClickHeader(page, selector) {
    const point = await visibleHeaderPoint(page, selector);
    await page.mouse.click(point.x, point.y, { button: 'right' });
}

export async function verifyCanvasContext(browser, url) {
    const desktop = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    try {
        await desktop.goto(url);
        await desktop.locator('article[data-node-id]').nth(3).waitFor();
        await fitCanvas(desktop);
        // Help is intentionally separate from node selection, wiring and context actions.
        const nodeCountBeforeHelp = await desktop.locator('article[data-node-id]').count();
        await desktop.locator('.node-help-trigger').first().click();
        const helpPanel = desktop.locator('.node-help-panel');
        await helpPanel.waitFor();
        assert.ok(await helpPanel.getByText('What it does').count());
        assert.ok(await helpPanel.getByText('Try it').count());
        assert.equal(await helpPanel.getByText('Related topics').count(), 0);
        await helpPanel.getByRole('button', { name: 'Close node help' }).press('Escape');
        await helpPanel.waitFor({ state: 'hidden' });
        assert.equal(await desktop.locator('article[data-node-id]').count(), nodeCountBeforeHelp);
        const hintPort = desktop.locator('.port-connector[data-port-help]').first();
        await hintPort.hover();
        const hint = desktop.locator('.port-help-tooltip:not([hidden])');
        await hint.waitFor();
        assert.ok((await hint.innerText()).length > 25);
        await hintPort.focus();
        await hint.waitFor();
        await desktop.mouse.move(1, 1);

        await rightClickHeader(desktop, 'article[data-node-id] .node-header');
        await menu(desktop).getByRole('button', { name: 'Focus node', exact: true }).waitFor();
        const originalIds = await desktop.locator('article[data-node-id]').evaluateAll(nodes => nodes.map(node => node.dataset.nodeId));
        await menu(desktop).getByRole('button', { name: 'Duplicate node', exact: true }).click();
        await menu(desktop).waitFor({ state: 'hidden' });
        await desktop.waitForFunction(count => document.querySelectorAll('article[data-node-id]').length === count, originalIds.length + 1);
        const duplicatedId = await desktop.locator('article[data-node-id]').evaluateAll((nodes, original) =>
            nodes.map(node => node.dataset.nodeId).find(id => !original.includes(id)), originalIds);
        assert.ok(duplicatedId, 'A fresh graph node was created');
        await desktop.locator(`article[data-node-id="${duplicatedId}"].node-selected`).waitFor();
        await fitCanvas(desktop);
        await rightClickHeader(desktop, `article[data-node-id="${duplicatedId}"] .node-header`);
        await menu(desktop).getByRole('button', { name: 'Duplicate node', exact: true }).waitFor();
        await menu(desktop).getByRole('button', { name: 'Close contextual actions' }).press('Escape');
        await menu(desktop).waitFor({ state: 'hidden' });
        const point = await emptyPoint(desktop);
        await desktop.mouse.click(point.x, point.y, { button: 'right' });
        await menu(desktop).getByRole('button', { name: 'Add note', exact: true }).click();
        const note = desktop.locator('.note-card.note-selected');
        await note.waitFor();
        const noteId = await note.getAttribute('data-note-id');
        await fitCanvas(desktop);
        await rightClickHeader(desktop, `.note-card[data-note-id="${noteId}"] .note-header`);
        await menu(desktop).getByRole('button', { name: 'Duplicate note' }).waitFor();
        await menu(desktop).getByRole('button', { name: 'Delete note', exact: true }).click();
        await menu(desktop).waitFor({ state: 'hidden' });
        await desktop.locator(`[data-note-id="${noteId}"]`).waitFor({ state: 'detached' });
    } finally { await desktop.close(); }

    const mobile = await browser.newPage({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    const cdp = await mobile.context().newCDPSession(mobile);
    try {
        await mobile.goto(url);
        await mobile.locator('article[data-node-id]').nth(3).waitFor();
        await fitCanvas(mobile);
        const beforeHelp = await mobile.locator('article[data-node-id]').count();
        await mobile.locator('.node-help-trigger').first().tap();
        const helpSheet = mobile.locator('.node-help-panel');
        await helpSheet.waitFor();
        assert.equal(await mobile.locator('.port-help-tooltip:not([hidden])').count(), 0,
            'Touch should not open connector hover hints');
        assert.ok(await helpSheet.getByText('Connectors').count());
        await helpSheet.getByRole('button', { name: 'Close node help' }).tap();
        await helpSheet.waitFor({ state: 'hidden' });
        assert.equal(await mobile.locator('article[data-node-id]').count(), beforeHelp);

        const point = await emptyPoint(mobile);
        const start = () => cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...point, id: 1 }] });
        const end = () => cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
        await start();
        await menu(mobile).getByRole('button', { name: 'Add node…', exact: true }).waitFor();
        assert.ok(await menu(mobile).evaluate(element => element.classList.contains('canvas-context-sheet')));
        await end();
        await menu(mobile).waitFor();
        await menu(mobile).getByRole('button', { name: 'Close contextual actions' }).tap();
        await menu(mobile).waitFor({ state: 'hidden' });
        await start();
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: point.x + 24, y: point.y + 24, id: 1 }] });
        await mobile.waitForTimeout(550);
        assert.equal(await menu(mobile).count(), 0);
        await end();

        // Verify the actual touch path, not just a simulated desktop context menu.
        await fitCanvas(mobile);
        const header = await visibleHeaderPoint(mobile, 'article[data-node-id] .node-header');
        const initialCount = await mobile.locator('article[data-node-id]').count();
        await cdp.send('Input.dispatchTouchEvent', {
            type: 'touchStart', touchPoints: [{ ...header, id: 2 }]
        });
        await menu(mobile).getByRole('button', { name: 'Duplicate node', exact: true }).waitFor();
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
        await menu(mobile).getByRole('button', { name: 'Duplicate node', exact: true }).tap();
        await mobile.waitForFunction(previous =>
            document.querySelectorAll('article[data-node-id]').length === previous + 1, initialCount);
    } finally { await cdp.detach(); await mobile.close(); }
}
