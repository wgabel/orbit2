import test from "node:test";
import assert from "node:assert/strict";
import { mountPortHints, portHelpPosition, trapNodeHelpFocus } from "../../src/Orbit.UI/wwwroot/js/canvasDom.js";

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

test("mobile popup Back cleanup is idempotent and never pops the page twice", async () => {
    const globals = ["window", "document", "history", "matchMedia"];
    const original = Object.fromEntries(globals.map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
    const install = (key, value) => Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
    const listeners = new Map();
    const historyStack = [{ page: "patch" }];
    let backCount = 0, dismissCount = 0;
    const history = {
        get state() { return historyStack.at(-1); },
        pushState(state) { historyStack.push(state); },
        back() { backCount++; historyStack.pop(); listeners.get("popstate")?.(); }
    };
    const panel = {
        addEventListener(type, cb) { listeners.set("panel-" + type, cb); },
        removeEventListener(type) { listeners.delete("panel-" + type); },
        querySelectorAll() { return []; }
    };
    const viewport = { querySelector() { return panel; } };
    const receiver = { invokeMethodAsync(method) {
        assert.equal(method, "DismissNodeHelpFromBackAsync");
        dismissCount++;
        return Promise.resolve();
    } };
    install("matchMedia", () => ({ matches: true }));
    install("history", history);
    install("document", { activeElement: null });
    install("window", {
        addEventListener(type, cb) { listeners.set(type, cb); },
        removeEventListener(type) { listeners.delete(type); }
    });
    try {
        const popup = trapNodeHelpFocus(viewport, receiver);
        assert.equal(historyStack.length, 2);
        popup.dispose();
        popup.dispose();
        assert.equal(historyStack.length, 1);
        assert.equal(backCount, 1, "explicit close must not navigate twice");
        assert.equal(dismissCount, 0);

        const second = trapNodeHelpFocus(viewport, receiver);
        assert.equal(historyStack.length, 2);
        history.back();
        assert.equal(dismissCount, 1, "Back dismisses help");
        second.dispose();
        assert.equal(backCount, 2, "closing after Back must not pop again");
        assert.equal(historyStack.length, 1);
    } finally {
        for (const key of globals) {
            if (original[key]) Object.defineProperty(globalThis, key, original[key]);
            else delete globalThis[key];
        }
    }
});
