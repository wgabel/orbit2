import test from "node:test";
import assert from "node:assert/strict";
import { connectionEditorPosition } from "../../src/Orbit.UI/wwwroot/js/canvasDom.js";

const bounds = { left: 0, top: 0, right: 800, bottom: 600 };
const panel = { width: 320, height: 180 };
const gap = 16;

test("connection editor opens below the cable without covering its center", () => {
    assert.deepEqual(connectionEditorPosition({ x: 400, y: 200 }, bounds, panel, gap), { x: 240, y: 216 });
});

test("connection editor flips above the cable near the bottom edge", () => {
    assert.deepEqual(connectionEditorPosition({ x: 400, y: 550 }, bounds, panel, gap), { x: 240, y: 354 });
});

test("connection editor clamps off-screen cables and partially visible canvases", () => {
    const visible = { left: 100, top: 60, right: 750, bottom: 520 };
    assert.deepEqual(connectionEditorPosition({ x: -400, y: -300 }, visible, panel, gap), { x: 116, y: 76 });
    assert.deepEqual(connectionEditorPosition({ x: 1400, y: 900 }, visible, panel, gap), { x: 414, y: 324 });
});

test("connection editor remains inside a viewport tightly fitting its measured size", () => {
    const small = { left: 0, top: 0, right: 352, bottom: 212 };
    assert.deepEqual(connectionEditorPosition({ x: 176, y: 106 }, small, panel, gap), { x: 16, y: 16 });
});
