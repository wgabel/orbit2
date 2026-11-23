import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const component = await readFile(new URL("../../src/Orbit.UI/Components/Canvas/NodeFrame.razor", import.meta.url), "utf8");
const canvas = await readFile(new URL("../../src/Orbit.UI/Components/Canvas/GraphCanvas.razor", import.meta.url), "utf8");
const styling = await readFile(new URL("../../src/Orbit.UI/wwwroot/css/components/node-frame.css", import.meta.url), "utf8");

test("compact node keeps all real ports mounted outside the expandable controls", () => {
    const ports = component.indexOf('<div class="node-ports">');
    assert.ok(ports > 0);
    assert.match(component, /@foreach \(var port in Node.Inputs\)/);
    assert.match(component, /@foreach \(var port in Node.Outputs\)/);
    assert.match(component, /ShowFullControls/);
    assert.match(styling, /> \.node-ports \{ order: 1; \}/);
    assert.match(styling, /> :not\(\.node-header\):not\(\.node-ports\) \{ order: 2; \}/);
    assert.match(canvas, /Expanded="@nodeExpansion\.IsExpanded\(node\)"/);
});

test("expansion has a dedicated accessible button and preserves native connector activation", () => {
    assert.match(component, /class="node-expand-toggle"/);
    assert.match(component, /aria-expanded="@\(Expanded/);
    assert.match(component, /@onclick:stopPropagation="true"/);
    assert.match(component, /OnToggleExpand\.InvokeAsync\(Node.Id\)/);
});
