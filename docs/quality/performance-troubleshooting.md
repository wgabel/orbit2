# Performance troubleshooting and improvement strategy

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [performance](performance.md) · [dsp engine](../audio/dsp-engine.md) · [testing](testing.md).

## 10. Diagnosing real problems

### 10.1 Problem → measurement → likely subsystem

| Observed issue | Useful first measurements | Investigation |
|---|---|---|
| Sound occasionally cracks, even when not editing | Underrun delta, duration, renderer capacity, real-time workload, device changes | If counters grow, investigate headroom and competing system load; if clean, examine oscillator/signal discontinuities |
| Crack immediately on patch routing change | Replacement stages/events, active voices, underruns, last-good status, waveform around transition | Crossfade/replacement scheduling, tail or phase discontinuity, peak safety; compare old/new DSP |
| Master or mixer displays +3.4 dBFS | Actual signal peak at affected source and final master, node gains | Signal amplitude/gain staging, not DSP timing; reduce contributions |
| Mouse interaction is laggy, but sound stays smooth | FPS, p95 gaps, long tasks, graph command and rerender timing | UI layout/measurement, canvas labels, high-frequency telemetry, plot drawing |
| Audio gets worse as scopes are expanded | Unique analysis channels/leasing, visible FFTs, renderer load, editor frame metrics | Compare same graph with plots hidden; avoid duplicate FFT/capture, limit refresh |
| Sound stops while many nodes are added | Runtime errors, compiler failure, requested vs accepted revision, underruns | Last-good graph policy, memory/CPU pressure, stale updates; don't replace audible graph on failure |
| Keyboard response feels late | Input-to-ack, estimated first-output latency, device type, event queue | Device and application input path; distinguish scheduled quantization and glide from processing delay |
| Loop drifts after several minutes | Intended and observed sample/beat positions, tempo/reset epoch | Shared transport and fractional remainder math; avoid browser timer scheduling |
| RAM rises after each graph edit | Retained voice/worklet count, analyzer leases, cache growth, heap profiles | Disposal, event listeners, worker pending requests, factory/cache lifetime |
| First press of Play slow, subsequent plays fast | Cold/warm compiler and asset-load stages, known factory cache | Worker initialization/download/WASM startup, not steady-state DSP load |

The table contains hypotheses, not automatic diagnoses. Correlate timestamps and reproduce; avoid "optimization" that changes signal behavior without evidence.

### 10.2 Practical example: locate a drone clicking problem

1. Open the same saved patch and set one known safe output level. Start Playback and Reset counters after initialization; note the environment.
2. Run 60 seconds with all optional plots closed. Listen, record timestamps of audible artifacts, and export a report.
3. Repeat with all desired scopes/spectra visible. Compare context underrun deltas, renderer headroom (externally if necessary), editor timing and resource counts.
4. If cracks appear when toggling an LFO or editing frequency, reproduce a single edit against the same patch. Use the existing scope/probe to inspect sample continuity; match the operation event to the accepted revision.
5. An increasing underrun count during a load spike suggests insufficient real-time headroom. A click at the same phase-reset or graph-replacement boundary with reported underruns unchanged suggests a different DSP continuity problem; inspect attack/release/phase handling and verify with deterministic tests. An unsupported underrun counter cannot rule anything out.
6. If you instead see a red mixer peak while capacity is low and no timing pressure occurs, lower gain and measure actual master peaks. Do not "fix" clipping by increasing latency.
7. Confirm a proposed fix by rerunning the same sequence and comparing output, latency, error counts and benchmark performance. Retain the previous accepted patch and avoid changing multiple settings simultaneously.

### 10.3 Check render capacity now, before the feature is implemented

In Chrome or compatible DevTools environments:

1. Open Orbit and start a representative patch.
2. Open Chrome DevTools (F12 or Ctrl+Shift+I on Windows), then press Ctrl+Shift+P for the Command Menu.
3. Search for "Show WebAudio". Select the active AudioContext in the panel.
4. Observe Sample Rate, Callback Buffer Size and Render Capacity while the patch plays. Record readings during idle playback, live editing and with plots expanded. A value nearing/exceeding 100% indicates pressure on the renderer; it is not the total system CPU. The device callback buffer is not necessarily the worklet render quantum.
5. Use the Performance panel for UI long tasks/reflows and the Memory panel for leak investigation. These developer tools can themselves add measurement overhead; use them for focused diagnosis.
6. Compare the same test with a simpler patch and then with optional plots closed. Do not assume Bluetooth and wired outputs have the same latency.

This is also the initial external reference for validating the future Orbit monitor.

## 11. Performance improvement strategy

Measure first, optimize the dominant cost, and retain audio correctness. A faster application with broken pitch tracking or missing audio tails is not an acceptable result.

### Real-time audio

- Keep all audio-rate work in the DSP/worklet; use host controls, stateful generators and existing modulation semantics, not a high-frequency C#/JS callback loop.
- Reuse buffers and avoid allocation-heavy work, I/O, logging, locks and compilation on the real-time rendering thread. Bound feedback, delay storage, polyphony and event queues.
- Avoid recompiling on scalar controls or live keyboard/sequence events. Continue comparing topology/source identity and coalescing obsolete revisions safely.
- Profile stateful effects and large oscillator banks with realistic modulation. Optimize shared mathematical primitives/emitters and control-rate work only where it preserves required audio-rate semantics.
- Profile old/new voices during crossfade, especially expensive patches. Maintain the existing last-good graph safety contract; do not prematurely dispose sound to reduce load.
- Keep the intended distinction between mute/bypass and CPU suspension: current live controls preserve wet/state behavior; a truly suspended branch could reset or lose its tail. Only design a separate explicit optimization with tested semantics.
- Treat sample rate and latency hint as user/device configuration decisions. Increasing the buffer can improve reliability at the cost of responsiveness; reducing sample rate can reduce per-second computational demand but affects fidelity/filter limits and may force a context restart. Do not silently change them; current context creation is user-gesture-owned and browsers may ignore requested latency.

### Editor and analysis

- Measure expensive layout, canvas re-render and JavaScript interop before optimizing. Keep display-only state independent from audio commands.
- Coalesce render updates; avoid triggering whole graph rerenders for telemetry or the performance monitor. Respect the established 25 Hz effective-parameter telemetry rather than increasing rates unnecessarily.
- Reuse analysis capture/FFT buffers and capability-discovered views; prioritize visible/expanded plots within bounded channel/lease capacity. Hidden/off-screen views should not draw unnecessarily. Test lower diagnostic refresh rates and adaptive resolution only as explicit choices; never silently alter sound or existing user visual preferences.
- Reduce avoidable layout shifts, redundant measurements and large per-event allocations. Maintain pointer/keyboard usability and design tokens.
- For startup/compilation profile worker loading, source generation, compile cache, factory initialization and JS/.NET asset download separately before changing dependencies or runtime packaging. Public benchmark patch cold startup is not the same as warm live replacement.

### Diagnostics export and engineering policy

A report should include:
- app/build revision, diagnostic schema version, timestamp/elapsed sample window and available environment fields;
- relevant metrics, units, availability and source; observed graph/voice counts (not all patch details by default);
- bounded history/events with accepted/superseded/failed labels;
- capability flags and errors, with sensitive data stripped; optional patch export goes through the existing explicit export workflow;
- explicit distinction between simulated/illustrative data, real browser-reported data and measured benchmark results.

A diagnostic report must not contain arbitrary raw audio, keyboard event stream, complete graph JSON, browsing history, user paths, credentials, fingerprint identifiers or personal data. Provide clear opt-in for attaching a patch. A report may be copied or downloaded locally; the feature must not introduce a network upload.
