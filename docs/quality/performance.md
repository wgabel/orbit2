# Orbit 2 — Performance monitoring, profiling and diagnostics

**Status:** 7.3.1 implemented ([PR #53](https://github.com/wgabel/orbit2/pull/53)); 7.3.2 implemented ([PR #56](https://github.com/wgabel/orbit2/pull/56)); 7.3.3 implemented ([PR #57](https://github.com/wgabel/orbit2/pull/57)); 7.3.4 benchmark baseline in review ([PR #58](https://github.com/wgabel/orbit2/pull/58)); 7.3.5–7.3.9 planned  
**Scope:** Browser audio playback, Faust DSP, editing/UI, compilation, memory, musical timing and repeatable performance tests.  
**Repository:** [wgabel/orbit2](https://github.com/wgabel/orbit2)  
**Parent architecture:** [ORBIT2-ARCHITECTURE.md](../architecture/overview.md)  
**Implementation baseline:** Reviewed main on 2026-11-21. Existing audio code is referenced below; later instrumentation is not implemented merely because this document exists. See the [7.3.1 implementation record](performance-implementation.md#phase-731--passive-monitor-in-review) for the passive monitor and [7.3.2](performance-implementation.md#phase-732--compilation-and-runtime-diagnostics-in-review) for compilation and resource instrumentation. [7.3.3](performance-implementation.md#phase-733--editor-responsiveness-in-review) describes foreground editor cadence and bounded history. [7.3.4](dsp-profiling.md#phase-734--benchmark-baseline-in-review) describes runnable benchmark fixtures and report artifacts; production DSP load remains unmeasured.

## 1. Purpose and goals

Orbit 2 is a browser-based modular synthesizer using C#, Blazor WebAssembly, a reusable graph compiler, Faust/WASM and Web Audio AudioWorklets. The primary performance requirement is **uninterrupted audio at the actual sample rate**, including complex drone patches, continuous LFO modulation, live edits, scope/spectrum display, and eventually keyboard performance, sequencing and multiple instrument voices.

Performance has several independent dimensions:

- **Real-time audio reliability:** can the rendering thread produce the next block of samples in time, every time? Are there underruns, interruptions or errors?
- **Audio latency:** how long between an input action and hearing its result? This matters for keyboard playing, although a sustained drone may tolerate more delay.
- **DSP processing demand:** how close is the audio engine to exhausting its rendering budget? Are we approaching a limit before an audible problem occurs?
- **Editor responsiveness:** is dragging nodes, changing sliders, interacting with wires or opening nested canvases smooth?
- **Compilation/replacement latency:** how quickly can Orbit generate Faust source, compile it, initialize a new worklet and accept a new graph without interrupting existing audio?
- **Resources:** how many reachable DSP nodes, parameter controls, observation taps, capture leases, FFTs and runtime voices are active? Do memory or resources grow over time?
- **Musical timing:** do multiple sequencers and event sources share exact boundaries, do long loops drift, and how late do manual performance events reach the engine?
- **Signal quality:** is the generated audio clipping or discontinuous? Signal clipping is not CPU overload.

**Desired performance and planned UX:** the [health target contract](performance-metrics.md#31-suggested-health-thresholds) defines proposed audio, editor and compile thresholds; the [explainable badge design](performance-metrics.md#421-explainable-health-indicators-planned-737) requires a short visible “why” next to every assessed metric. Phase 7.3.7 adds this interpretation to existing valid measurements without waiting for validated in-app DSP timing. Phase 7.3.8 [device capacity benchmarks](dsp-profiling.md#94-device-specific-capacity-and-reference-compositions-planned) establish representative mobile and desktop workloads and calibrate targets. These are planned goals, not verified current capacities.

A single number called "performance" cannot answer these questions. The monitor should expose a small number of useful, honest metrics and allow an expanded diagnostic view when something goes wrong. It should let a musician build richer patches with confidence and let a developer or AI coding agent identify the relevant subsystem before changing code.

**Delivery priority:** Phase 7.5 ([stereo audio, panning and oscillator unison](../audio/stereo-panning-and-unison.md)) should follow the 7.3.1 passive monitor, 7.3.2 compilation/runtime/resource diagnostics and 7.3.4 reproducible mono benchmark baseline. Unison multiplies waveform instances; stereo expands channel processing; graph crossfades temporarily run both old/new DSP. Establish measurements before introducing these costs and repeat the benchmark after each Phase 7.5 increment. Use the documented external/offline workflow when in-app load is unsupported. Editor monitoring (7.3.3) is recommended; the timing and advanced reports milestones need not delay Phase 7.5.

### Non-goals and invariants

- Do not redesign the graph model, audio engine or Faust execution solely to display statistics.
- Do not create a second audio context or route new channels into the master merely for monitoring.
- Do not introduce server telemetry or require a network service. Statistics are local and reports are explicitly exported.
- Do not promise to measure total computer CPU, GPU, battery use or actual speaker latency from ordinary browser APIs.
- Do not silently change output latency, sample rate, patch settings, graph content, analyzer quality or sound to improve the meter. Suggest actions and let the user choose.
- A monitor read, toggle, reset or report export must not change command history, committed graph revision, persisted patch content, musical transport, DSP source, compiled identity or playback generation.
- Do not use a displayed peak, a worklet error count, a JavaScript timer delay or a made-up CPU estimate as a substitute for actual rendering capacity or underrun statistics.
- Separate **ordinary passive monitoring** (low overhead; opt-in by opening the Performance panel) from **explicit diagnostic profiling and stress testing** (potentially expensive; separately opt-in). While closed, no recurring monitor sampling or operation timing runs. See the [on-demand collection contract](performance-implementation.md#on-demand-collection).

## Contents

1. [Purpose and goals](#1-purpose-and-goals)
2. <a id="2-understand-audio-performance"></a>[Understand audio performance](performance-metrics.md#2-understand-audio-performance)
3. <a id="3-metric-catalog-and-interpretation"></a>[Metric catalog and interpretation](performance-metrics.md#3-metric-catalog-and-interpretation)
4. <a id="4-performance-monitor-ux-and-usage"></a>[Performance Monitor UX and usage](performance-metrics.md#4-performance-monitor-ux-and-usage)
5. <a id="5-browser-capabilities-and-measurement-limitations"></a>[Browser capabilities and measurement limitations](performance-metrics.md#5-browser-capabilities-and-measurement-limitations)
6. <a id="6-implementation-architecture"></a>[Implementation architecture](performance-implementation.md#6-implementation-architecture)
7. <a id="7-metric-collection-and-measurement-contracts"></a>[Metric collection and measurement contracts](performance-implementation.md#7-metric-collection-and-measurement-contracts)
8. <a id="8-dsp-profiling-and-headroom"></a>[DSP profiling and headroom](dsp-profiling.md#8-dsp-profiling-and-headroom)
9. <a id="9-benchmark-patches-and-regression-workflow"></a>[Benchmark patches and regression workflow](dsp-profiling.md#9-benchmark-patches-and-regression-workflow)
10. <a id="10-diagnosing-real-problems"></a>[Diagnosing real problems](performance-troubleshooting.md#10-diagnosing-real-problems)
11. <a id="11-performance-improvement-strategy"></a>[Performance improvement strategy](performance-troubleshooting.md#11-performance-improvement-strategy)
12. <a id="12-testing-and-acceptance-criteria"></a>[Testing and acceptance criteria](performance-verification.md#12-testing-and-acceptance-criteria)
13. <a id="13-implementation-roadmap"></a>[Implementation roadmap](performance-verification.md#13-implementation-roadmap)
14. <a id="14-glossary"></a>[Glossary](performance-verification.md#14-glossary)
15. <a id="15-technical-references"></a>[Technical references](performance-verification.md#15-technical-references)


## Large-patch editor responsiveness (7.3.9)

**Problem:** a 191-node musical patch was reported too laggy to run. Treat editor responsiveness as an independent workload, not proof of high DSP cost. The initial code-inspection suspects include full canvas node iteration, shared effective-value dictionaries passed to every NodeFrame, whole-graph port anchor discovery, and per-wire rendering. These are hypotheses, not measured findings. The owning [roadmap slices](../roadmap/advanced.md#739-delivery-slices) require profiling before selecting optimizations.

**Benchmark design:** deterministic importable 50, 200 and 500 node fixtures, including connected oscillators, effects, modulation, sequencers, compact/expanded controls and realistic cable density. Record active canvas count, viewport-intersecting count, total recursive patch count, reachable DSP count, connection count, viewport size and zoom separately. Include dense layouts with approximately 200 nodes intersecting the viewport (respect 25–400% zoom; do not shrink UI below its allowed limits) and a larger graph with only a subset in view. Test the same fixtures with transport stopped and playing, monitor closed by default and plots closed/open in separate runs; use opt-in benchmark instrumentation or external browser profiling to measure overhead separately. Include import/time-to-interactive, idle, scripted drag, pan, pinch/wheel zoom, Fit, cable drag/selection, parameter edits, telemetry and nested navigation. Reproduce on a reference desktop and Android device; record exact revision, hardware, browser, refresh cadence, fixture fingerprint, warmup, sample count and configuration. Report repeated median/p95 frame gaps, observed FPS, long tasks, interaction-to-paint, component render counts, DOM/layout/paint cost, interop calls, allocation/GC and memory trend where available. Separate actual measurements from estimates and unavailable fields.

**Targets:** use the existing [cadence-relative health targets](performance-metrics.md#31-suggested-health-thresholds). On a 60 Hz reference, aim for 55–60 observed FPS and p95 frame gap below 20 ms during representative 200-node interaction, no repeated long tasks, no UI-induced audio interruptions and prompt input feedback; aim for usable 500-node interaction (at least 40 FPS and p95 at most 33 ms) and publish measured capacity rather than promising hardware-independent guarantees. Compare identical workload before/after; shared CI runner timings are diagnostic, not hard device-performance gates. Add deterministic regression assertions for render/invalidation scope, event coalescing, resource lifecycle, and graph/interaction correctness. Measure import/first paint separately from steady state.

**Preservation contract:** retain the shared root/nested canvas, original editable compact/expanded controls, all in-view nodes and individual input/output ports, selection, focus, drag, gestures, cables, anchor geometry, accessibility and state persistence. No automatic hiding of inactive nodes or changes to DSP/audio quality. Offscreen-only presentation culling may be evaluated with stable world geometry and overscan; retain logical visibility, graph membership, connection paths crossing the viewport, Fit and selection semantics. Never treat culling as a graph edit or interrupt audio. Keep telemetry throttling presentation-only, preserve live effective values, and maintain the existing on-demand performance-monitor lifecycle. Prefer measured incremental improvements over a rewrite; evaluate alternative rendering technology only if traces justify it.

### Initial external editor benchmark tooling (7.3.9-S1, in review)

[PR #125](https://github.com/wgabel/orbit2/pull/125) adds a distinct 50/200/500-node fixture recipe to the existing probe and reuses the published-app Chromium benchmark host. From the repository root after publishing the app and installing the browser dependencies described in [the DSP benchmark workflow](dsp-profiling.md#phase-734--benchmark-baseline-implemented):

```sh
dotnet run --project tests/Orbit.Audio.Probe/Orbit.Audio.Probe.csproj --configuration Release -- --editor-benchmark editor-benchmark-output
node scripts/benchmark-live.mjs --editor editor-benchmark-output editor-benchmark-output/editor-report.json editor-50 editor-200 editor-500
```

Set `ORBIT_PLAYWRIGHT_DIR`, `ORBIT_CHROME`, and optionally `ORBIT_BENCH_PUBLISH_DIR`, `ORBIT_REVISION` and `ORBIT_BENCH_HEADFUL=1` as for the existing live benchmark. Use `ORBIT_EDITOR_VIEWPORT=mobile` for a narrow emulated viewport (still mouse-driven, not physical Android or pinch), `ORBIT_EDITOR_PLAYBACK=0` for stopped-only profiling, `ORBIT_EDITOR_REPEATS=1..5` and `ORBIT_EDITOR_IDLE_MS=500..10000`. Default runs exercise stopped and playing idle, pan, wheel zoom and node drag three times. Preserve the generated patch files and their reported SHA-256 fingerprints; recipes have stable layout and parameters but fresh generated graph identities. CI generates and validates all three sizes via the real serializer/compiler, imports 50 nodes and measures both states; runner speed is diagnostic, not a timing gate. Compare identical saved fixtures, browser/viewport and settings before and after each optimization.

The report includes rendered/intersecting counts, wire count, zoom, pan, DOM elements, rAF frame gaps, long tasks where available, renderer CDP layout/style/script durations and heap delta. Import timing includes the import dialog lifecycle. Remaining S1 evidence includes physical desktop/Android measurements, true touch/pinch, explicit live control edits, Fit/cable/nested scenarios, component render counts, detailed traces and audio reliability measurements. Do not mark S1 complete or select a bottleneck from fixture counts alone. No runtime production instrumentation or audio changes.
