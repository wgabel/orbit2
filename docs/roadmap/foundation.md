# Milestone register: foundations

[Architecture overview](../architecture/overview.md) · [Roadmap](overview.md)

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

## Agent documentation discovery and compliance (AGENT-DOCS)

Repository infrastructure, independent of the audio/editor phase numbering. Keep the [canonical contributor policy](../agents/contributor-instructions.md) authoritative and load only task-relevant documentation. Automated checks can verify structural compliance and declared evidence, not an agent's comprehension.

- **AGENT-DOCS-S1 — Restore native discovery (In review; committed, CI not verified)** — Depends: none; preserve root `AGENTS.md` as the only permitted root Markdown file, link canonical instructions, align Copilot and documentation conventions. Done: entry point exists; other root Markdown is rejected; links resolve. Verify: documentation checker and PR CI. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S2 — Mandatory initialization (Implemented; documentation committed, awaiting merge)** — Depends: S1 committed on this branch; define the canonical pre-work protocol for every task, including read-only and delegated work, scope changes, unavailable docs and concise initialization declaration. Done: all entry points route to the same protocol; representative tasks have clear prerequisites. Verification: waived for documentation-only changes by owner; no CI claim. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S3 — Explicit task routing (Implemented; documentation committed, awaiting merge)** — Depends: S2; define additive mandatory/recommended/reference routes using one small machine-readable configuration and distinguish semantic task classification from deterministic path-based checks. Done: route IDs are unique, all required paths resolve, cross-cutting and novel tasks have a fallback. Verification: waived for documentation-only metadata by owner; automated validation and fixtures deferred to S6. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S4 — Reduce mandatory context (Implemented; documentation committed, awaiting merge)** — Depends: S3; split global and domain-specific contributor rules without losing obligations or creating competing copies. Done: mandatory global policy stays compact; all prior rules remain reachable by appropriate routes. Verification: waived for documentation-only changes by owner; moved domain rules into routed development, music and sound-design policies. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S5 — Auditable handoffs (Implemented; documentation committed, awaiting merge)** — Depends: S4; standardize compact initialization, completion and PR evidence with selected routes, actual paths read, applicable constraints and verification limitations. Done: examples cover code, documentation, music and delegated work. Verification: waived for documentation-only changes by owner; report contract and PR template committed. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S6 — Enforce structural compliance (In review; CI pending)** — Depends: S5; validate routing and PR declarations against objectively classifiable changed paths, required documentation and exceptions; add negative fixtures and wire checks into CI. Document how repository owners can make the check required for main, and distinguish declaration from proof of reading or understanding. Verify: passing/failing fixtures and PR CI. [PR #105](https://github.com/wgabel/orbit2/pull/105).

- **AGENT-DOCS-S7 — Repository-wide documentation coverage (In review; verification pending)** — Depends: S6 existing tooling; register all active Markdown owners with narrow semantic routes and explicit historical/redirect classifications. Extend existing checks with Git inventory, forward and reverse navigation, local paths/anchors and declared semantic scenarios; warn on deep paths. Verify: focused positive/negative fixtures and full repository CI audit. See [coverage contract](../agents/compliance-reports.md#repository-wide-documentation-coverage).

Next: verify S7 repository audit and CI; resolve any remaining actual link or anchor issues before closeout. Owner may require the compliance status check on main. Owner-authorized documentation-only slices can proceed without intermediate CI or merge; S1 includes a validator change and remains unverified in review. Executable changes retain their verification requirements.

## Documentation-aware build and deploy (CI-DOCS)

- **CI-DOCS-S1 — Shared classification and safe workflow/deployment gating (Merged; [CI verified](https://github.com/wgabel/orbit2/actions/runs/45661056344), [PR #132](https://github.com/wgabel/orbit2/pull/132)):** one coordinated change preserves the existing build/compliance check names, skips runtime work only for known documentation-only paths, runs full CI on mixed or unknown changes, and permits an already built artifact to deploy across documentation-only main changes under the Pages lock. Unit/integration Git fixtures cover classification, rename/deletion, missing history, mixed PRs and deployment freshness. See [deployment contract](../getting-started/deployment.md#documentation-only-ci).
- **CI-DOCS-S2 — End-to-end workflow evidence (Verified):** [PR #133](https://github.com/wgabel/orbit2/pull/133) merged; both its [documentation PR](https://github.com/wgabel/orbit2/actions/runs/45662694426) and [main push](https://github.com/wgabel/orbit2/actions/runs/45663005951) passed lightweight checks and skipped runtime build/deploy. [PR #132](https://github.com/wgabel/orbit2/pull/132) passed full CI; its [main build and Pages deployment](https://github.com/wgabel/orbit2/actions/runs/45662482468) succeeded across the intervening documentation merge, with the deployment guard explicitly accepting unchanged runtime inputs. Regression tests cover stale runtime and failed fetch. See [verification evidence](../getting-started/deployment.md#documentation-only-ci). Additional optimization of documentation-only closeout commits on code PRs is deferred pending a proven runtime-input cache/verification design.
