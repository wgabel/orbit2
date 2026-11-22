# Orbit 2 — Performance monitoring, profiling and diagnostics

**Status:** Planned specification · Phase 7.3  
**Scope:** Browser audio playback, Faust DSP, editing/UI, compilation, memory, musical timing and repeatable performance tests.  
**Repository:** [wgabel/orbit2](https://github.com/wgabel/orbit2)  
**Parent architecture:** [ORBIT2-ARCHITECTURE.md](ORBIT2-ARCHITECTURE.md)  
**Implementation baseline:** Reviewed main on 2026-11-21. Existing audio code is referenced below; the proposed monitor, instrumentation and benchmark harness are not implemented merely because this document exists.

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

A single number called "performance" cannot answer these questions. The monitor should expose a small number of useful, honest metrics and allow an expanded diagnostic view when something goes wrong. It should let a musician build richer patches with confidence and let a developer or AI coding agent identify the relevant subsystem before changing code.

**Delivery priority:** Phase 7.5 ([stereo audio, panning and oscillator unison](docs/audio/stereo-panning-and-unison.md)) should follow the 7.3.1 passive monitor, 7.3.2 compilation/runtime/resource diagnostics and 7.3.4 reproducible mono benchmark baseline. Unison multiplies waveform instances; stereo expands channel processing; graph crossfades temporarily run both old/new DSP. Establish measurements before introducing these costs and repeat the benchmark after each Phase 7.5 increment. Use the documented external/offline workflow when in-app load is unsupported. Editor monitoring (7.3.3) is recommended; the timing and advanced reports milestones need not delay Phase 7.5.

### Non-goals and invariants

- Do not redesign the graph model, audio engine or Faust execution solely to display statistics.
- Do not create a second audio context or route new channels into the master merely for monitoring.
- Do not introduce server telemetry or require a network service. Statistics are local and reports are explicitly exported.
- Do not promise to measure total computer CPU, GPU, battery use or actual speaker latency from ordinary browser APIs.
- Do not silently change output latency, sample rate, patch settings, graph content, analyzer quality or sound to improve the meter. Suggest actions and let the user choose.
- A monitor read, toggle, reset or report export must not change command history, committed graph revision, persisted patch content, musical transport, DSP source, compiled identity or playback generation.
- Do not use a displayed peak, a worklet error count, a JavaScript timer delay or a made-up CPU estimate as a substitute for actual rendering capacity or underrun statistics.
- Separate **ordinary passive monitoring** (low overhead; enabled by default when supported) from **explicit diagnostic profiling and stress testing** (potentially expensive; opt-in).

## Contents

1. [Purpose and goals](#1-purpose-and-goals)
2. <a id="2-understand-audio-performance"></a>[Understand audio performance](docs/quality/performance-metrics.md#2-understand-audio-performance)
3. <a id="3-metric-catalog-and-interpretation"></a>[Metric catalog and interpretation](docs/quality/performance-metrics.md#3-metric-catalog-and-interpretation)
4. <a id="4-performance-monitor-ux-and-usage"></a>[Performance Monitor UX and usage](docs/quality/performance-metrics.md#4-performance-monitor-ux-and-usage)
5. <a id="5-browser-capabilities-and-measurement-limitations"></a>[Browser capabilities and measurement limitations](docs/quality/performance-metrics.md#5-browser-capabilities-and-measurement-limitations)
6. <a id="6-implementation-architecture"></a>[Implementation architecture](docs/quality/performance-implementation.md#6-implementation-architecture)
7. <a id="7-metric-collection-and-measurement-contracts"></a>[Metric collection and measurement contracts](docs/quality/performance-implementation.md#7-metric-collection-and-measurement-contracts)
8. <a id="8-dsp-profiling-and-headroom"></a>[DSP profiling and headroom](docs/quality/dsp-profiling.md#8-dsp-profiling-and-headroom)
9. <a id="9-benchmark-patches-and-regression-workflow"></a>[Benchmark patches and regression workflow](docs/quality/dsp-profiling.md#9-benchmark-patches-and-regression-workflow)
10. <a id="10-diagnosing-real-problems"></a>[Diagnosing real problems](docs/quality/performance-troubleshooting.md#10-diagnosing-real-problems)
11. <a id="11-performance-improvement-strategy"></a>[Performance improvement strategy](docs/quality/performance-troubleshooting.md#11-performance-improvement-strategy)
12. <a id="12-testing-and-acceptance-criteria"></a>[Testing and acceptance criteria](docs/quality/performance-verification.md#12-testing-and-acceptance-criteria)
13. <a id="13-implementation-roadmap"></a>[Implementation roadmap](docs/quality/performance-verification.md#13-implementation-roadmap)
14. <a id="14-glossary"></a>[Glossary](docs/quality/performance-verification.md#14-glossary)
15. <a id="15-technical-references"></a>[Technical references](docs/quality/performance-verification.md#15-technical-references)
