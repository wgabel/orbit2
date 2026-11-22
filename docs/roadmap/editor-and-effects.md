# Milestone register: editor, analysis and effects

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [audition](../editor/audition.md) · [node presets](../editor/node-presets.md) · [visualizations](../editor/visualizations.md) · [signal observation](../audio/signal-observation.md) · [annotations and connections](../editor/annotations-and-connections.md) · [pitch shifter](../audio/pitch-shifter.md).

#### Cross-cutting follow-up items

- Deferred: deployment-version detection and safe workspace-save/reload prompt for long-lived tabs whose versioned lazy modules have been removed by a later Pages deployment.
- Partial: independent composite and individual preset reuse implemented; general graph/subgraph copies and linked/versioned template reuse remain planned.

#### Phase 6.16 — Node audition actions (implemented)

- **6.16.1 — Node mute (implemented):** explicit model/snapshot state, validated command, backward-compatible persistence, history and click-safe live controls; support leaf and composite outputs and shared M header button.
- **6.16.2 — Processor bypass (implemented):** explicit eligible dry-path capability, independent state, validated command and smoothed blend; shared B header button, mute precedence and preserved wet processing state.
- **Deferred:** connection mute and solo, global/group solo, composite bypass routing and CPU-saving branch suspension until separately justified.

Deliverable: audition or silence entire linear lanes by muting their final node, and compare effects with their dry input without deleting or rewiring anything, including during playback.

#### Phase 6.17 — Individual node presets (implemented)

- **6.17.1 — Model and catalog (implemented):** versioned immutable parameter configurations, stable IDs, registry-derived type/key validation and grouped embedded JSON resources with starter examples for all eligible node types.
- **6.17.2 — Atomic application (implemented):** validated multi-parameter graph operation and `ApplyNodePresetCommand`, one committed revision and undo entry; tested nested nodes, dormant connections, audition state and live playback updates/replacements.
- **6.17.3 — Shared selector (implemented):** compact accessible grouped `NodePresetSelector`, descriptions, base-configuration matching/modified state, shared token styling, and root/nested browser coverage; no node-specific UI.
- **6.17.4 — Built-in content (implemented):** 65 configurations with meaningful usage groups for all eight eligible generators and processors, including chorus; validate, apply and match every shipped preset in CI.
- **6.17.5 — Custom library (implemented):** persistent `Custom` group for every eligible node type, save as, explicit overwrite, rename/delete and independent instances; IndexedDB reload and workspace-switch coverage.
- **6.17.6 — Portable node presets (implemented):** versioned individual JSON import/export with validated preview, explicit copy or matching-ID replacement, transactional validation and schema-1 compatibility.

Deliverable: choose or create a grouped configuration for any eligible node without changing its identity or wiring; apply in one undoable operation during playback and reuse across patches. Custom composite graph templates (including internal wiring and exposed racks) remain separate [Phase 7.4 work](../editor/reusable-modules.md), with a consistent catalog experience.

<a id="phase-618--signal-analysis-and-visualization-planned"></a>

#### Phase 6.18 — Signal analysis and visualization (partial: 6.18.1–6.18.5 implemented)

- Partial (6.18): shared observation, visualization, integrated node plots and processor response curves implemented; dynamic tap allocation (6.18.8) is in review ([PR #20](https://github.com/wgabel/orbit2/pull/20)); passive analyzer sinks and advanced analysis remain planned.

- **6.18.1 — Observation infrastructure (implemented):** bounded Faust output taps, explicit master-channel isolation, graph-qualified manifest, generation-aware capture leases and analysis contracts. Offline/worklet regression coverage for master equivalence, nested routing, mute/bypass, leakage and bounded resources. Device-scale profiling and long-history capture remain later analysis work.
- **6.18.2 — Oscilloscope and level meter (implemented):** reusable selected-node scope, browser-side time-domain display, peak/RMS, timebase, zero-crossing trigger and bounded slow-modulation history. Known sine, clipped and low-rate modulation regression coverage.
- **Parameter editing UX refinement (implemented):** independent display-scale metadata, segmented logarithmic oscillator frequency rail and consistent modulation visualization; unchanged physical values and DSP semantics.
- **6.18.3 — Spectrum analyzer (implemented):** reusable FFT display with logarithmic frequency and calibrated peak dBFS axes, selectable window/resolution and time-based power smoothing; known fundamental/harmonic/noise tests and shared capture with the oscilloscope.
- **6.18.4 — Integrated and expanded views (implemented):** capability-driven shared `NodeVisualizationPanel`, compact collapsible waveform/spectrum/level plots, expand/mode/source controls, visible-only rendering, tokenized design and compatible per-graph layout persistence; no graph history or DSP restarts for display changes.
- **Cycle timing and pulse width refinement (implemented):** shared nominal cycle/polarity durations, seconds-based editing of free oscillator rate, approximate live period, conditional modulatable square duty with schema-1 compatibility, and oscillator-output one-configured-cycle scope with bounded history.
- **6.18.5 — Processor response curves (implemented):** pure tested response providers matching the actual parametric EQ and four filter modes, configured and instantaneous effective parameter response, sample-rate-aware logarithmic axis, stopped previews, audition notices and shared compact/expanded layout persistence; numerical and measured offline Faust parity. Implementation: [PR #4](https://github.com/wgabel/orbit2/pull/4).
- **6.18.8 — Dynamic signal tap allocation (implementation in review):** bounded, demand-driven runtime allocation to visible live plots replaces compile-time priority allocation. Releases unused bindings, shares identical signals, distinguishes routing failure from true capacity exhaustion and preserves master isolation and continuous playback. See [Signal observation and analysis](../audio/signal-observation.md#signal-observation-and-analysis-phase-618--partial). Ship before 6.18.6/6.18.7 or other new features; multi-drone device profiling remains follow-up. Design: [PR #15](https://github.com/wgabel/orbit2/pull/15). Implementation: [PR #20](https://github.com/wgabel/orbit2/pull/20).
- **6.18.6 — Dedicated analyzer sink (planned):** validated passive mono observation node for any compatible signal, reusable visualization panels, independent placement/resizing, analyzer-only reachability, nested graph persistence and undo/redo; prove audible master isolation.
- **6.18.7 — Advanced analysis (planned):** synchronized input/output spectral comparisons, bounded spectrogram history and profiling of multi-drone workloads; reuse chart infrastructure for future envelopes, automation and sample-based nodes.

Deliverable: inspect an oscillator waveform, harmonics, modulation and levels directly on the node; expand the same visualization without changing playback; see an EQ/filter's configured response when stopped and effective response during playback; attach an independent analyzer anywhere in a patch without changing its audible path.

<a id="phase-619--patch-documentation-and-canvas-organization-planned"></a>

#### Phase 6.19 — Patch documentation and canvas organization (partial: 6.19.5 core and 6.19.6 implemented)

- Partial (6.19): presentation-only note cards, node/connection descriptions, visual groups and searchable patch documentation implemented; annotation visibility/zoom detail in review; patch outline planned; connection amount badges and inline editing implemented (core). The authoritative design for annotation features is [patch-annotations.md](../editor/patch-annotations.md); numeric badge behavior remains in [connection visualization](../editor/annotations-and-connections.md).

- **6.19.1 — Note cards (implemented):** graph-scoped non-DSP annotation model, shared visible placement/fit, editable title and multiline body, semantic colors, resize/collapse/delete/duplicate, accessible text editing, bounded annotation history and additive `SavedCanvas` persistence; root/nested playback isolation regression coverage. Independent of remaining signal-analysis work; implemented early without renumbering Phase 6.18. Implementation: [PR #27](https://github.com/wgabel/orbit2/pull/27).
- **6.19.2 — Node and connection descriptions (implemented):** presentation-only aliases, attached descriptions and wire labels keyed by stable graph/node/connection IDs; shared draft/edit history with note cards, canonical names, breadcrumb aliases, cable tooltip/inspector, dormant/deleted target reconciliation, validated additive persistence and undo restoration. Implementation: [PR #28](https://github.com/wgabel/orbit2/pull/28).
- **6.19.3 — Visual groups (implemented):** explicit graph-local membership of nodes and notes, named/color-coded background regions, frame-only resize and atomic movement of members, shared annotation history, measured fit and validated additive persistence; no new signal graph, rack, audio behavior or implicit deletion of members. Implementation: [PR #30](https://github.com/wgabel/orbit2/pull/30).
- **6.19.4 — Patch overview and discovery (implemented):** optional title, description, usage instructions and tags in the portable document; protected overview draft, recovery, named snapshot metadata previews, case-insensitive text and exact tag search without graph hydration, compatible import/export, and annotated modulation and AD impact example exports. Implementation: [PR #32](https://github.com/wgabel/orbit2/pull/32).
- **6.19.5 — Connection visualization and inline editing (implemented core):** shared committed amount badge with destination units, signed/zero/dormant states, fixed Bézier midpoint behind nodes (no collision detection or manual positioning), All/Modified/Selected/Off persisted display preference, and one anchored floating amount editor with explicit Apply/Cancel and Escape. Remaining refinements: automatic zoom suppression and optional per-edge predicted contribution. Verify display-only isolation and ordinary graph undo/redo for actual amount edits in root and nested canvases. Badges may be implemented independently of other Phase 6.19 steps. Implementation: [PR #1](https://github.com/wgabel/orbit2/pull/1), [PR #2](https://github.com/wgabel/orbit2/pull/2), [PR #3](https://github.com/wgabel/orbit2/pull/3).

- **6.19.6 — Composite boundary visibility (implemented):** explicit rack direction labels, output rack initial placement in its own column, boundary frames painted above ordinary frames, nested-only Locate output rack action, measured viewport fitting and preservation of saved positions. Verify the Impact preset's mirrored audio port and real internal connection, existing saved positions and browser navigation. Implementation: [PR #18](https://github.com/wgabel/orbit2/pull/18).

- **6.19.7 — Annotation visibility and zoom-dependent detail (in review):** clean performance view with Show/Titles only/Hide, persisted per-canvas preference, screen-scaled markers and zoom hysteresis, protected text drafts, hidden-item picker and temporary reveal/fit, signal-only fit, preserved authored geometry/manual collapse and no change to DSP or amount preferences.
- **6.19.8 — Patch outline and navigation (planned):** lightweight searchable hierarchy of graphs, groups, composites, named nodes and notes; graph-qualified focus/fit and temporary reveal of hidden targets, nested navigation, reconciliation after edits/undo/import, accessible keyboard selection and no graph/audio changes.

Deliverable: a musician can open a previously saved or shared complex patch, identify its sections, understand modulation intent and recover design notes without altering sound or reading source code.

#### Phase 6.20 — Audio pitch shifting (planned)

- **6.20.1 — Pitch shifter processor (planned):** mono post-mixer transposition with semitone/cents and wet mix controls, shared pitch-ratio mathematics, bounded overlapping-window Faust resampling, click-safe zero shift, standard audition and live parameter updates; test polyphonic spectral ratios, duration, latency, artifacts and resource bounds at 44.1/48 kHz. Ship independently of oscillator pitch tracking or typed note events.
- **6.20.2 — Quality and usage (planned):** tune window/interpolation choices against multi-tone and transient fixtures, measure latency and CPU cost, document dry/wet and parallel-path limitations, include octave/fifth/detune presets and a reusable mixed-drone example. Investigate higher-quality algorithms only if measured needs justify them.

Deliverable: transpose an entire mixed oscillator bank or another audio source using a single downstream node, with documented latency and quality tradeoffs; use Phase 7.2.2 source-level pitch tracking when generators are individually accessible and clean transposition is preferred.
