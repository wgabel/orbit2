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

export async function verifyCanvasContext(browser, url) {
    const desktop = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    try {
        await desktop.goto(url);
        await desktop.locator('article[data-node-id]').nth(3).waitFor();
        await fitCanvas(desktop);
        const node = desktop.locator('article[data-node-id]').first();
        await node.locator('.node-alias').click({ button: 'right' });
        await menu(desktop).getByRole('button', { name: 'Focus node', exact: true }).waitFor();
        await menu(desktop).getByRole('button', { name: 'Close contextual actions' }).press('Escape');
        await menu(desktop).waitFor({ state: 'hidden' });
        const point = await emptyPoint(desktop);
        await desktop.mouse.click(point.x, point.y, { button: 'right' });
        await menu(desktop).getByRole('button', { name: 'Add note', exact: true }).click();
        await desktop.locator('.note-card').first().waitFor();
        await desktop.locator('.note-card .note-title').first().click({ button: 'right' });
        await menu(desktop).getByRole('button', { name: 'Duplicate note' }).waitFor();
        await menu(desktop).getByRole('button', { name: 'Delete note', exact: true }).click();
        await menu(desktop).waitFor({ state: 'hidden' });
    } finally { await desktop.close(); }

    const mobile = await browser.newPage({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    const cdp = await mobile.context().newCDPSession(mobile);
    try {
        await mobile.goto(url);
        await mobile.locator('article[data-node-id]').nth(3).waitFor();
        await fitCanvas(mobile);
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
    } finally { await cdp.detach(); await mobile.close(); }
}
