import test from "node:test";
import assert from "node:assert/strict";
import { measureTouchWire } from "../../src/Orbit.UI/wwwroot/js/canvasDom.js";

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
