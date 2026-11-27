# Annotations and connection visualization

[Architecture overview](../architecture/overview.md) · [Roadmap](../roadmap/overview.md)

Related: [canvas](canvas.md) · [design system](design-system.md) · [commands and events](../core/commands-and-events.md) · [persistence and history](persistence-and-history.md).

### Flexible patch-cable appearance (implemented [PR #90](https://github.com/wgabel/orbit2/pull/90); single-cubic refinement merged [PR #93](https://github.com/wgabel/orbit2/pull/93))

The shared connection geometry has a short **rigid section** extending right from the output marker and left into the input marker (maximum 20 world units, reduced when ports are close). The remaining cable is **one smoothly sagging cubic Bézier** joining the two rigid ends, without an explicit central join or midpoint constraint. The previous paired cubics introduced a noticeable shape transition for distant nodes; [PR #93](https://github.com/wgabel/orbit2/pull/93) removes that intermediate anchor and adjusts only the two cubic handles near the rigid ends. For level ports a downward sag with one cubic requires slightly angled departure/arrival tangents; perfectly horizontal tangents at both ends would force a flat curve.

Rigid sections remain **thicker parts of the same colored cable**, not separate metal plugs: ordinary cable 3 px, rigid sleeves 5 px, selected cable 4 px, selected sleeves 6 px. A draft wire only thickens the anchored source end until it attaches. `PatchCable.Between` owns the world-space geometry for root and nested canvases. The SVG line and transparent mouse/touch hit path share the same centerline; selection, dormant/zero styles, colors, endpoint snapping, canvas pan/zoom and the DSP graph remain unchanged. Amount badges use the mathematical midpoint on the single cubic (which is not an inserted control point). Existing 12 px mouse and 24 px touch hit strokes remain independent. Regression tests cover long spans and single `C` commands, along with short, reverse, vertical and coincident endpoints.

<a id="patch-documentation-and-canvas-annotations-planned-phase-619"></a>

### Patch documentation and canvas annotations

The full annotation design has moved to [Patch documentation and canvas organization](patch-annotations.md). It covers note cards, node aliases/descriptions, connection labels, visual groups, patch overview, visibility and patch outline. This anchor remains for existing links. The connection-amount implementation contract below remains authoritative for numeric badges and editing.

<a id="connection-amount-badges-and-inline-editing-planned-phase-6195"></a>

### Connection amount badges and inline editing (Phase 6.19.5 — implemented core)

**Implemented:** [PR #1](https://github.com/wgabel/orbit2/pull/1) added badges and selection, [PR #2](https://github.com/wgabel/orbit2/pull/2) persisted per-canvas visibility, [PR #3](https://github.com/wgabel/orbit2/pull/3) anchored the shared editor. Typed event labels and unity semantics arrived in [PR #8](https://github.com/wgabel/orbit2/pull/8). Automatic zoom suppression and per-edge predicted contribution remain refinements.

Every connection already owns a signed finite `SignalConnection.Amount`. Make this configured value readable directly on its cable, without requiring the user to select each connection. The badge is a derived presentation of the committed edge, distinct from the optional human-authored connection description in Phase 6.19.2. It never introduces a second copy of the amount, a new signal port, or a modulation destination.

Phase 6.19.2 (implemented, [PR #28](https://github.com/wgabel/orbit2/pull/28)) adds authored labels to the badge tooltip and selected cable editor, with a separate Apply action and annotation history. See [patch annotations](patch-annotations.md#node-and-connection-descriptions-implementation). The existing amount draft remains independent.

| Receiving destination | Example badge | Meaning |
|---|---|---|
| Frequency / cutoff parameter | `+100 Hz` | Multiply the source by 100 Hz before adding to the base value. |
| Normalized amplitude / mix | `−0.3` | Inverted modulation coefficient, not the current effective amplitude. |
| Parametric EQ gain | `+6 dB` | Additive depth in the receiving parameter's unit, not an audio-route gain. |
| Phase parameter | `+90°` | Additive depth in degrees. |
| Audio input or intermediate rack/modulation route | `×0.5` | Dimensionless multiplier; the final parameter unit applies only at its direct input. |
| Typed event route | `Trigger`, `Gate` or `Clock` | Unity event semantics; type badge and no ordinary amount control. |

Extract shared destination/amount semantics and formatting from `ConnectionAmountEditor` into reusable presentation helpers, resolving the graph-qualified source and destination, target parameter, unit, active state and actual default amount (currently 1). Use the existing parameter unit vocabulary; display short, readable signed values, with full precision and route name available in the accessible label/inspector. A positive depth means a positive coefficient, not necessarily a positive signal; actual contribution depends on source amplitude, polarity and upstream routing. Do not label an audio multiplier as dB or present a depth as a measured live value. No special cases by node type.

**Visual placement and visibility.** Extend `ConnectionVisual` and the existing `ConnectionLayer`/`ConnectionPath` projection with the badge data and stable connection ID. Evaluate the same cubic `BezierConnection` used by the wire at its fixed midpoint in world coordinates; project using the same `CanvasViewport`. Render one reusable, tokenized HTML button aligned with the SVG cable, so pointer and keyboard behavior do not depend on SVG text hit testing. Labels follow live node movement, backwards cables, panning and zoom without separate topology or continuous DOM polling. Keep text legible and hit targets usable in screen space. Do not enlarge node frames or draw new signal connectors. Badges can overlap one another and are painted behind node frames; neither collision detection nor manual repositioning along the cable is required. A covered or hidden label remains discoverable by selecting the cable or using keyboard navigation. Avoid additional audio/animation work for labels; do not allocate a per-edge signal analyser.

Add a compact **Connection labels** toolbar choice with four modes, scoped to each root/nested canvas and persisted as an optional `SavedCanvas` preference (default All):

- **All:** show eligible configured amounts, subject to viewport and zoom limits.
- **Modified:** show amounts differing from the actual connection creation default (currently 1); selected connections also show their value.
- **Selected:** show selected connection amounts only.
- **Off:** hide all badges, retaining normal cable selection and the inspector.

**Planned refinement:** at low zoom suppress unselected badges automatically using a shared configurable threshold; retain an eligible selected badge when practical. Use existing signal colors and semantic surface/focus tokens. A negative amount displays its sign, a zero amount has a visibly faded line and `0`/`×0` label (not a distinct mute state), dormant connections keep their dashed style and show an explicit inactive indicator when labelled, and selection remains visually distinguishable. Zero and dormant are not interchangeable; amount edits and resets preserve edge identity and dormant settings. User-authored descriptions may be displayed as secondary text or a tooltip rather than competing with the numeric badge.

**Interaction and history.** Clicking a badge selects its existing connection exactly as clicking the wire does, using the same graph-qualified selection and the single `ConnectionAmountEditor`. Host that editor outside the transformed world in a bounded, accessible floating panel anchored to the cable midpoint in screen space, including when badges are hidden. Clamp it to the visible canvas and prefer placing it below or above the anchor; observe size and anchor changes without polling playback. Keep the draft and Apply/Cancel semantics, stop pointer/keyboard propagation during editing, avoid obscuring the active cable where possible, and close safely on navigation, deletion, Escape or selection change. An amount edit still dispatches `SetConnectionAmountCommand` once on explicit Apply, preserving validation, graph revision, undo/redo, range updates and existing playback replacement/crossfade. Display-mode changes, selection and opening/closing the form are layout changes only and never advance graph revision or compile DSP. The badge always displays the latest committed amount, not a partially typed input; undo/redo, imports and graph edits automatically refresh it.

**Optional individual contribution insight.** In the selected editor, reuse existing `GraphModulationRangeEstimator` source-bound / weighted-input rules (extract a shared per-edge projection if needed) to display the configured contribution interval independently of the other incoming edges. For a bipolar unit sine with depth +100 Hz and base 440 Hz, the contribution is −100…+100 Hz; 340…540 Hz is merely the resulting interval if that is the only modulation and no clamp applies. Show unknown bounds honestly and distinguish dormant/zero contributions. The combined clamped parameter range remains the existing slider's responsibility. No extra DSP capture, sample arrays or continuous updates are needed for a predicted interval. Defer measured per-edge telemetry until justified; future live readouts must distinguish configured amount, predicted contribution and actual signal.

~~~mermaid
flowchart TD
    G[Committed graph snapshot] --> P[Connection presentation]
    V[Canvas geometry and preferences] --> P
    P --> B[Amount badge on cable]
    B --> S[Select existing connection]
    S --> E[Shared amount editor]
    E --> A[Explicit Apply]
    A --> C[SetConnectionAmountCommand]
    C --> G
    C --> D[Existing history and playback]
~~~

Test exact destination units and signed amounts, zero versus dormant styling, typed-event restrictions when available, stable graph-qualified IDs, Bézier midpoint anchoring under node drag/pan/zoom, all four display modes, nested canvas persistence and legacy documents, accessible selection and keyboard isolation, no graph/audio changes for display actions, and exactly one validated graph/history entry on Apply with badge refresh after undo/redo. Verify that hidden labels do not disable cable hit testing, that long labels or dense patches do not create layout overflow, and that the floating editor remains in the visible canvas while panning, preserves the uncommitted draft, and closes without a graph command on Cancel or Escape. The initial badges can be delivered independently of notes, descriptions and groups; preserve the planned 6.19 numbering.

### Direct canvas amount slider

Eligible connections show a horizontal amount rail at the same midpoint as the original badge. The rail previews the amount locally and commits once on successful gesture completion through the existing graph command. Select the wire to open the inspector for exact values and other cable actions. Typed events retain their badges. The [slider design](connection-amount-slider.md) describes the range policy and follow-up for inline numeric entry.
