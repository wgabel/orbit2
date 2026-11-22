import test from "node:test";
import assert from "node:assert/strict";
import { measureTouchWire, watchTouchWireGeometry } from "../../src/Orbit.UI/wwwroot/js/canvasDom.js";

test("touch targets use measured client coordinates and omit covered, clipped and hidden inputs", () => {
    const button = (id, x, y, size = 8) => ({
        querySelector: () => ({ dataset: { portId: id }, getBoundingClientRect: () =>
            ({ left: x - size / 2, top: y - size / 2, width: size, height: size }) }),
        closest: () => ({ dataset: { nodeId: "node" } })
    });
    const valid = button("visible", 150, 200);
    const covered = button("covered", 160, 200);
    const clipped = button("clipped", 150, 460);
    const hidden = button("hidden", 150, 210, 0);
    const oldWindow = globalThis.window, oldDocument = globalThis.document;
    globalThis.window = { visualViewport: { offsetLeft: 0, offsetTop: 0, width: 400, height: 450 } };
    globalThis.document = { elementFromPoint: x => ({ closest: () => x === 150 ? valid : null }) };
    try {
        const viewport = {
            getBoundingClientRect: () => ({ left: 10, top: 40 }), clientLeft: 1, clientTop: 1,
            clientWidth: 360, clientHeight: 500,
            querySelectorAll: () => [valid, covered, clipped, hidden]
        };
        assert.deepEqual(measureTouchWire(viewport), {
            left: 11, top: 41, visible: { left: 11, top: 41, right: 371, bottom: 450 },
            ports: [{ nodeId: "node", portId: "visible", x: 150, y: 200 }]
        });
    } finally {
        globalThis.window = oldWindow;
        globalThis.document = oldDocument;
    }
});

test("native viewport scrolling changes the overlay origin, not its visible clip", () => {
    const oldWindow = globalThis.window, oldDocument = globalThis.document;
    const button = {
        querySelector: () => ({ dataset: { portId: "input" }, getBoundingClientRect: () =>
            ({ left: 146, top: 126, width: 8, height: 8 }) }),
        closest: () => ({ dataset: { nodeId: "node" } })
    };
    globalThis.window = { visualViewport: { offsetLeft: 0, offsetTop: 0, width: 400, height: 450 } };
    globalThis.document = { elementFromPoint: () => ({ closest: () => button }) };
    try {
        const viewport = {
            getBoundingClientRect: () => ({ left: 10, top: 40 }), clientLeft: 1, clientTop: 1,
            clientWidth: 360, clientHeight: 400, scrollLeft: 15, scrollTop: 70,
            querySelectorAll: () => [button]
        };
        const result = measureTouchWire(viewport);
        assert.deepEqual(result.visible, { left: 11, top: 41, right: 371, bottom: 441 });
        assert.equal(result.left, -4);
        assert.equal(result.top, -29);
        assert.deepEqual(result.ports, [{ nodeId: "node", portId: "input", x: 150, y: 130 }]);
        // A ring placed in the scrolled absolute overlay lands at the marker.
        assert.equal(result.ports[0].y - result.top, 159);
    } finally {
        globalThis.window = oldWindow;
        globalThis.document = oldDocument;
    }
});

test("geometry observation coalesces layout changes, measures the new markers and disconnects", () => {
    const originals = Object.fromEntries(["window", "document", "ResizeObserver", "requestAnimationFrame", "cancelAnimationFrame"]
        .map(key => [key, globalThis[key]]));
    const listeners = new Map(), frames = new Map(), messages = [], observed = [];
    let callback, disconnected = false, nextFrame = 0, y = 200;
    const button = {
        querySelector: () => ({ dataset: { portId: "input" }, getBoundingClientRect: () =>
            ({ left: 146, top: y - 4, width: 8, height: 8 }) }),
        closest: () => ({ dataset: { nodeId: "node" } })
    };
    const node = {};
    const viewport = {
        isConnected: true, getBoundingClientRect: () => ({ left: 10, top: 40 }),
        clientLeft: 1, clientTop: 1, clientWidth: 360, clientHeight: 400, scrollTop: 0,
        querySelectorAll: selector => selector === ".port-input" ? [button] : [node]
    };
    Object.assign(globalThis, {
        window: {
            visualViewport: { offsetLeft: 0, offsetTop: 0, width: 400, height: 450,
                addEventListener() {}, removeEventListener() {} },
            addEventListener: (name, listener) => listeners.set(name, listener),
            removeEventListener: name => listeners.delete(name)
        },
        document: { elementFromPoint: () => ({ closest: () => button }) },
        ResizeObserver: class {
            constructor(handler) { callback = handler; }
            observe(element) { observed.push(element); }
            disconnect() { disconnected = true; }
        },
        requestAnimationFrame: handler => { frames.set(++nextFrame, handler); return nextFrame; },
        cancelAnimationFrame: id => frames.delete(id)
    });
    const flush = () => { const work = [...frames.values()]; frames.clear(); work.forEach(handler => handler()); };
    try {
        const watcher = watchTouchWireGeometry(viewport, { invokeMethodAsync: async (...args) => messages.push(args) }, 7);
        assert.deepEqual(observed, [viewport, node]);
        callback(); callback();
        assert.equal(frames.size, 1);
        flush();
        assert.equal(messages.length, 1);
        assert.equal(messages[0][0], "TouchWireGeometryChanged");
        assert.equal(messages[0][1], 7);
        assert.equal(messages[0][2].ports[0].y, 200);
        callback(); flush();
        assert.equal(messages.length, 1, "unchanged layout does not trigger an interop callback");
        viewport.scrollTop = 70;
        y = 130;
        listeners.get("scroll")(); callback(); flush();
        assert.equal(messages.length, 2);
        assert.equal(messages[1][2].top, -29);
        assert.equal(messages[1][2].ports[0].y, 130);
        callback();
        watcher.dispose();
        flush();
        assert.equal(messages.length, 2);
        assert.equal(listeners.size, 0);
        assert.ok(disconnected);
    } finally { Object.assign(globalThis, originals); }
});
