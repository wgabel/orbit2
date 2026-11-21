# Annotations and connection visualization

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [canvas](canvas.md) · [design system](design-system.md) · [commands and events](../core/commands-and-events.md) · [persistence and history](persistence-and-history.md).

<a id="patch-documentation-and-canvas-annotations-planned-phase-619"></a>

### Patch documentation and canvas annotations (Phase 6.19.1–6.19.4 — planned)

The independent cable badge/editor (6.19.5) has shipped; it does not depend on these presentation-only annotations.

Complex patches should explain their musical intent as well as their signal topology. A **Note** appears in the Add catalog alongside other canvas items, but is a presentation-only annotation, not a `SignalNode`. It has no ports, parameter definitions, DSP emitter, presets, audition controls, observation channels or routing dependencies. Do not register an inert DSP node merely to obtain a frame or persistence. Notes and organizational metadata must be editable during playback without a graph commit, graph-history entry, audio update, compilation, phase reset or capture invalidation.

#### Note card experience

- Add Note on the current root or nested canvas using the existing visible-world placement policy, taking measured signal nodes and note cards into account. Give each note a stable ID scoped to its owning graph; allow multiple independent notes. A sensible default title and empty body are sufficient.
- Render a reusable card with a draggable header, editable title, multiline body, collapse/expand, resize handle and a small palette of named semantic accents (e.g. neutral, idea, instruction, warning). Use existing design tokens, shared selection, keyboard accessibility and world-coordinate geometry; do not duplicate the graph viewport or implement a second editor. A note has a distinguishable icon and no sockets.
- Keep editing simple: plain text with preserved line breaks, selectable/copyable text and an explicit edit/apply/cancel interaction (or equivalent accessible inline editor). Text editing owns focus; typing, selecting text, native text undo and scrolling must not drag the canvas, delete signal nodes or trigger shortcuts. Collapse retains the complete content and expanded size. Allow deletion and duplication with a fresh annotation ID; text is always stored and displayed as data, never injected as HTML, CSS, JavaScript or Faust source. A restricted Markdown renderer can be evaluated later, with raw HTML disabled.
- Use selectable notes in Fit-to-content and shared hit testing, without counting them as signal nodes or introducing fake connectors. Notes remain visible when playback is stopped and survive pan/zoom, navigation, autosave, restart, named save and JSON import/export.

Example: a note titled “Thump — pitch and noise” explains a short high-frequency onset, descending pitch, longer low-frequency decay and a filtered noise transient; the neighboring nodes remain independently editable.

#### Descriptive patch features

| Feature | Ownership and initial behavior |
|---|---|
| Note | Independent graph-scoped canvas item with title, body, bounds, accent and collapsed state. |
| Node alias and attached note | Optional per-node presentation label and description keyed by graph/node ID; keep canonical type labels and IDs visible and unchanged. Show the alias in the shared frame, breadcrumbs/selection where useful; show a consistent description affordance without replacing real parameter editors. |
| Connection label | Optional short description keyed by graph/connection ID, e.g. “slow cutoff sweep”; display in the selected-wire editor and optionally near the wire, never in its routing or amount contract. |
| Visual group | Named, colored background frame with explicit member IDs and optional group description. Unlike a composite/drone, grouping never changes ownership, creates racks or reroutes signals. Start with manual membership, resize, and explicit move-group action that translates its members once; no implicit inclusion from overlapping bounds. Render behind nodes/cables and do not intercept normal member interactions. Group deletion removes the frame only; no implicit member deletion or mute. |
| Patch overview | Optional patch-level title, description, intended sound, playing/tuning instructions and tags, stored in `PatchDocument` rather than duplicating the named-library storage key. Surface in the existing patch manager and support searching/filtering saved-patch metadata. |

Do not use grouping, labels or notes as implicit graph commands. Later, an explicit upgrade from a visual group to a real composite may be considered under Phase 7 group/ungroup with validated routing and history. Node aliases are presentation metadata, not a new registered node type or a mutation of a composite's canonical name. Existing rack port names remain the actual public interface.

#### State, persistence and history

Use immutable records for annotation data with stable IDs and graph ownership. Data-only portable records belong in `Orbit.Application.Persistence`; validation, geometry, selection, edits and immutable annotation projections belong in `Orbit.Presentation.Canvas`. Extend `CanvasStateSnapshot` and `CanvasStateService` with dedicated annotation state, selection, explicit validated add/edit/move/resize/delete/duplicate operations and a layout notification. Reuse `CanvasNavigationService` capture/restore, placement and fit geometry. The UI only translates gestures, renders a shared annotation card and handles text drafts; no browser dependency enters the presentation model. Keep annotation IDs in a separate namespace/collection from node/connection IDs and check graph ownership before edits. Use shared geometry for bounds and a generic canvas-item abstraction only as needed, without forcing signal-specific sockets/editors onto notes.

Extend `SavedCanvas` with optional annotations, aliases, connection descriptions and groups; extend `PatchDocument` with optional overview metadata. The schema-1 pattern used for visualization settings allows additive fields with empty/default values for older files; update the source-generated JSON contract and document compatibility. Validate all unique IDs, known graph/target references, finite world bounds, supported accent tokens, size/count limits and text lengths (initial proposal: title 120 characters, body 16,000, up to 200 notes per graph; bound total document size as well). Reject malformed annotation documents atomically before replacing a running workspace; do not silently discard user text. Restore missing optional fields as empty. No extra IndexedDB store is required: notes travel with patch recovery, independent named snapshots, JSON and future portable packages. The overview's patch title is distinct from the external library entry name; no automatic rename of named saves.

Keep visual changes out of `IPatchSession.Execute` and `PatchHistory`. Existing graph undo/redo remains unchanged. Add a bounded presentation-only annotation edit history (explicit undo/redo actions, one committed content change or operation rather than keystroke entries); a text draft uses native text undo before Apply. Coalesce drag/resize into one operation; do not include viewport pan/zoom in annotation history. Reuse generic immutable history mechanics if extracted, with separate history and redo boundaries. Never undo an audio setting when a user intended to undo a note edit. Selection, uncommitted drafts and history are not persisted. Mark unapplied drafts clearly and prevent navigation/closing from silently discarding them. Content edits trigger existing debounced recovery persistence on commit; users can Save explicitly.

When a signal node or connection is deleted, prune its alias/attached description and group membership, retaining graph-ID-qualified metadata in the existing bounded removal/restoration pattern for graph undo. Independent notes remain; linked annotations must not attach to an unrelated new ID. Removing a composite removes its child canvas annotations from capture and preserves them in the bounded removed-layout cache for graph undo, just like its positions and visualization settings. No orphaned layout references are emitted on export. Future copy/paste or composite template duplication must remap annotation identities and target/member IDs as part of the same copy operation; copies do not share mutable text or metadata.

~~~mermaid
flowchart TD
    N[Note or organization edit] --> L[CanvasStateService layout commit]
    L --> V[Immutable canvas snapshot]
    L --> H[Annotation history]
    V --> R[Shared GraphCanvas renderer]
    V --> P[CanvasNavigation capture]
    P --> D[PatchDocument]
    D --> S[Recovery and named saves]
    G[PatchSession graph commit] --> A[Audio engine]
    G --> L
~~~

The only direction from the signal graph to annotations is reconciliation of identity/membership. No reverse dependency into the audio path is introduced. Test root and nested note edits, independent identities, undo/redo, visible placement at pan/zoom, bounds and fit, keyboard isolation, deletion/restoration, invalid import rejection, old documents, named snapshots and reload. Verify unchanged graph revision, signal node/edge count, compiler identity and active playback generation for every annotation action, including edits while playing. Ship a small annotated example patch when the shared documentation features are ready.

<a id="connection-amount-badges-and-inline-editing-planned-phase-6195"></a>

### Connection amount badges and inline editing (Phase 6.19.5 — implemented core)

**Implemented:** [PR #1](https://github.com/wgabel/orbit2/pull/1) added badges and selection, [PR #2](https://github.com/wgabel/orbit2/pull/2) persisted per-canvas visibility, [PR #3](https://github.com/wgabel/orbit2/pull/3) anchored the shared editor. Typed event labels and unity semantics arrived in [PR #8](https://github.com/wgabel/orbit2/pull/8). Automatic zoom suppression and per-edge predicted contribution remain refinements.

Every connection already owns a signed finite `SignalConnection.Amount`. Make this configured value readable directly on its cable, without requiring the user to select each connection. The badge is a derived presentation of the committed edge, distinct from the optional human-authored connection description in Phase 6.19.2. It never introduces a second copy of the amount, a new signal port, or a modulation destination.

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
