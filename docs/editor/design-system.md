# Shared components and design system

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [canvas](canvas.md) · [parameters and modulation](../core/parameters-and-modulation.md).

### Shared canvas and component standards

Every root canvas and nested canvas uses the same editor and viewport behavior. A canvas is a view over a signal graph, not a separate audio engine.

Non-signal note cards, group frames, authored labels and planned responsive visibility follow the same semantic theme tokens and geometry; see [patch annotations](patch-annotations.md) for their distinct interaction contract.

- Pan freely along both axes using a background drag (middle mouse or space + drag also works over nodes). Panning must not move graph nodes.
- Zoom with the wheel or toolbar controls, anchored at the pointer when zooming on the canvas. Show the current percentage at all times, with zoom in/out, reset to 100%, and fit-to-content controls. Use a configurable bounded zoom range (initially 25–400%).
- Keep the top controls compact. On desktop the shared workspace toolbar composes zoom, node catalog and root-owned playback/transport. Phase 6.21.1 (implemented, [PR #60](https://github.com/wgabel/orbit2/pull/60)) adds a CSS-responsive shell for narrow screens and short coarse-pointer landscape screens: two tokenized 44 CSS-pixel rows, compact breadcrumbs/BPM/Play/Stop, floating zoom, and on-demand existing controls behind Nodes, Outline, Edit, View and More. The same mounted components and graph state are reused on either layout. The workspace delegates viewport actions to its active canvas reference while retaining the keyed canvas lifecycle. The canvas fills the remaining height; counts remain non-interactive viewport overlays, and desktop-only gesture hints are hidden on mobile. Phase 6.21.2 (in review, [PR #61](https://github.com/wgabel/orbit2/pull/61)) adds named mobile sheet headings, touch-sized close controls, contextual Edit actions, selection tools, focus restoration and single-entry Escape/Back navigation. See [Phase 6.21](../roadmap/editor-and-effects.md#phase-621--canvas-first-workspace-and-node-library-planned) for the planned panel refinements and node library.
- Store viewport pan and zoom separately for each graph. Preserve them when navigating between root and child canvases. Use a single world-to-screen transform shared by nodes, ports, connections and hit testing: screen = world × zoom + pan.
- Render connections as cubic Bézier curves between the actual port anchors. Use one connection renderer for draft, connected, selected and invalid-preview states; keep wire endpoints attached during node movement, pan and zoom.
- Input ports appear on the left, outputs on the right, with accessible names and adequate hit targets. The port shape encodes signal type, not direction. Use both labels and shapes so color is never the only distinction.

#### Visual vocabulary

All nodes have a filled background and a category-specific accent from shared semantic theme tokens. [PR #62](https://github.com/wgabel/orbit2/pull/62) (6.21.3, in review) introduces the presentation-owned `NodeAppearanceCatalog` and shared `NodeAppearanceIcon`. The library, node frames, outline, preset selector and composite breadcrumbs reuse this catalog. Category colors have their own theme keys; the existing role colors and signal/port semantics are unchanged.

| Node category | Accent |
| --- | --- |
| Audio generators | Cyan / blue |
| Modulation | Mint |
| Effects | Purple |
| Sequencing & timing | Amber |
| Processing & mixing | Orange |
| Composites & instruments | Pink |
| Input / output & canvas | Slate |

A sine used as an LFO remains the same generator implementation: its displayed category changes to modulation while retaining its waveform icon. Processor roles remain independent from category accents. Instantiated composites use their common composite type icon; registered templates have recognizable instrument-family icons in the library. The catalog includes a fallback for new registered types; add specific metadata for each new type. Icons are decorative for assistive technology because labels communicate the same meaning.

| Signal kind | Port shape |
| --- | --- |
| Audio | Circle |
| Modulation / continuous control | Diamond |
| Trigger | Triangle |
| Gate | Square |
| Clock | Hexagon |

Map these presentation shapes to the existing domain port kinds; introduce future kinds only when their domain semantics exist. Audio channel count is a label, not a separate signal kind.

#### Design token and theme system

The design system is a presentation-owned boundary. Static visual values must have a single JSON source, independent of markup, application commands and audio processing. Each root canvas and nested canvas uses the same semantic tokens and shared component styles.

| Level | Responsibility | Example |
| --- | --- | --- |
| Primitive | Raw shared palette, spacing, font sizes, radii, opacity and motion | `primitive.color.ink-950` |
| Semantic | Visual purpose independent of the widget | `semantic.color.generator` |
| Component | Reusable widget dimensions or semantic aliases | `component.node.min-width`, `component.slider.modulation-color` |

The base file is `src/Orbit.UI/DesignSystem/design-tokens.json`; `Themes/dark.json` and `Themes/light.json` supply the same semantic color keys. A theme cannot redefine component sizes: both themes share the same interaction geometry. Domain role colors, selection/focus, errors and modulation use semantic keys. New UI components should reference those keys instead of duplicating their values.

~~~mermaid
flowchart TD
    J["Shared token JSON"] --> G["Build-time generator"]
    T["Dark and light theme JSON"] --> G
    G --> V["Generated CSS variables"]
    V --> B["Base CSS"]
    V --> C["Component styles"]
    B --> R["Root and nested Blazor views"]
    C --> R
~~~

JSON aliases use `"{primitive.color.generator-blue}"` syntax and compile into CSS variable references. The deterministic generator validates token names, flattened name collisions, missing and circular references, and matching theme keys. It emits `wwwroot/css/tokens.css`, which is tracked and loaded before the base and component styles. CI verifies regenerated output and all published CSS assets; there is no runtime JSON reader or styling framework. Dark is the default, light can be activated by setting `data-theme="light"` on the HTML root. Theme selection UI/persistence is a future concern.

Example use in a shared node stylesheet:

~~~css
.node-frame {
    min-width: var(--component-node-min-width);
    border-radius: var(--component-node-border-radius);
    background: var(--semantic-color-surface);
}
.node-frame--generator {
    border-color: var(--semantic-color-generator);
}
~~~

Keep layout structure in CSS and markup, with numeric styling constants in JSON. Reuse token-based component styles for repeated UI controls (the existing button, slider and validation message are migrated). Only computed presentation geometry such as port coordinates, canvas transforms, live modulation percentages and Bézier path points can be provided dynamically by code. Parameter bounds, units, sample rates, transport settings, zoom interaction limits and graph rules are functional values, not visual tokens.

#### Reusable presentation building blocks

| Component | Single responsibility |
| --- | --- |
| GraphCanvas | Compose viewport, graph nodes, connections and local interaction state for any graph |
| CanvasViewport | Coordinate conversion, pan, zoom and viewport state |
| CanvasToolbar | Zoom controls, percentage readout, fit and reset |
| PerformanceMonitor | Shared passive toolbar indicator and expandable diagnostics; token-based panel, local reset; independent of graph/canvas state (7.3.1 in review, [PR #53](https://github.com/wgabel/orbit2/pull/53)) |
| NodeFrame | Shared node background, category accent, header, input/output layout and content slot |
| NoteCard / NoteEditor | Presentation-only text, semantic accent, header, resize, collapse and isolated unscaled text draft (6.19.1, implemented) |
| DescriptionEditor | Unscaled node alias/description or cable label editor, shared annotation draft lifecycle (6.19.2, implemented) |
| VisualGroupFrame / VisualGroupEditor | Background frame, header drag, frame-only resize, explicit membership and unscaled metadata draft (6.19.3, implemented) |
| PatchOverviewEditor / PatchOverviewSummary | Patch-wide protected metadata draft and read-only summary, reused by PatchManager for active and saved documents (6.19.4, implemented) |
| AnnotationMarker / AnnotationDetailPolicy | Shared screen-scaled title/marker detail and per-canvas visibility, with selection reveal and stable authored geometry (6.19.7, implemented) |
| PatchOutlinePanel / PatchOutline | Searchable, bounded hierarchy of committed graph-qualified targets, with explicit locate/open actions (6.19.8, implemented) |
| PortConnector | Render shape, label, typed endpoint, hit area and connection gesture |
| ConnectionLayer / ConnectionPath | Render and hit-test Bézier connections using shared port geometry |
| ParameterSlider | Name, unit, editable numeric base value, min/max, range slider, inline error, modulation band and effective marker |
| ValidationMessage | Consistent field-level error presentation directly below an invalid value box |

Razor markup composes reusable components; code-behind handles presentation interaction. Domain validation and application commands remain authoritative. Node-specific views supply definitions and content, rather than duplicating viewport, slider, port or connection code. Theme tokens and reusable styles live in the UI project, never in Domain. Razor components using DOM event directives such as `@onclick` must import `Microsoft.AspNetCore.Components.Web`; without that import a clean build can emit literal, inert HTML attributes. Verify actual event behavior in the published editor, as in the outline regression in [PR #39](https://github.com/wgabel/orbit2/pull/39).

#### Parameter editor contract

A parameter's immutable definition supplies its name, unit (Hz, ratio, dB, etc.), minimum, maximum, modulation scale and capability. The parameter instance supplies its editable base value. A range slider and number box edit that same value; invalid, non-finite or out-of-range input stays in the field with an explanatory message beneath it and is not committed to the graph. Slider display scaling is independent of DSP modulation semantics.

Modulation adds two read-only visual values to the editor:

- A colored interval on the rail displays the predicted effective-value range; it is computed from the known modulation bounds and routing amounts, not from an animation timer. Unknown bounds are displayed as unknown.
- A moving marker and numeric live readout display the actual effective value sampled from the processing engine. The base thumb and numeric input stay stable.

For example, a frequency base of 440 Hz with a sine LFO at ±100 Hz displays a 340–540 Hz band, while the live marker follows the DSP frequency. Where modulation exceeds the editable base limits, the rail is clipped and the full effective bounds and current value remain visible in text.

Live telemetry is a passive, display-rate snapshot (around 20–30 Hz initially, batched per graph when scaled up), not an application event for every sample. Audio processing never waits for the UI. Do not persist telemetry or write it back to editable parameter values. A stopped or unavailable processor has no live marker.

**Stable modulation readouts.** The shared parameter slider renders Modulation and Live in fixed shrinkable columns, with each label above its single-line value. The predicted range receives more width than the instantaneous value. Tabular digits, non-wrapping text and constrained grid tracks prevent changing telemetry precision or magnitude from changing the node height or width; exceptional long values ellipsize with the full text retained in a tooltip and accessible readout. Unknown and stopped values occupy the same slots, and live outputs do not announce each telemetry update. Browser regression tests measure actual readout and node height with variable-length values and changing live telemetry.

**Frequency display scale (implemented).** `ParameterDefinition.DisplayScale` is optional immutable metadata, validated by `Parameter`; absent metadata preserves the original linear rail. A display scale contains strictly increasing positive physical values and normalized positions, from 0 to 1, spanning the parameter's physical bounds. Consecutive anchors define logarithmic segments (two anchors provide a conventional logarithmic rail). `ParameterSliderScale` in Orbit.Presentation provides forward and inverse mappings. `ParameterSlider` uses normalized native range input for non-linear scales, translates edits to physical values before validation/commands, and maps the base thumb, predicted interval endpoints and effective marker through the same forward mapping. Numeric edits remain exact; newly dragged values are rounded to six significant digits for readable precision. The normalized range provides the physical value through accessible value text. Marker percentages are clamped at rail limits, not written back to model or DSP. The oscillator's anchors are (0%, 0.01 Hz), (20%, 40 Hz), (85%, 1,000 Hz), (100%, 20,000 Hz), preserving the full range while assigning 65% of travel to the most-used musical band. `ParameterScale.Linear` still specifies additive Hz modulation and does not change; display metadata is excluded from DSP topology comparison and is not serialized in patches or presets. No node-specific UI is required.

~~~mermaid
flowchart TD
    R[Normalized slider position] -->|Inverse display scale| V[Physical base value]
    V --> D[DSP modulation in physical units]
    V --> M[Shared forward display scale]
    D --> E[Effective value]
    E --> M
    B[Predicted physical bounds] --> M
    M --> T[Thumb, band and marker]
~~~

The shared ParameterSlider is implemented in the editable node frames. The graph editor supports live base-value edits and effective modulation meters for reachable connected parameters. `GraphModulationRangeEstimator` calculates conservative parameter intervals from the committed snapshot using optional signal-bound rules on the registered DSP emitters. The weighted inputs are summed and clamped to parameter limits; unknown bounds propagate rather than inventing a range. `FaustNodeContext` inserts a passive meter after the effective value clamp, and the playback service maps its stable name to a parameter address. `GraphPlaybackControls` polls the service at 25 Hz; the workspace accepts only the matching graph revision and passes display data to the shared node frames. The base thumb is independent of the marker. Disconnected nodes can show predicted bounds but have no live sample.

Phase 4 implements the shared canvas, node frames, typed ports and connection renderer; Phase 5 reuses them for nested canvases.
