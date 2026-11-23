import test from "node:test";
import assert from "node:assert/strict";
import { mountPortHints, portHelpPosition } from "../../src/Orbit.UI/wwwroot/js/canvasDom.js";

test("port help is clamped in screen-space, not scaled node coordinates", () => {
    const bounds = { width: 390, height: 650 };
    assert.deepEqual(portHelpPosition({ left: 80, top: 70, bottom: 94 },
        bounds, { width: 250, height: 90 }, 8), { left: 80, top: 102 });

    const bottomRight = portHelpPosition({ left: 380, top: 610, bottom: 634 },
        bounds, { width: 250, height: 90 }, 8);
    assert.deepEqual(bottomRight, { left: 132, top: 512 });

    const tiny = portHelpPosition({ left: 10, top: 15, bottom: 20 },
        { width: 70, height: 60 }, { width: 55, height: 45 }, 8);
    assert.ok(tiny.left >= 8 && tiny.top >= 8);
});

test("re-entrant canvas mounting reuses one tooltip and disposes cleanly", () => {
    const previousWindow = globalThis.window;
    let appended = 0, removed = 0;
    globalThis.window = { addEventListener() {}, removeEventListener() {} };
    const element = () => ({
        hidden: false, dataset: {},
        setAttribute() {}, append() {}, remove() { removed++; }
    });
    const viewport = {
        ownerDocument: { createElement: element },
        append() { appended++; },
        addEventListener() {}, removeEventListener() {}
    };
    try {
        const first = mountPortHints(viewport);
        const repeat = mountPortHints(viewport);
        assert.strictEqual(repeat, first);
        assert.equal(appended, 1, "only one hint surface may be created");
        first.dispose();
        first.dispose();
        assert.equal(removed, 1, "disposal is safe to repeat");
        const afterDispose = mountPortHints(viewport);
        assert.notStrictEqual(afterDispose, first);
        assert.equal(appended, 2);
        afterDispose.dispose();
    } finally {
        if (previousWindow === undefined) delete globalThis.window;
        else globalThis.window = previousWindow;
    }
});
