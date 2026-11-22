# Milestone register: foundations

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [graph model](../core/graph-model.md) · [signals and ports](../core/signals-and-ports.md) · [commands and events](../core/commands-and-events.md) · [transport and timing](../audio/transport-and-timing.md) · [oscillators and noise](../audio/oscillators-and-noise.md) · [effects](../audio/effects.md) · [persistence and history](../editor/persistence-and-history.md).

<a id="phase-1--domain-primitives-graph-ownership-and-tests"></a>

### Phase 1 — Domain primitives, graph ownership and tests (implemented)

- Define SignalGraph, ISignalNode, generator/processor contracts, Parameter, PortDefinition and SignalConnection.
- Define stable ID rules, signal compatibility and fan-in/fan-out.
- Add registry with a minimal sine generator, gain and master output.
- Implement graph validation and reject dependency cycles.
- Add unit tests for the independent graph model, without requiring a browser or audio device.

Deliverable: create and validate a small graph without a UI or a real audio device.

<a id="phase-2--shared-timing-and-first-sample-rate-modulation"></a>

### Phase 2 — Shared timing and first sample-rate modulation (implemented)

- Use the actual AudioContext sample rate/frame clock.
- Introduce transport model and a time snapshot interface; add BPM synchronization incrementally.
- Create reusable Faust sine function taking a signal-valued frequency.
- Compile an initial fixed graph: LFO -> target frequency -> audible oscillator -> output.
- Support base frequency, modulation depth, start/stop with smoothing.
- Verify continuous phase and parameter behavior at 44.1/48 kHz.

Deliverable: audible two-sine modulation proof, no per-sample C#/JS message loop.

<a id="phase-3--commands-and-event-dispatch"></a>

### Phase 3 — Commands and event dispatch (implemented)

- Implemented: typed ConnectNodes, RemoveConnection, SetParameterValue, AddNode and RemoveNode commands.
- Implemented: validated single-command commits, immutable snapshots and revisioned change sets.
- Implemented: patch-session dispatcher with disposable typed and whole-change-set subscriptions, isolation and failure reporting.
- Implemented: the fixed modulation patch is constructed through the session commands.
- Implemented: independently tested canvas state projection, layout-only events, node placement, selection, pan and pointer-anchored zoom in Orbit.Presentation.
- Historical next steps — delivered in Phase 4 (canvas renderer and dynamic audio graph subscriber) and Phase 5 (multi-graph transactions and composite support).

Deliverable: a graph edit reaches independent subscribers once, with valid model state.

<a id="phase-4--flat-canvas-and-dynamic-graph-compiler"></a>

### Phase 4 — Flat canvas and dynamic graph compiler (implemented)

- Reuse the implemented JSON design token system for canvas, node, port, connection and parameter styles.
- Implemented: reusable canvas renderer with role-colored nodes, typed ports and visible Bézier connections, plus pointer pan/zoom and fit.
- Implemented: drag-to-connect, click-to-connect, delete selected connections/nodes, node placement and shared base parameter editor.
- Implemented: dependency ordering and extensible C# graph-to-Faust source compiler with stable parameter controls (sine, gain, master output), plus offline WASM probe tests.
- Implemented: on-demand graph playback from the editor, with a lazy Faust compiler worker, revision status and safe start/stop. The original fixed UI is retired.
- Implemented: live base-value updates on the running worklet, including startup queuing, revision ordering, smoothed controls and structural-change detection.
- Implemented (follow-up, [PR #75](https://github.com/wgabel/orbit2/pull/75)): [parameter-specific base-control smoothing](../core/parameters-and-modulation.md#base-control-smoothing) shortens manual oscillator frequency transitions while retaining continuous phase, modulation bandwidth and anti-click protection; rapid retune regression and device listening check.
- In review ([PR #80](https://github.com/wgabel/orbit2/pull/80)): centralized smoothing on every registered parameter definition, including the shared Pitch Control and resonator. The compiler rejects missing policies; the shared pitch bank regression checks immediate transposition/fine tune and rapid retargets.
- Implemented: automatic structural replacement, stale-revision invalidation, same-source reuse, 50 ms audio-clock crossfade and last-good-graph recovery.
- Implemented: effective parameter telemetry and animated modulation indicators; conservative ranges are estimated from the graph and live values come directly from Faust passive meters.
- Historical next step — delivered: composite nodes and nested canvases (Phase 5).
- Implemented: compile asynchronously, track graph revision and safely swap runtime.
- Implemented: tests for fan-out and multiple modulation sources.
- Delivered later in Phases 6.6–6.7: low-pass effect and mixer.

Deliverable: construct and edit a graph of two generators, a filter, mixer/output and modulation edges on the canvas while audio runs.

<a id="phase-5--composite-nodes-and-rack-interfaces"></a>

### Phase 5 — Composite nodes and rack interfaces (implemented)

- Implemented: ICompositeNode, graph-owned Input Rack and Output Rack, immutable shared boundary identities, recursion checks and nested snapshots.
- Implemented: child graph command routing, patch-wide committed snapshots, exact graph event filters and independent graph-ID-targeted canvas projections.
- Implemented: recursive DSP boundary flattening, stable graph-qualified control/meter mappings, child modulation range estimation and live child parameter edits.
- Implemented: graph navigation, breadcrumb component, cached independent canvas layouts, starter drone catalog and root playback across canvas changes.
- Implemented: editable Input Rack and Output Rack ports, projected automatically onto composite node sockets.
- Implemented: transactional add/rename/reorder/remove-port commands crossing parent/child boundaries, with one changeset and affected-graph notification filters.
- Implemented: reuse the same canvas UI for all levels.
- Implemented: selected-wire amount editing in root and nested canvases. Keep immutable edge identity and endpoints; commit a graph-local `ConnectionAmountChanged` with before/after values. Signed finite amounts support polarity inversion and silent connected routes. Direct parameter targets use their own unit; boundary/audio edges use multipliers. Existing range estimation, topology comparison and crossfade apply the revised coefficient from the committed snapshot.

Deliverable: open a drone node's internal canvas, connect external LFO to a named input, route it internally and hear the modulation.

### Phase 6 — Transport synchronization, drone presets and editor features (partial)

#### Phase 6.1–6.15 — Foundations, nodes and workspace management (implemented)

##### Timing and sound generation

- Implemented (6.1): shared patch-wide transport service, BPM editor, sample-frame-derived musical position, start/stop/resume/reset semantics, and musical divisions.
- Implemented (6.2): audio-rate shared transport phase, tempo-synchronized sine LFO, validated musical division parameter choices, root/nested and crossfade phase sharing.
- Implemented (6.3): one reusable oscillator with waveform selector (sine, triangle, square, sawtooth), phase, normalized output and structural change crossfade.
- Implemented (6.4): unified free/synchronized oscillator timing modes, conditional rate controls, dormant wires and active DSP lowering.
- Implemented (6.5): separate white/pink/brown noise generator with modulatable amplitude and structural color selection.
- Implemented (6.6): independent layered drone preset, generic composite preset catalog registration and mono weighted mixer.
- Implemented (6.7): unified resonant filter processor (low-pass, high-pass, band-pass, notch), with audio-rate cutoff and Q modulation.

##### Workspace persistence and history

- Implemented (6.8): versioned `PatchDocument` serialization, including root/nested graph identities, rack definitions, primitive parameter values and separate canvas layout. Reconstruction uses registered leaf factories and validated domain graph operations, not persisted DSP definitions.
- Implemented (6.9): browser-local IndexedDB, debounced autosave and startup recovery of all visited layouts, active canvas and BPM; playback starts stopped.
- Implemented (6.10): named independent patch snapshots, new/open/overwrite/delete, JSON import/export, validated workspace replacement while stopped.
- Implemented (6.11): bounded patch-wide graph command history with atomic undo/redo and fresh committed revisions, including nested boundaries and stable edge/node identities.

##### Audio effects

- Implemented (6.12): single-band parametric EQ with continuously modulatable frequency, gain in dB and Q, sharing the state-variable filter core.
- Implemented (6.13): mono feedback delay with free and synchronized timing, fractional delay, audio-rate modulation and live shared BPM.
- Implemented (6.14): mono algorithmic reverb with audio-rate decay, damping and wet mix, bounded feedback combs and all-pass diffusion.
- Implemented (6.15): two-voice mono chorus with modulatable rate, depth, base delay and wet mix; shared millisecond parameter unit and conservative output bounds.

Deliverable: several independently configurable drones from one reusable modular architecture.
