# Milestone register: advanced functionality

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [envelopes](../audio/envelopes.md) · [pitch control](../instruments/pitch-control.md) · [note sequencer](../instruments/note-sequencer.md) · [physical modelling](../instruments/physical-modelling.md) · [polyphony](../instruments/polyphony.md) · [sample assets](../audio/sample-assets.md) · [performance](../../performance.md).

<a id="phase-7--advanced-functionality-and-performance"></a>

### Phase 7 — Advanced functionality and performance (partial)

- Partial: filters, EQ, delay, chorus and reverb implemented in Phase 6; flanger, compression and further processing planned.
- Planned: random, step sequencers, automation, explicit feedback; reuse the Phase 7.1 event and envelope infrastructure.
- Partial: shared musical pitch control implemented (7.2.2); note control, resonator and modal instrument templates planned; reuse Phase 7.1 articulations and existing composite graphs (Phase 7.2).
- Additional routing adapters and channel handling; extend Phase 6.18 observation infrastructure to multichannel signals and future node types.
- Group/ungroup nodes; versioned reusable graph definitions and a custom Module Library (Phase 7.4).
- Improve graph swap continuity, compile cache and performance profiling.
- Validate original Orbit's clicking/underrun concerns with large patches.

Deliverable: a scalable modular environment, adding functionality through node definitions rather than hardcoded editors.

<a id="phase-71--envelope-generators-and-rhythmic-triggers-planned"></a>

#### Phase 7.1 — Envelope generators and rhythmic triggers (partial: 7.1.1–7.1.5 implemented)

- **7.1.1 — Oscillator-driven AD envelope (implemented):** reusable sample-rate edge detector, continuous oscillator trigger input, shared AD segment state machine, configurable durations/curves, unipolar range prediction, shared parameter UI, five presets, editable impact patch and compiled DSP timing/continuity tests. No dedicated trigger source required. Implementation: [PR #7](https://github.com/wgabel/orbit2/pull/7).
- **7.1.2 — Typed rhythmic ports and adapters (implemented):** Trigger, Gate and Clock contracts, explicit continuous-signal edge/gate adapters, single-source unity event links, nested rack/flattening/validation/persistence support and tokenized event sockets; preserve the existing oscillator trigger route with conditional source selection. Implementation: [PR #8](https://github.com/wgabel/orbit2/pull/8).
- **7.1.3 — Rhythmic sources (implemented — PR #10):** manual trigger and gate runtime actions, free/BPM clock using shared transport, gate duration and trigger fan-out; no graph revision or recompilation on events; generation-safe worklet delivery and timing tests. Implementation: [PR #10](https://github.com/wgabel/orbit2/pull/10).
- **7.1.4 — AR, ADSR and Loop (implemented — PR #12):** gate-aware stage transitions, sustain/release, repeat/rest and rapid retrigger continuity using the same envelope node and conditional parameters; compatible AD migration, initial presets and multi-rate DSP tests. Implementation: [PR #12](https://github.com/wgabel/orbit2/pull/12).
- **7.1.5 — Envelope visualization (implemented):** configured curve, live measured output and DSP stage/progress playhead, compact/expanded shared visualization and portable view preferences; no graph mutation or DSP dependency on display. Implementation: [PR #14](https://github.com/wgabel/orbit2/pull/14).
- **7.1.6 — Presets and worked patches (partial — AD presets and impact composite delivered in 7.1.1, initial AR/ADSR/Loop presets in 7.1.4):** grouped AD/percussion, AR/swell, ADSR/voice, rhythmic modulation presets and independently routed multi-envelope impact examples, validated in CI.
- **7.1.7 — Custom multi-point envelopes (planned):** bounded versioned breakpoint definitions, reusable segment evaluation, atomic edit/history and compatible persistence; documented loop and trigger behavior.

Deliverable: an oscillator can repeatedly trigger a percussion envelope without any new timing node; later explicit trigger, gate and clock nodes can drive several independently shaped envelopes or other event consumers in sample-aligned fashion. Phase 6.18 visualization remains the shared display foundation; sequencers and sample playback reuse the event contracts.

<a id="phase-72--musical-instrument-synthesis-and-physical-modelling-planned"></a>

#### Phase 7.2 — Musical instrument synthesis and physical modelling (partial: 7.2.2 implemented)

- **7.2.1 — Existing-node instrument templates (in review):** five editable organ, flute, brass, bowed-string and didgeridoo composites using harmonic oscillators, filtered noise, mixers, effects and conservative gain staging. Shared relative semitone pitch and gain-offset racks; independent instances and ordinary patch persistence. See [instrument recipes](../examples/instrument-recipes.md). These are sustained approximations, not yet note-triggered or polyphonic instruments.
- **7.2.2 — Musical pitch tracking (implemented for oscillators):** pure note/Hz/semitone/cents conversion, opt-in pitch tracking and conditional semitone inputs, shared `Pitch Control` source with transpose/fine tune and presets, audio-rate mapping and range prediction, effective Hz telemetry, stable intervals, compatible migration and nested fan-out. Independent of typed note events and Phase 6.20; future resonators reuse the convention. Implementation: [PR #6](https://github.com/wgabel/orbit2/pull/6).
- **7.2.3 — Monophonic note source (planned):** sample-aligned pitch/gate/trigger/velocity outputs, on-screen and laptop keyboard note actions through a shared explicit performance-input adapter (reused by 7.2.8), and generic multi-output lowering where required; reuse Phase 7.1 event delivery and ADSR, no recompilation or graph history on note events. Define release/retrigger, legato and focus-loss behavior.
- **7.2.4 — Resonator processor (planned):** reusable damped-mode Faust emitter with independently modulatable frequency, decay/T60 and excitation/output gain, stable sample-rate-aware coefficient math, compatible persistence/presets and impulse/spectrum/stability regression tests. Independent of note-source work; ordinary signals can excite it.
- **7.2.5 — Modal synthesis (planned):** parallel resonator composite first, then bounded configurable modal bank if needed; short AD/noise or pulse excitation, independent tuned/inharmonic mode ratios and decays, shared pitch and velocity, handpan-like, bell, gong and wood templates, response and spectrum views.
- **7.2.6 — Playable instrument templates and polyphony (planned):** add reusable note-controlled organ, flute, brass, bowed-string and modal instruments with expressive rack inputs, annotated recipes, optional browser MIDI adapter and bounded precompiled voice allocation once monophonic timing is proven. Profile CPU/memory, voice stealing and independent releases before increasing voice counts.
- **7.2.7 — Further physical models (planned):** investigate string/waveguide, tube/formant and hybrid sample excitation models only after verified resonator/modal performance, with explicit stability, tuning and resource acceptance criteria.
- **7.2.8 — Keyboard performance and continuous pitch sequencing (partial):** shared laptop/on-screen input capture, live latched transposition, editable bounded note patterns, BPM/free seconds step timing including multi-bar ambient durations, sample-aligned looping, glide, quantized live recording, step recording with rests/holds/auto-advance, manual grid editing and atomic pattern history. Deliver in increments: **(a) implemented — keyboard capture and live pitch-only latch on 7.2.2**, with the shared action contract for future 7.2.3; **(b) implemented — pitch-only pattern engine, long-transport extension, playback, glide and manual/step editing**; **(c) implemented — quantized live recording, overdub/replace, atomic take history/persistence and worked drone recipe**; (d) Note-mode gate/trigger/velocity articulation using 7.1.3–7.1.4 and 7.2.3. Parts (a–c) may ship before ADSR, resonators or polyphony; keep (d) dependent on their relevant note/event prerequisites. Profile and verify long synchronized loops, tempo changes, wrap/reset, live input latency, phase/tail continuity and runtime updates without per-note recompilation. Implementation: [PR #16](https://github.com/wgabel/orbit2/pull/16), [PR #17](https://github.com/wgabel/orbit2/pull/17), [PR #19](https://github.com/wgabel/orbit2/pull/19).

Deliverable: play and edit a synthesized instrument as an ordinary Orbit2 graph; excite a handpan-like modal bank with short bursts and different pitches/velocities; use the same pitch, gate, envelope and resonator components for sustained and percussive sounds without hardcoded instrument-specific DSP; perform and loop quantized pitch progressions over rhythmic or multi-minute ambient timescales without interrupting continuous drones. Phase 8 later adds recorded assets and hybrid techniques.

#### Phase 7.3 — Performance monitoring, profiling and diagnostics (planned)

The detailed implementation and usage contract lives in [performance.md](../../performance.md), not in this roadmap. The monitor distinguishes measured/estimated/unsupported metrics, stays read-only and measures audio reliability separately from UI and signal quality.

- **7.3.1 — Passive Performance Monitor (planned):** shared compact toolbar/expanded diagnostics, sample rate and context state, browser-reported latency and supported underrun statistics, feature detection, local session baselines and bounded slow collection.
- **7.3.2 — Compilation and runtime diagnostics (planned):** C# source generation, worker round trip, worklet creation, replacement and control-only update timing; accepted/stale revisions, active/retiring voices, graph complexity and analyzer resource counts.
- **7.3.3 — Editor performance (planned):** foreground FPS/frame-gap and supported long-task metrics, independent observer lifecycle and bounded history; no changes to graph or audio.
- **7.3.4 — DSP profiling and benchmarks (planned):** deterministic representative fixtures and offline throughput, external live renderer measurements, optional validated low-overhead worklet timing if supported; relative node costs and baseline before expensive DSP/polyphony.
- **7.3.5 — Input latency and musical timing (planned):** generation-qualified action and applied-frame measurements, output-timestamp estimate, scheduled event error and long-loop drift tests once the relevant event/keyboard/sequence paths exist.
- **7.3.6 — Reports, stress and regression diagnostics (planned):** opt-in bounded local reports, history, safe reproducible stress fixtures, long-running resource cleanup and comparable CI trends; prioritize measured bottlenecks.

Deliverable: a musician can inspect audio latency and reliability while safely editing a patch, identify performance pressure and distinguish it from clipping or DSP discontinuities; developers can reproduce and compare performance regressions without changing sound. The first four increments do not depend on NoteSource, ADSR or polyphony. No ordinary browser API is assumed to expose an exact whole-renderer CPU or physical key-to-speaker latency measurement.

<a id="phase-74--reusable-composite-modules-planned"></a>

#### Phase 7.4 — Reusable composite modules (planned)

The detailed design, user workflows, graph transformation rules, storage contracts and acceptance tests live in [reusable-modules.md](../editor/reusable-modules.md). Use independent, editable composite instances first, not special drone types or live template references.

- **7.4.1 — Multi-node selection and actions (planned refinement):** reuse existing additive Shift/Ctrl/Meta selection and graph-scoped state; add marquee selection and shared accessible selection actions with eligibility feedback. Selection alone must not mutate the DSP graph.
- **7.4.2 — Group and ungroup composites (planned):** atomic selection-to-composite and single-level expansion; preserve typed boundary crossings, weights, dormant wiring, relative layout, nested ownership and undo/redo; reject non-equivalent/cyclic transformations with clear reasons; compare flattened behavior.
- **7.4.3 — Custom Module Library (planned):** save existing composites, immutable versioned data definitions, metadata and grouped searchable Add Node catalog, validated deep-cloning with remapped identities, independent persistence, built-in protections and explicit library management. Can start with existing composites independently of grouping.
- **7.4.4 — Whole-patch reuse (planned):** save a current or saved root patch as a reusable module without changing the source; convert its master-feed routing into an Output Rack and preserve configurations/layout; disclose destination transport semantics and validate the boundary.
- **7.4.5 — Portable module definitions (planned):** dedicated JSON import/export with preview, ID/name conflict resolution, bounded schema/graph validation and backward-compatible persistence; Phase 8 defines binary asset packaging.
- **7.4.6 — Composite macro controls (planned):** front-panel named controls mapped to one or more internal parameters, configurable ranges/units/curves, explicit conflict and manual-value handling, shared parameter UI, optional audio-rate modulation, validated mapping persistence/cloning and history.
- **7.4.7 — Optional linked/versioned template updates (deferred):** investigate explicit opt-in revision updates, boundary compatibility, local-edit conflicts and self-contained fallback. Never silently modify placed instances when a library record changes.

Deliverable: create a drone, instrument, effect or modulation network once; group it or save a complete patch as a module, reuse it across workspaces as independently editable composite copies, share it portably and eventually adjust internal sound parameters through front-panel macros. This track uses existing composite/port/persistence foundations and does not depend on Note-mode sequencing, resonators, polyphony or the Performance Monitor. Prioritize 7.4.2–7.4.4 for immediate practical graph reuse; 7.4.3 may ship first if saving already-authored composites is the most useful increment.

### Phase 8 — Audio assets and sample-based synthesis (planned)

- **8.1 — Audio asset library (planned):** import browser-decodable audio files, validate size/format, store original binary assets and metadata in IndexedDB, list and resolve by stable ID, and verify reload and deduplication. Prototype the FaustWasm soundfile binding and measure decoded memory requirements.
- **8.2 — Sample Player (planned):** integrate asset references into graph commands, snapshots, persistence and history; implement independent playheads, amplitude/rate modulation, playback modes, bounded loop region and click-safe looping. Validate nested graphs, live edits and resource-aware runtime replacement.
- **8.3 — Waveform editor (planned):** reusable waveform preview with region selection, playhead telemetry and relink/missing-asset feedback; reuse design tokens and shared parameter controls.
- **8.4 — Portable patch packages (planned):** versioned `.orbit` archives containing the graph document and required binary assets; safe transactional import, deduplication, missing-asset handling and backwards-compatible JSON patches.
- **8.5 — Granular Generator (planned):** overlapping grains, positional and grain-parameter modulation, bounded polyphony and CPU/memory tests.
- **8.6 — Wavetable Generator (planned):** extract or import periodic tables, prepare anti-aliased mip levels, implement morph/interpolation and pitch/phase modulation.
- **8.7 — Envelope Follower (planned):** general audio-to-modulation processor with adjustable attack/release and output scaling; test routing into existing parameters.

Deliverable: import a voice recording, persist and reuse it in multiple independent nodes or nested drones, share a self-contained patch, and transform the same source into evolving granular and wavetable textures. Existing Phase 6/7 effects work remains on the roadmap.
