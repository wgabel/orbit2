import test from "node:test";
import assert from "node:assert/strict";
import { portHelpPosition } from "../../src/Orbit.UI/wwwroot/js/canvasDom.js";

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
