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
    const start = component.indexOf("@if (ShowFullControls && Node.Parameters.Count > 0");
    const end = component.indexOf("@if (ShowFullControls)", start + 1);
    assert.ok(start >= 0 && end > start);
    const leafControls = component.slice(start, end);
    assert.equal((leafControls.match(/<ParameterSlider\b/g) ?? []).length, 1);
    assert.equal((leafControls.match(/<ParameterChoiceEditor\b/g) ?? []).length, 1);
    assert.equal((component.match(/<ParameterChoiceEditor\b/g) ?? []).length, 1);
    assert.match(leafControls, /EffectiveValue="@GetEffective\(parameter\.Id\)"/);
});

test("composite macros reuse the slider only on always-full composite wrappers", async () => {
    const start = component.indexOf("@if (Node.Composite is { } composite)");
    const end = component.indexOf("@if (Node.TypeKey is InputRack.NodeType or OutputRack.NodeType)", start);
    assert.ok(start >= 0 && end > start);
    const macroControls = component.slice(start, end);
    assert.match(macroControls, /@if \(composite\.Macros\.Count > 0\)\s*\{\s*<div class="node-parameters" role="group" aria-label="Composite macro controls">/);
    assert.match(macroControls, /@foreach \(var macro in composite\.Macros\)/);
    assert.equal((macroControls.match(/<ParameterSlider\b/g) ?? []).length, 1);
    assert.equal((component.match(/<ParameterSlider\b/g) ?? []).length, 2,
        "Exactly one original parameter slider declaration and one composite macro slider declaration");
    assert.match(macroControls, /<ParameterSlider @key="macro.Id" Definition="@MacroSliderDefinition\(macro\)" Value="@macro.BaseValue"/);
    assert.match(macroControls, /OnMacroEdit\.InvokeAsync\(new MacroEditRequest\(Node.Id, macro.Id, edit.Value, edit.GestureId\)\)/);
    assert.match(macroControls, /GestureEnded="OnParameterGestureEnded"/);
    assert.match(macroControls, /OnOpenComposite\.InvokeAsync\(Node.Id\)/);
    assert.match(macroControls, /OnEditMacros\.InvokeAsync\(Node.Id\)/);
    assert.match(canvas, /OnEditMacros="OnEditMacros"/);
    assert.match(canvas, /MacroOwnerships="\@\(expanded \|\| !NodeExpansionState\.IsEligible\(node\)/);
    assert.match(component, /Ownership="@MacroOwnerships\.FirstOrDefault/);
    assert.match(component, /ShowFullControls && MacroOwnerships\.Any/);
    const editor = await readFile(new URL("../../src/Orbit.UI/Components/Canvas/CompositeMacroEditor.razor", import.meta.url), "utf8");
    const editorCode = await readFile(new URL("../../src/Orbit.UI/Components/Canvas/CompositeMacroEditor.razor.cs", import.meta.url), "utf8");
    const workspace = await readFile(new URL("../../src/Orbit.UI/Components/Canvas/GraphWorkspace.Macros.cs", import.meta.url), "utf8");
    assert.match(editor, /^@using Microsoft\.AspNetCore\.Components\.Web$/m,
        "Razor DOM event handlers require the Web namespace (no shared imports)");
    assert.match(editor, /@onclick="CancelAsync"/);
    assert.match(editorCode, /CancelAsync\(\) => OnCancel\.InvokeAsync\(\)/);
    assert.match(workspace, /CancelMacroEditorAsync\(\)\s*\{\s*CloseMacroEditor\(\);\s*return InvokeAsync\(StateHasChanged\);/);
    assert.match(workspace, /CloseMacroEditor\(\);\s*StateHasChanged\(\);\s*return null;/);
    const expansion = await readFile(new URL("../../src/Orbit.Presentation/Canvas/NodeExpansionState.cs", import.meta.url), "utf8");
    assert.match(expansion, /IsEligible\(GraphNodeSnapshot node\) =>\s*node.Composite is null/);
});

test("folded node frames receive no changing playback telemetry", () => {
    assert.match(canvas, /EffectiveValues="@effectiveProjection\.ForNode\(node.Id, expanded\)"/);
    assert.match(canvas, /Playhead="@\(expanded \? EnvelopePlayheads\.GetValueOrDefault\(node.Id\) : null\)"/);
    assert.match(canvas, /SampleRate="@\(expanded \? SampleRate : null\)"/);
});

test("canvas telemetry diagnostics are opt-in and use the active graph", async () => {
    const code = await readFile(new URL("../../src/Orbit.UI/Components/Canvas/GraphCanvas.razor.cs", import.meta.url), "utf8");
    assert.ok(code.includes('Contains("orbit-canvas-telemetry=1", StringComparer.Ordinal)'));
    assert.ok(code.includes("if (CountCanvasTelemetry) RecordCanvasTelemetry();"));
    assert.ok(code.includes("Snapshot.Graph.Nodes.Select(node => node.Id).ToHashSet()"));
    for (const name of ["graph", "deliveries", "effective", "playheads", "foreign-effective", "foreign-playheads"])
        assert.ok(canvas.includes('data-telemetry-' + name + '="@(CountCanvasTelemetry ?'));
});
