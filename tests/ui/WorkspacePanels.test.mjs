import test from "node:test";
import assert from "node:assert/strict";
import { mountWorkspacePanels } from "../../src/Orbit.UI/wwwroot/js/workspacePanels.js";

test("mobile sheet navigation uses one history entry, restores focus and preserves a locked draft", async () => {
    const globals = ["matchMedia", "history", "window", "MutationObserver"];
    const saved = Object.fromEntries(globals.map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
    let observer, keyHandler, backHandler, mediaHandler, pointerHandler, focused, disposed = false;
    const historyStack = [null];
    const media = { matches: true, addEventListener(_, cb) { mediaHandler = cb; }, removeEventListener() { mediaHandler = null; } };
    const history = { get state() { return historyStack.at(-1); },
        pushState(state) { historyStack.push(state); },
        back() { historyStack.pop(); queueMicrotask(() => backHandler?.()); } };
    const workspace = {
        dataset: {},
        querySelector(selector) {
            const match = selector.match(/data-mobile-(sheet|trigger)="([^"]+)"/);
            if (!match) return null;
            const focus = () => { focused = match[1] + ":" + match[2]; };
            return { focus, querySelector: () => ({ focus }) };
        },
        addEventListener(type, cb) { if (type === "keydown") keyHandler = cb; else pointerHandler = cb; },
        removeEventListener(type) { if (type === "keydown") keyHandler = null; else pointerHandler = null; }
    };
    const setPanel = panel => { workspace.dataset.mobileActivePanel = panel; observer(); };
    let dismissals = 0;
    const receiver = { async invokeMethodAsync(method) {
        assert.equal(method, "DismissMobilePanelCoreAsync");
        dismissals++;
        setPanel("");
        return true;
    } };
    const install = (key, value) => Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
    install("matchMedia", () => media);
    install("history", history);
    install("window", { addEventListener(type, cb) { if (type === "popstate") backHandler = cb; }, removeEventListener(type) { if (type === "popstate") backHandler = null; } });
    install("MutationObserver", class { constructor(cb) { observer = cb; } observe() {} disconnect() { disposed = true; } });
    const flush = () => new Promise(resolve => setImmediate(resolve));
    let controller;
    try {
        controller = mountWorkspacePanels(workspace, receiver);
        setPanel("nodes");
        assert.equal(historyStack.length, 2);
        assert.equal(focused, "sheet:nodes");
        setPanel("edit");
        assert.equal(historyStack.length, 2);
        assert.equal(focused, "sheet:edit");
        setPanel("");
        await flush();
        assert.equal(historyStack.length, 1);
        assert.equal(focused, "trigger:edit");
        setPanel("view");
        keyHandler({ key: "Escape", preventDefault() {}, stopPropagation() {} });
        await flush();
        assert.equal(dismissals, 1);
        assert.equal(historyStack.length, 1);
        assert.equal(focused, "trigger:view");
        setPanel("more");
        workspace.dataset.mobilePanelLocked = "true";
        history.back();
        await flush();
        assert.equal(dismissals, 1);
        assert.equal(historyStack.length, 2);
        assert.equal(workspace.dataset.mobileActivePanel, "more");
        workspace.dataset.mobilePanelLocked = "false";
        history.back();
        await flush();
        assert.equal(dismissals, 2);
        assert.equal(historyStack.length, 1);
        assert.equal(focused, "trigger:more");
        setPanel("transport");
        media.matches = false;
        mediaHandler();
        await flush();
        assert.equal(historyStack.length, 1);
        assert.equal(workspace.dataset.mobileActivePanel, "transport", "Resize preserves the open panel");
        setPanel("edit");
        assert.equal(historyStack.length, 1, "Desktop opens do not alter history");
        assert.equal(focused, "sheet:edit");
        pointerHandler({ target: { closest: () => null } });
        await flush();
        assert.equal(workspace.dataset.mobileActivePanel, "");
        assert.equal(focused, "sheet:edit", "Outside click must not steal canvas focus");
        setPanel("view");
        keyHandler({ key: "Escape", preventDefault() {}, stopPropagation() {} });
        await flush();
        assert.equal(focused, "trigger:view");
    } finally {
        controller?.dispose();
        for (const key of globals) {
            if (saved[key]) Object.defineProperty(globalThis, key, saved[key]);
            else delete globalThis[key];
        }
    }
    assert.equal(disposed, true);
    assert.equal(keyHandler, null);
    assert.equal(pointerHandler, null);
    assert.equal(backHandler, null);
});
