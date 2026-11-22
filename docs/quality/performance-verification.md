# Performance verification, roadmap and glossary

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [performance](../../performance.md) · [dsp engine](../audio/dsp-engine.md) · [testing](testing.md).

## 12. Testing and acceptance criteria

### 12.1 Unit and contract tests (C# and JavaScript)

- Quantum budget conversion: 128/48 kHz, 128/44.1 kHz, other actual sizes; reject zero/negative/nonfinite sample rates or frame counts. Preserve load above 100%.
- Latency: seconds→ms once; zero vs null; partial browser support; no double-counting; output timestamp timebase mapping and stale/invalid values.
- Session counters: nonzero initial baselines, positive deltas, reset, context change, stop/restart, cumulative counter decreases, empty/zero total duration, unsupported fields. Report 3 rather than 15 events in the example above.
- Aggregation: bounded history, correct median/p95 for deterministic samples, samples/window labels, no false zeros or fake load, correct state and stale behavior.
- Timing stages: fake clock, worker request and reply, cold/warm cache classification without guessing, requested and accepted operation IDs, failed/cancelled/superseded events; distinguish source-reused operation.
- Runtime: read-only metrics never create/resume context, reconnect master, alter active voice, increment graph revision, change undo/persistence, or touch transport. Diagnostics exceptions and missing APIs do not stop audio.
- Lifecycle: no overlapping interop reads, late results cannot overwrite a new session, disposal cancels timers/observers, visibility transitions don't generate gigantic frame gaps, repeated panel opens don't leak listeners.
- UI: accessible compact control/expanded panel, separate badges, missing state and tooltip, stable sizing, readable units and responsive/nested canvas behavior, no graph manipulation on panel interaction, local report export.
- Resource counts: distinction between reachable and editable, current and retiring voices, unique capture channel vs lease; verify counts match ownership and cleanup after stop/replace/dispose.
- DSP profiler if supported: synthetic deterministic clock examples, actual block shape, scope labels, overhead and identical sample output; unsupported path must remain functional.
- Report schema: finite numeric fields, provenance/capabilities, bounded sizes, no audio buffers or personal/patch content by default.

### 12.2 Integration/browser tests

Run against the published app, not only unit mocks. Test both browser-supported and deliberately missing optional APIs. Add deterministic fakes for playback statistics as needed to avoid requiring a real physical underrun in CI.

- Startup: monitor can be opened in a stopped workspace; context remains uncreated until Play.
- Playback: displayed sample rate and context state; counters update at display cadence; missing feature states correct.
- Live parameter edit: no transition/compilation count change except the control update; monitor visibility/toggle does not change source or generation.
- Structural edit: valid source generation and worker/factory timing, accepted generation and replacement stage separation, retiring count resolves; stale/failed replacements obey last-good policy.
- Expand/collapse visualization, nested navigation, patch save/load, repeated play/stop: bounded observers/leasing and independent diagnostics.
- Background/visibility changes: paused or stale UI sample, resumed fresh window, no fabricated performance crisis from inactivity.
- Basic benchmark protocol with deterministic fixtures at 44.1 and 48 kHz, and optional real hardware smoke check. Document actual hardware/browser and real-vs-simulated statistics.

### 12.3 Numerical DSP and long-runtime acceptance

Use existing offline/probe tests to check that enabling diagnostics leaves signal output unchanged (identical deterministic samples or documented numeric tolerance), maintains phase continuity, preserves master channel isolation and does not add latency/samples to the DSP signal path. Future sequencer tests prove intended versus actual event frames across 12-beat wrap extensions, tempo changes, long loops and free/synchronized timing.

During repeated runtime replacement and prolonged playback, collect resource-count trends, sample continuity checks, latency/underrun deltas, and external renderer measurements where available. Assess profiler overhead separately from the base app. Do not claim "zero clicks" based on a single test or a browser's unsupported stats. Use benchmark logs to document observed constraints and revise the thresholds.

## 13. Implementation roadmap

Each phase should be a separate reviewable increment. Before implementation read the parent architecture, README, project layout and current test infrastructure; inspect merged repo history to reconcile exact dependencies and naming. Implement production code only with tests, use the existing architecture boundaries and design tokens, run available local/CI checks, and update this document with measured results, browser support and actual behavior. Retain milestone IDs when independent work ships out of sequence.

| Milestone | Status | Deliverable |
|---|---|---|
| 7.3.1 | Implemented · [PR #53](https://github.com/wgabel/orbit2/pull/53) | Passive context statistics + shared Performance Monitor |
| 7.3.2 | Implemented · [PR #56](https://github.com/wgabel/orbit2/pull/56) | Compilation, update/replacement, runtime and analysis resource diagnostics |
| 7.3.3 | In review · [PR #57](https://github.com/wgabel/orbit2/pull/57) | Foreground editor responsiveness and bounded diagnostics history |
| 7.3.4 | Planned | Validated DSP profiling and reproducible benchmark fixtures |
| 7.3.5 | Planned | Input-to-engine and musical timing diagnostics, when note/sequencer events exist |
| 7.3.6 | Planned | Advanced local reports, stress/regression automation and measured improvement cycles |

### 7.3.1 — Passive monitor

**Implemented:** [PR #53](https://github.com/wgabel/orbit2/pull/53). Node tests cover idle/unsupported/invalid fields, context changes, local baseline and reset, decrease/recovery, same-interval fraction, overlapping lifetimes, stop/restart, and graph replacement preserving epoch. The published editor smoke test opens diagnostics before Play, verifies no context exists, opens during accepted playback, checks sample rate and resets without changing the accepted revision. Local JavaScript suites passed (23 tests); the .NET/published-browser checks run in CI.

Implement framework-neutral snapshot and optional metrics port, browser context reads, capability/availability states, session baseline/reset and slow polling, compact toolbar and expanded details. Include sample rate, context state, estimated latency and supported playback statistics; unavailable metrics must stay unavailable. Test no graph/audio mutations and lifecycle/disposal. Ship even when render load is unavailable. Do not block on profiling or sequencer work.

### 7.3.2 — Compilation and runtime resources

**Implemented:** [PR #56](https://github.com/wgabel/orbit2/pull/56). 32 local Node tests pass across passive monitoring, runtime recorder, worker/factory and AudioEngine; verify acceptance vs completion, stale/failure protection, current/retiring counts, existing capture resource aggregation and live control updates not increasing preparation attempts. .NET regression covers initial build, live control reuse, disconnected source-identical edit, real replacement and stopped snapshot. Published browser test checks actual monitor worklet and source counts; full [CI run](https://github.com/wgabel/orbit2/actions/runs/45035672813) passed after correcting the reachable-leaf fixture expectation (two oscillators + gain). These tests validate host-stage instrumentation, not audio-render headroom.

Instrument existing compiler and playback stages with attempt/accepted IDs, known cache information, compile/replacement timings and counters. Expose editable/reachable counts, current/retiring voices, auxiliary channels, analyzer/capture counts, and runtime errors. Add bounded operation events. Verify base-only and source-identical edits do not add unnecessary compilation. This can ship independently of worklet instrumentation.

### 7.3.3 — UI performance

**In review:** [PR #57](https://github.com/wgabel/orbit2/pull/57). Eight local deterministic Node tests cover observed cadence, percentile and window calculation, stalls exceeding the window, independently supported/throwing observers, null vs zero, capped history, visibility and stale callbacks, and idempotent disposal. Published-editor smoke coverage waits for FPS and history before Play and retains the existing audio/session/revision checks. Full .NET and published browser verification runs in CI. No physical-device performance benchmark was performed.

Add feature-detected foreground frame/long-task monitoring, p95 frame gaps, slow publication and bounded history. Implement independent observer lifecycle and test visibility handling; measure normal editor interactions, node plots and nested views. Include no automatic quality changes.

### 7.3.4 — DSP profiling and benchmark baseline

First build the deterministic fixture harness (baseline, increasing oscillator counts, layered/analysis-heavy graphs, edit/lifecycle suites) and record offline throughput plus external DevTools renderer measurements. Evaluate in-app worklet timing feasibility on the pinned library/platform; ship a live DSP-only load/headroom metric only if the timing source and measured overhead meet acceptance. Otherwise preserve "unavailable" and document how to use the external/offline approach. Measure node categories only as relative profile-specific costs. Establish baseline before introducing costly resonator/modal, sample or polyphonic DSP. Phase 7.5 stereo/unison specifically prioritizes 7.3.1, 7.3.2 and this benchmark baseline before implementation; compare mono/stereo and voice-count profiles, including graph-replacement peak load (see [7.5 performance contract](../audio/stereo-panning-and-unison.md#1-performance-first)).

### 7.3.5 — Musical timing diagnostics

After Phase 7.1 runtime event delivery and 7.2.3/7.2.8 performance actions exist, add generation-qualified action IDs and measured application dispatch/ack, actual frame, expected frame error and output timestamp estimate. Validate the shared clock with long drone loops and tempo changes. Optional external physical latency measurement documented as a diagnostic exercise. Do not introduce keyboard actions or sequencer functionality merely to implement metrics.

### 7.3.6 — Reports and regression improvements

Add opt-in history charts, local report/copy/export, stress testing with safe fixture workload controls, optional machine-comparable CI benchmark job, long-running leak tests, analysis comparisons and regression summaries. Record actual current-vs-baseline results and prioritize measured bottlenecks; review resource and quality changes separately. No unbounded histories, automatic uploads, hidden audio paths or speculative optimizations.

Dependencies: 7.3.1–7.3.4 can start from the current architecture without waiting for musical instrument milestones; 7.3.5 depends on the relevant runtime event/keyboard work; 7.3.6 may progressively extend earlier reporting/benchmark facilities. Phase 6.18 signal-analysis observations can contribute resource metrics but must not duplicate the diagnostics system. Maintain performance acceptance as a cross-cutting requirement for all subsequent DSP nodes.

## 14. Glossary

| Term | Meaning in Orbit 2 |
|---|---|
| AudioContext | Shared browser object owning audio time, device output and audio graph |
| AudioWorklet | Browser mechanism for custom real-time audio calculations off the UI thread |
| DSP | Digital signal processing: synthesis, modulation, filters and effects that calculate audio/control samples |
| Faust | Functional audio programming language compiled to WebAssembly for Orbit DSP |
| WASM / WebAssembly | Portable execution format for compiled Faust and the .NET browser runtime |
| Sample / frame | One channel value / simultaneous values across all channels at a time point |
| Sample rate (Fs) | Frames processed per second, measured in Hz |
| Render quantum | Audio block handled by one rendering operation (often 128 frames) |
| Callback buffer | Device/platform callback buffer; may include more than one quantum |
| Render budget | Audio duration represented by the quantum; processing must keep up over time |
| Render capacity | Renderer-specific fraction/percentage of its time budget consumed; Chrome DevTools metric |
| DSP-only load | Measured kernel compute time divided by budget; excludes other renderer overhead |
| Headroom (performance) | Unused fraction of processing budget; different from signal amplitude headroom |
| Headroom (signal) | Remaining amplitude margin before the relevant clipping limit |
| Latency | Elapsed time between identified events; specify endpoints and measurement method |
| Underrun / dropout | Missing frames at the output because audio was not supplied in time |
| Glitch | A perceptible unwanted artifact; can have processing, signal or device causes |
| Clipping | Signal amplitude exceeds an output range, causing distortion/saturation where clipped |
| dBFS | Decibels relative to digital full scale; 0 dBFS corresponds to normalized peak magnitude 1 |
| RMS | Root mean square, an amplitude/energy-related level over a window, not CPU |
| FFT | Fast Fourier transform, computing spectrum from a time-domain sample window |
| LFO | Low-frequency oscillator used to modulate another parameter |
| BPM / transport | Beats per minute and shared musical-time reference |
| Quantization | Mapping musical event onset to a defined rhythmic grid; different from audio render quantum |
| Glide | Intentional gradual transition between pitch targets |
| Phase continuity | Keeping oscillator cycle state as frequency changes, avoiding unintended discontinuity |
| Crossfade | Temporarily overlap old/new audio graphs while fading gains in complementary directions |
| Revision / generation | Committed graph change identity / accepted runtime voice identity used to reject stale observations |
| Cache hit | Reuse of previously compiled artifact if known; not determined by elapsed time alone |
| p50, p95 | Median and approximately 95th-percentile values within an observed sample set |
| FPS | Foreground observed frames per second; influenced by display cadence |
| Long task | Browser-reported main-thread work with a duration typically exceeding 50 ms |
| Offline throughput | Amount of generated audio per wall-clock second in a benchmark; not guaranteed live capacity |
| Monotonic clock | Increasing elapsed-time reference, independent of calendar/system-time changes |
| Jitter | Variation in timing around a target or between events |
| Drift | Accumulating difference over repeated timing intervals |
| Telemetry | Read-only observed runtime information; here local, not remote analytics |
| Interop | Boundary communication between Blazor/C# and JavaScript modules |
| Provenance | Where a metric came from, its scope and measurement method |
| Feature detection | Checking actual browser support at runtime, rather than assuming from version or device |

## 15. Technical references

Prefer primary specifications and official browser documentation. Recheck browser compatibility and pinned Faust library behavior when implementing.

- [Web Audio API specification (AudioContext, output latency, timestamps, audio rendering and AudioPlaybackStats)](https://webaudio.github.io/web-audio-api/)
- [Chrome DevTools WebAudio panel](https://developer.chrome.com/docs/devtools/webaudio)
- [Chrome DevTools Protocol WebAudio domain](https://chromedevtools.github.io/devtools-protocol/tot/WebAudio/) — experimental developer-tool renderer availability, not a normal page API.
- [Chrome 146 release notes](https://developer.chrome.com/release-notes/146) — WebAudio Playback Statistics API.
- [High Resolution Time specification](https://www.w3.org/TR/hr-time/) — monotonic timing.
- [Performance Timeline specification](https://www.w3.org/TR/performance-timeline/) — observation and performance entries.
- [Long Tasks API specification](https://w3c.github.io/longtasks/) — optional main-thread measurements.
- [HTML animation frame processing](https://html.spec.whatwg.org/multipage/imagebitmap-and-animations.html#animation-frames) — rendering callbacks.
- [Faust documentation](https://faustdoc.grame.fr/) and pinned [faustwasm project](https://github.com/grame-cncm/faustwasm) — inspect the actual supported worklet integration before profiling.
- [Orbit 2 architecture](../../ORBIT2-ARCHITECTURE.md) — layers, real-time requirements, signal graph and transport contracts.
- [Orbit 2 README](../../README.md) — build, existing features, local execution and test workflows.
