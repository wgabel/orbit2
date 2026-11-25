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
    assert.match(styling, /grid-template-areas: "icon name" "actions actions"/);
    assert.match(styling, /\.node-display-name \{ grid-area: name;/);
    assert.match(styling, /\.node-header-actions \{[\s\S]*?grid-area: actions;/);
    assert.match(styling, /> \.node-ports \{ order: 1; \}/);
    assert.match(styling, /> :not\(\.node-header\):not\(\.node-ports\) \{ order: 2; \}/);
    assert.match(canvas, /var expanded = nodeExpansion\.IsExpanded\(node\);/);
    assert.match(canvas, /Expanded="@expanded"/);
});

test("expansion has a dedicated accessible button and preserves native connector activation", () => {
    assert.match(component, /class="node-expand-toggle"/);
    assert.match(component, /aria-expanded="@\(Expanded/);
    assert.match(component, /@onclick:stopPropagation="true"/);
    assert.match(component, /OnToggleExpand\.InvokeAsync\(Node.Id\)/);
});

test("folded nodes mount no property editors; expanded nodes use the originals once", () => {
    assert.doesNotMatch(component, /CompactParameterValue|CompactModulationPortMap|node-modulation-slot/);
    assert.doesNotMatch(styling, /node-compact-value|node-unmapped-parameter/);
    assert.match(component, /@if \(ShowFullControls && Node\.Parameters\.Count > 0[^\n]*\)\s*\{\s*<div class="node-parameters">/);
    assert.match(component, /Node\.Parameters\.Where\(Node\.IsParameterActive\)/);
    assert.equal((component.match(/<ParameterSlider\b/g) ?? []).length, 1);
    assert.equal((component.match(/<ParameterChoiceEditor\b/g) ?? []).length, 1);
    assert.match(component, /EffectiveValue="@GetEffective\(parameter\.Id\)"/);
});

test("folded node frames receive no changing playback telemetry", () => {
    assert.match(canvas, /EffectiveValues="@effectiveProjection\.ForNode\(node.Id, expanded\)"/);
    assert.match(canvas, /Playhead="@\(expanded \? EnvelopePlayheads\.GetValueOrDefault\(node.Id\) : null\)"/);
    assert.match(canvas, /SampleRate="@\(expanded \? SampleRate : null\)"/);
});
