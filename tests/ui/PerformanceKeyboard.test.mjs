import { test } from "node:test";
import assert from "node:assert/strict";
import { mountPerformanceKeyboard } from "../../src/Orbit.UI/wwwroot/js/performanceKeyboard.js";

class Target {
    listeners = new Map();
    addEventListener(type, fn) {
        const list = this.listeners.get(type) ?? [];
        list.push(fn);
        this.listeners.set(type, list);
    }
    removeEventListener(type, fn) {
        this.listeners.set(type, (this.listeners.get(type) ?? []).filter(item => item !== fn));
    }
    emit(type, init = {}) {
        const event = { preventDefault() {}, stopPropagation() {}, ...init };
        for (const fn of this.listeners.get(type) ?? []) fn(event);
    }
}

class ElementStub extends Target {}

function fixture() {
    const root = new ElementStub(), piano = new ElementStub();
    const window = new Target(), document = new Target(), calls = [];
    const key = note => {
        const element = new ElementStub();
        element.dataset = { midiNote: String(note) };
        element.closest = selector => selector.includes("data-midi-note") ? element : piano;
        return element;
    };
    const c = key(60), d = key(62), cs = key(61);
    root.isConnected = true;
    root.dataset = { armed: "true", stepEntry: "false" };
    root.closest = () => null;
    root.contains = target => [c, d, cs].includes(target);
    piano.contains = root.contains;
    piano.setPointerCapture = () => {};
    piano.hasPointerCapture = () => false;
    piano.releasePointerCapture = () => {};
    document.querySelector = () => null;
    document.hidden = false;
    document.elementFromPoint = () => null;
    const original = { window: globalThis.window, document: globalThis.document, Element: globalThis.Element };
    Object.assign(globalThis, { window, document, Element: ElementStub });
    const adapter = mountPerformanceKeyboard(root, { invokeMethodAsync: async (...args) => { calls.push(args); } }, ["KeyA"]);
    const pointer = { pointerId: 4, button: 0, pointerType: "touch" };
    const flush = () => new Promise(resolve => setImmediate(resolve));
    return { root, window, document, c, d, cs, calls, pointer, flush, adapter,
        dispose() { adapter.dispose(); Object.assign(globalThis, original); } };
}

test("captured pointer slides across white and black keys and releases outside", async () => {
    const f = fixture();
    try {
        f.root.emit("pointerdown", { ...f.pointer, target: f.c });
        f.document.elementFromPoint = () => f.cs;
        f.window.emit("pointermove", f.pointer);
        f.document.elementFromPoint = () => f.d;
        f.window.emit("pointermove", f.pointer);
        f.window.emit("pointerup", f.pointer);
        f.root.emit("lostpointercapture", f.pointer);
        await f.flush();
        assert.deepEqual(f.calls, [
            ["PianoPointer", 4, 60], ["PianoPointer", 4, 61], ["PianoPointer", 4, 62], ["PianoPointer", 4, null]
        ]);
    } finally { f.dispose(); }
});

test("step-entry does not repeat on slides; clear invalidates queued events and collapsed dock ignores new ones", async () => {
    const f = fixture();
    try {
        f.root.dataset.stepEntry = "true";
        f.root.emit("pointerdown", { ...f.pointer, target: f.c });
        f.document.elementFromPoint = () => f.d;
        f.window.emit("pointermove", f.pointer);
        f.window.emit("pointercancel", f.pointer);
        await f.flush();
        assert.deepEqual(f.calls, [["PianoPointer", 4, 60], ["PianoPointer", 4, null]]);
        f.root.emit("pointerdown", { ...f.pointer, target: f.c });
        f.adapter.clear();
        f.root.dataset.armed = "false";
        f.root.emit("pointerdown", { ...f.pointer, target: f.d });
        await f.flush();
        assert.equal(f.calls.length, 2);
        f.root.dataset.armed = "true";
        f.root.emit("pointerdown", { ...f.pointer, target: f.d });
        await f.flush();
        f.window.emit("blur");
        await f.flush();
        assert.deepEqual(f.calls.slice(2), [["PianoPointer", 4, 62], ["ReleasePhysicalKeys"]]);
    } finally { f.dispose(); }
});
