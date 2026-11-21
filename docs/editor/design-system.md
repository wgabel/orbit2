# Shared components and design system

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [canvas](canvas.md) · [parameters and modulation](../core/parameters-and-modulation.md).

### Shared canvas and component standards

Every root canvas and nested canvas uses the same editor and viewport behavior. A canvas is a view over a signal graph, not a separate audio engine.

- Pan freely along both axes using a background drag (middle mouse or space + drag also works over nodes). Panning must not move graph nodes.
- Zoom with the wheel or toolbar controls, anchored at the pointer when zooming on the canvas. Show the current percentage at all times, with zoom in/out, reset to 100%, and fit-to-content controls. Use a configurable bounded zoom range (initially 25–400%).
- Keep the top controls compact: one workspace toolbar composes zoom, node catalog and root-owned playback/transport. The workspace delegates viewport actions to its active canvas reference while retaining the keyed canvas lifecycle. The canvas expands to fill remaining viewport height; counts and gesture hints sit in fixed, non-interactive viewport overlays outside the transformed graph world.
- Store viewport pan and zoom separately for each graph. Preserve them when navigating between root and child canvases. Use a single world-to-screen transform shared by nodes, ports, connections and hit testing: screen = world × zoom + pan.
- Render connections as cubic Bézier curves between the actual port anchors. Use one connection renderer for draft, connected, selected and invalid-preview states; keep wire endpoints attached during node movement, pan and zoom.
- Input ports appear on the left, outputs on the right, with accessible names and adequate hit targets. The port shape encodes signal type, not direction. Use both labels and shapes so color is never the only distinction.

#### Visual vocabulary

All nodes have a filled background and a role-specific accent color from shared semantic theme tokens. The colors below are the initial palette, not values hard-coded into individual components.

| Node role | Accent |
| --- | --- |
| Audio generator | Cyan / blue |
| Modulation source (e.g. sine used as an LFO) | Green |
| Signal processor / effect | Amber |
| Composite / drone | Violet |
| Input rack, output rack, master output | Slate |

A sine used as an LFO remains the same generator implementation: the displayed role does not create a new DSP class. Processors that produce modulation retain their processor role; port types distinguish their signals.

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
| NodeFrame | Shared node background, role accent, header, input/output layout and content slot |
| PortConnector | Render shape, label, typed endpoint, hit area and connection gesture |
| ConnectionLayer / ConnectionPath | Render and hit-test Bézier connections using shared port geometry |
| ParameterSlider | Name, unit, editable numeric base value, min/max, range slider, inline error, modulation band and effective marker |
| ValidationMessage | Consistent field-level error presentation directly below an invalid value box |

Razor markup composes reusable components; code-behind handles presentation interaction. Domain validation and application commands remain authoritative. Node-specific views supply definitions and content, rather than duplicating viewport, slider, port or connection code. Theme tokens and reusable styles live in the UI project, never in Domain.

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
