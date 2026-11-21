# Testing and acceptance criteria

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [graph model](../core/graph-model.md) · [dsp engine](../audio/dsp-engine.md) · [commands and events](../core/commands-and-events.md) · [performance](../../performance.md).

## 14. Testing and acceptance criteria

The core model should be independently unit testable without a browser or Faust. Offline DSP tests and browser integration tests cover the actual audio path.

| Area | Essential acceptance test |
|---|---|
| Node model | Create sine/processor nodes with stable port and parameter identities. |
| Connection validation | Reject invalid endpoints, incompatible ports, duplicate edges and forbidden cycles. |
| Modulation | Base 440 Hz with +/-100 Hz source yields 340–540 Hz without changing BaseValue. |
| Multiple sources | One LFO routes to several parameters; a parameter combines multiple valid sources. |
| Phase | Frequency changes advance existing phase instead of accidentally resetting it. |
| Sample rate | Oscillator frequency and tempo sync correct at both 44.1 and 48 kHz. |
| Transport | 120 BPM quarter note = 0.5 seconds; synchronized LFOs share expected phase/reset. |
| Envelope and rhythmic events (partial: AD and typed adapters implemented) | Oscillator edges trigger one AD each, exact duration/endpoints at 44.1/48 kHz, retrigger from current value, gate-off release, shared trigger fan-out, typed routing and nested persistence, no phantom pulses across transport wraps/replacements and no graph revisions on runtime actions. |
| Pitch and instruments (partial: shared pitch tracking implemented) | Reference-note conversion and exact octave/harmonic ratios, compatible additive Hz and pitch modes, sample-aligned pitch/gate/trigger/velocity, no graph commits on note actions, independent voices, valid editable templates and bounded CPU/headroom. |
| Keyboard performance / continuous sequencing (7.2.8 — partial) | Keyboard isolation, latched pitch, generation-safe live actions and no per-note compilation in 7.2.8a; multi-bar/free loop boundaries, Holds, glide and atomic pattern edits in 7.2.8b; quantized live recording, atomic commit/undo/persistence and future-boundary updates in 7.2.8c ([PR #19](https://github.com/wgabel/orbit2/pull/19)); Note-mode articulation remains planned. |
| Audio pitch shifting (Phase 6.20, planned) | Single-tone and mixed-chord spectral peaks follow the same transposition ratio at 44.1/48 kHz, sustained duration/tempo preserved, latency and alias/sideband quality measured, silent and bounded output under extreme modulation, transparent dry/zero shift, click-safe audition, independent instances and bounded CPU/memory. |
| Resonators and modal synthesis (planned) | Compiled impulse responses at 44.1/48 kHz match tuned center and amplitude T60, silence does not self-oscillate, repeated excitation retains ringing state, rapid modulation stays finite/stable, modal frequency and independent decays match configuration, and template/bank state round-trips with undo and persistence. |
| Fan-out | One output can feed several processors without duplicating generator runtime state. |
| Composite | Flattening a subgraph produces equivalent routing to a flat graph. |
| Rack ports | Add/remove/rename/reorder updates both views; rename/reorder retains connections. |
| Rack deletion | Parent and child connections removed atomically; no dangling references. |
| Events | Successful command publishes committed change set; rejected command publishes no change event. |
| Subscribers | Disposing subscription stops delivery; independent listeners can coexist. |
| UI separation | Node movement/opening subcanvas does not recompile or restart audio. |
| Compile revisions | Stale results cannot replace a newer successful runtime. |
| DSP failure | Last valid runtime remains active; error is displayed to the user. |
| Presets | Two instances have independent parameters and DSP state. |
| Node audition (implemented) | Mute last node in one mixer lane while preserving other lanes; mute fan-out including composite and modulation outputs; bypass eligible effects with unchanged dry signal; retain DSP state, parameters and wiring, verify smooth transitions without recompilation, mute precedence, undo/redo and compatible persistence. |
| Individual node presets (implemented) | Validate every shipped configuration; reject invalid batch without mutation; application preserves identities and wiring, emits one revision and undo entry; base-only updates avoid recompilation; custom groups survive reload and library edits do not alter existing nodes. |
| Signal analysis (implemented core; advanced analysis planned) | Compare master samples with taps on/off; no observation-channel leakage, phase reset or graph compile on view changes; verify root/nested port mapping, weighted input and post-mute/bypass output, known waveform/FFT/peak/RMS, slow LFO history, stale-frame rejection, hidden-view cleanup and bounded resources. |
| Dynamic signal taps (6.18.8 — in review) | [PR #20](https://github.com/wgabel/orbit2/pull/20) adds compiler, runtime and offline WASM regressions for more than eight reachable outputs, eight concurrent auxiliary slots, ninth-request capacity feedback, reuse, alias sharing, distinct unrouted status, nested/stale-generation cleanup and sample-identical master audio without graph recompilation; browser/device profiling remains follow-up. |
| Processor response (Phase 6.18.5 — implemented) | EQ center gain, Q bandwidth, filter modes and Nyquist limits match offline Faust measurements at 44.1/48 kHz; distinguish base/effective estimates, live spectra and stopped previews. |
| Analyzer sink (planned) | Analyzer-only branches are observable but silent at the master; explicit graph edits round-trip with undo and persistence; visualization preferences restore without changing graph topology or audio. |
| Patch documentation (planned) | Notes, aliases, labels and groups round-trip with root/nested layouts and named snapshots; old documents restore empty annotations, invalid data is rejected atomically, annotation history and text editing remain independent of graph history, and graph revision, compilation and playback generation stay unchanged. |
| Connection visualization (Phase 6.19.5 — implemented core) | Badge units and signs match committed receiving-port semantics; zero/dormant states remain distinct; labels follow cable midpoints behind nodes and obey visibility preferences; the editor clamps to usable canvas geometry; display changes leave graph and playback unchanged, while explicit Apply commits one amount change and undo/redo restores its badge. |
| Output safety | Controlled master gain, no unexpected discontinuity when changing base values. |
| Performance and diagnostics (7.3 — planned) | Feature-detected metrics with explicit scope/availability, no monitoring-induced graph/audio changes, session/generation correctness, bounded resources and reproducible throughput/real-time comparisons; see [performance.md](../../performance.md). |
| Persistence | Save/load nested graph and layout with stable IDs and equivalent connectivity. |
| Audio assets (planned) | Import and reload binary recordings, share immutable data across nodes, preserve IDs through undo/redo, handle quota and missing assets without corrupting patches. |
| Sample playback (planned) | Verify pitch/rate at 44.1/48 kHz, independent playheads, interpolation, loop bounds/crossfades, reverse and one-shot termination; reject stale asset loads during live replacement. |
| Portable packages (planned) | Round-trip a patch and its assets in a fresh browser; reject malformed, oversized or conflicting archives without partial workspace replacement. |

Use OfflineAudioContext or an appropriate offline DSP renderer for deterministic audio tests where possible. Measure spectral/frequency behavior, sample continuity and expected modulation envelopes. Browser playback and workload tests are needed to catch real-time underruns and device-specific behavior.
