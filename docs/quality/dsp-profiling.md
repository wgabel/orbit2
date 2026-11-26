# DSP profiling and benchmark workflow

[Architecture overview](../architecture/overview.md) · [Roadmap](../roadmap/overview.md)

Related: [performance](performance.md) · [dsp engine](../audio/dsp-engine.md) · [testing](testing.md).

<a id="phase-734--benchmark-baseline-in-review"></a>

## Phase 7.3.4 — Benchmark baseline (implemented)

[PR #58](https://github.com/wgabel/orbit2/pull/58). These are **developer/test workflows**, not an ordinary audio meter or automatically loaded stress patch.

### Run the baseline

From the repository root with .NET 10 and Node 24:

```sh
npm install --no-audit --no-fund --ignore-scripts --no-save @grame/faustwasm@0.19.0
dotnet run --project tests/Orbit.Audio.Probe/Orbit.Audio.Probe.csproj --configuration Release -- --benchmark benchmark-output
ORBIT_FAUST_DIR="$PWD/node_modules/@grame/faustwasm" node scripts/benchmark-dsp.mjs benchmark-output benchmark-output/offline-report.json
```

In PowerShell set `$env:ORBIT_FAUST_DIR = "$PWD/node_modules/@grame/faustwasm"` before running the Node command. Optionally set `ORBIT_REVISION` to the exact checked-out commit (CI supplies GITHUB_SHA). For larger 100/200-oscillator graphs, explicitly append `--large` to the .NET probe command, in a separate output directory. Default profiles remain bounded: 1/10/50 sine oscillators, 10 mixed carrier waveforms with 10 modulating LFOs, 10-source filter/EQ/chorus/delay/reverb chain, four nested layered drones, and the same ten-sine graph with no allocated taps, eight allocated idle slots, one active tap and eight active taps. Synthetic profiles use ordinary validated graph commands, safe gain staging, independent frequencies and identities. The exporter verifies restoration via the actual patch serializer; preserve the generated fixtures to repeat identical source identities. Re-created profiles have stable recipe version/parameters but fresh GUIDs; use source hashes to identify an exact build.

The output directory contains `fixtures.json`, importable `*.orbit2.json` files and `offline-report.json`. The report records source-generation samples (three warm C# builds), first unique source's factory compile and compiler initialization, independent instance creation/binding, warm-up and offline rendering distribution. Render settings are 128-frame blocks, one audio second warm-up, 44.1/48 kHz, five repeats and two measured audio seconds per default repeat (rounded up to whole blocks); `ORBIT_BENCH_SECONDS` accepts 1–10 and `ORBIT_BENCH_REPEATS` 3–10. CI uses one measured second and five repeats. The report records actual frames, sample rates, output count, reachable nodes/edges, selected taps, source hash, software/OS/CPU and revision. Timed scope is the vendor offline render call including allocating/copying output buffers; validation/hashing is outside timing. It is **not** Faust-only kernel or audio deadline time. Each render must be finite, non-silent at the master, under the fixture peak bound and have the expected channels; paired tap variants must preserve the exact mono master hashes, selected tap channels must be non-silent and unused slots silent. Compare throughput (higher is faster) and realtime factor (lower is faster) on the same runner/settings, not universal node limits.

### External live renderer and lifecycle

After preparing browser compiler assets and publishing the Release app (see root README and CI workflow), install `playwright-core@1.65.1` for the benchmark tool; set `ORBIT_PLAYWRIGHT_DIR` to its package directory and `ORBIT_CHROME` to a local Chromium/Chrome executable.

```sh
node scripts/benchmark-live.mjs benchmark-output benchmark-output/live-report.json sine-bank-1 sine-bank-10
```

The script serves `publish/wwwroot` on loopback (override `ORBIT_BENCH_PUBLISH_DIR`), opens a fresh browser context per profile, imports the fixture through **More → Patches → Import JSON**, plays it with plots closed, warms up and samples CDP's [WebAudio.getRealtimeData](https://chromedevtools.github.io/devtools-protocol/tot/WebAudio/#method-getRealtimeData) once per second. Chromium reports render capacity in percent (100% is full render budget). The script does not multiply it again. It requires advancing context time; unsupported CDP or non-advancing samples yield unavailable rather than zero. Results retain raw samples, distribution, browser version, actual rate/buffer/quantum when available and Performance Monitor readings (open **More → Performance** for each snapshot, then close both panels before editing or sampling). The first profile also times a base-frequency edit without source rebuild and a structural square-wave edit with one accepted replacement, checks cleanup to zero worklets after Stop, and replays/stops the current patch. These UI action-to-status timings include browser automation and graph update, not physical input-to-sound delay or exact crossfade peak. Default sample duration 10 seconds, `ORBIT_BENCH_LIVE_SECONDS` 5–120; CI uses five seconds. Default headless and muted; optional `ORBIT_BENCH_HEADFUL=1` is still muted, for headed rendering comparison. Select other non-tap profile IDs as needed. Offline tap selection is separate from actual browser plot/lease overhead.

### Find the CI results and use them

**Monitor session scope ([PR #76](https://github.com/wgabel/orbit2/pull/76), integrated with [PR #77](https://github.com/wgabel/orbit2/pull/77)):** the benchmark explicitly opens More → Performance before Play to collect initial compilation metrics, then closes both for warm-up and external renderer sampling. The report records `monitorOpenDuringRendererSampling: false`. Reopening after sampling starts a fresh session: the `steady` snapshot shows current resources, not timings or underrun history from the closed interval. Keep that session open across control edits, replacement, Stop and replay; assert no source build for a base-value edit, one for the structural edit, and a fresh build on replay. The desktop More popover stays open during canvas and playback interactions while diagnostics are expanded; closing it, pressing Escape or switching tools ends collection. This preserves the original lifecycle checks while excluding monitor overhead from steady-state CDP measurements.

Open the successful **Build and deploy** run → **Artifacts** → `orbit-dsp-benchmark-<commit>` (30-day retention). Save a known-good mono report and its fixtures locally before changing 7.5. Compare matching profile/rate/settings on the same machine and repeat across runs; shared-host timing is evidence for trends only. In-app DSP load remains Not measured: the production integration offers host-side timing, not a validated real-time compute timer. The external benchmark supplies an honest fallback. Physical-device tests are still necessary for audible glitches, larger sustained sessions, occupied analysis views, and replacement peak capacity; use DevTools WebAudio and the passive monitor while exercising the imported patch for 60 seconds and repeating the same changes. Do not derive live CPU percentage from offline throughput. Repeat 7.3.4 with each unison/stereo increment and add paired voice-count/channel profiles to this versioned harness.

## 8. DSP profiling and headroom

### 8.1 Measure before claiming live DSP percentage

The most valuable long-term audio metric is rendering headroom: the fraction of the real-time processing budget still available. However, the browser's application-facing APIs do not guarantee an accurate whole-renderer CPU meter. The user must be able to trust the displayed number.

Start with three measurement paths, explicitly distinguish them:

1. **External live renderer measurement (developer workflow):** Chrome DevTools WebAudio Render Capacity measures time spent rendering the graph relative to its budget. It includes browser audio graph work, not just Orbit's Faust kernel. Use the DevTools UI manually or controlled Chrome DevTools Protocol tooling in benchmark automation; not in the production page. Record browser version and scope.
2. **Validated optional in-app DSP timing:** investigate how the pinned FaustWasm AudioWorklet actually calls its compute function. If a reliable monotonic timer is accessible, instrument only the relevant compute region and sample minimally. Validate timer precision and overhead; if unavailable, leave the in-app metric unsupported. Instrumentation must not alter the signal, schedule audio, create a second worklet, or fork generated vendor code unnecessarily. A measured "Faust compute load" excludes other graph/worklet/device work and is not the same as renderer capacity. If rendering quanta cannot be timed accurately, do not infer them from messages or callback gaps.
3. **Offline DSP benchmark:** compile deterministic patches and render a known number of frames as fast as possible. This measures throughput and relative algorithmic cost, not real-time scheduling, device latency or live underruns. Reuse the existing Orbit.Audio.Probe and compiled Faust test infrastructure. It is valuable across machines even when in-app live timing is unsupported.

A single worklet exception or difference between audio clock and wall clock cannot be converted into a trustworthy CPU percentage.

For optional timed profiling, accumulate processing-time histogram/counters over a bounded interval (e.g. five seconds), sample count, mean, p95 estimate, max, budget-exceedance count and budget fraction. Allocate all storage before rendering. Aggregate externally where possible; use fixed histogram bins and avoid per-block sort or message posts. Display the actual timed scope, sample rate and block size. If multiple worklets run during crossfade, separate per-generation observations and report concurrent rendering context; sum only compatible measured scopes in the same rendering interval, never sum unrelated p95s as an exact total. Do not profile a stale generation. A measured 0.75 ms kernel plus 0.1 ms observed analysis work is not automatically the full 0.85 ms renderer load.

**Instrumentation acceptance gate:** compare identical patches with profiling off/on. Timed profiling may ship only if its overhead is quantified and it preserves deterministic output and realtime behavior. If a measurement requires more privileged browser capabilities (such as debug protocol tooling or cross-origin isolation), make it explicitly developer-only, optional and separately documented. Do not change deployment/security policy merely to show a prettier meter.

### 8.2 Real-time factor and throughput

For offline tests, specify the meaning of the ratio; different audio tools call its inverse "real-time factor". Use explicit labels:

```text
audioDurationSeconds = renderedFrames / sampleRateHz
offlineRealtimeFactor = renderWallSeconds / audioDurationSeconds
throughputTimesRealtime = audioDurationSeconds / renderWallSeconds
framesPerWallSecond = renderedFrames / renderWallSeconds
```

Example: render 10 seconds (480,000 frames at 48 kHz) in 0.5 wall-clock seconds. Offline realtime factor = 0.05, throughput = 20× realtime, frames/s = 960,000. Rendering 10 seconds in 12 seconds yields factor 1.2 and throughput 0.833× realtime. This is an offline processing comparison, not a 5% guaranteed live DSP load reading: real-time buffering, worklet overhead, browser scheduling and compilation are different.

Warm up WebAssembly and JIT (just-in-time compilation) before steady-state profiling. Separate warm-up time, source generation, factory compilation, instance creation, and processing. Reuse a fixed block size and deterministic seed/input; avoid timing sample array allocation, JSON serialization or test assertion work as DSP compute. For an end-to-end render benchmark, time those stages explicitly instead. Repeat, report distribution and environment; do not use a single best run.

### 8.3 Node-level cost estimates

Orbit's compiler flattens and composes the connected graph into one Faust program; expressions may be shared, simplified or optimized. It is not generally possible to attach a stopwatch around every visible UI node within that single program without changing the generated DSP and biasing measurements. A node can have a different incremental cost at a different sample rate, modulation configuration, waveform, filter mode, delay allocation, output channel count or graph topology.

Instead develop a reproducible **incremental-cost benchmark**:

- Establish a minimal reachable baseline (source and master, required common plumbing) with identical output channel/capture configuration.
- Build paired test graphs containing N representative instances (e.g. 1, 10, 50) of a node type, all reachable via a safe mixer. Control input amplitude, ensure finite output, vary inputs enough to prevent accidental constant elimination, and measure all configurations and sample rates of interest.
- For processors, compare a graph with N pass-through/known baseline paths against N real processors with the same source and mixing cost. Ensure optimization does not eliminate useful work. Use a stable seed for noise and shared versus independent inputs as separate profiles.
- Subtract median baseline wall processing time from median candidate time over the same number of frames. Report a relative incremental cost per N instances where the measurement exceeds noise. If baseline variance dominates, label inconclusive rather than negative CPU.
- Include mixed graphs and true live renderer capacity tests to see whether stand-alone trends generalize. A muted node may still compute; bypass can intentionally preserve wet processor state. Do not label muted/bypassed nodes "free" without measurement.
- Cache summaries per benchmark profile/version, not as a fixed universal per-node weight. A cost estimate under one browser/device is not a promise about another.

Example hypothetical results for 10 seconds of steady-state offline DSP at 48 kHz:

| Same benchmark setup | Median wall processing time | Difference from baseline |
|---|---:|---:|
| Paired baseline (sources/mixer) | 200 ms | — |
| Baseline + 20 filters | 260 ms | +60 ms |
| Baseline + 20 reverbs | 440 ms | +240 ms |

The test indicates a larger incremental cost for that reverb configuration. It does not establish that every reverb is four times more expensive than every filter, or that 20 reverbs consume exactly 2.4% of the actual device renderer. Use estimates to guide an optimization investigation. A later inspector may describe expensive node categories (e.g. "Measured representative cost — high") with profile provenance, but never show fabricated precise percentages on each node.

## 9. Benchmark patches and regression workflow

### 9.1 Reusable benchmark fixtures

Generate deterministic patches through existing validated graph commands or the existing probe/fixture path, with a consistent root master and unique graph/port identities. Do not hand-edit invalid JSON or create disconnected nodes then assume they burden the DSP compiler. Keep fixtures ordinary editable Orbit patches where useful for manual reproduction; create programmatic larger fixtures in test code when UI usability would suffer. Save reproducible patch IDs, parameter values, seeds, graph size and fixture version. Do not add them to every user's preset catalog unless appropriate.

| Fixture | Variants | Purpose / metrics |
|---|---|---|
| Baseline sine → Gain → Master | 44.1/48 kHz; mono; analyzers closed | Establish startup, steady-state and control-update baseline |
| Oscillator bank | 10, 50, 100, 200 reachable oscillators into mixer; independent frequencies | Scaling, DSP throughput, reachable counts, peak safety, compilation |
| Waveforms and audio-rate modulation | Sine, saw, triangle, square with duty; 1/10/50 LFO routes | DSP cost, continuous frequency changes, no phase reset |
| Layered drones | 1, 4, 8, 16 nested drones, delay/reverb/chorus where applicable | Flattening, realistic sustained sound, runtime headroom, tails |
| FX chain | Filter, EQ, chorus, delay, reverb; repeat and parallel routing | Cost of DSP algorithms, bounded memory, clipping vs processing errors |
| Dense graph editor | Disconnected vs reachable nodes, 50/100/200; panning, zoom, node/edge drags | UI layout, connector geometry, graph compilation should skip unreachable/source-identical cases |
| Modulation/telemetry | Many active controls and incoming modulation sources | Interop payloads, meter refresh and responsiveness |
| Analysis-heavy | Same patch with 0/1/4/maximum supported views/capture channels; waveform vs FFT | Compare analyzer overhead and cleanup, respect existing channel/lease caps |
| Graph edit stress | Rapid slider updates, routing, amount changes, undo/redo, valid replacements, stale compile | Compile frequency, replacement latency, concurrent voices, no unintended recompilation |
| Lifecycle endurance | Repeated Play/Stop, patch switch, panel toggle, repeated crossfade; 10–30+ minute sustained run | Resource counts, memory trends, cleanup, underruns and stability |
| Musical timing (after event/sequence delivery) | Multiple sequencers, 120 BPM and tempo edits; 20-second drone steps and multi-minute loops | Frame error and drift; event queues; wrap, reset, suspension |
| Oscillator unison / stereo (Phase 7.5) | Mono baseline, 1/2/3/5/7/8 voices (16 experimental); same layered patch in mono/stereo; 0/1/max taps; replacement under load | Voice-count and channel expansion, gain bounds, audible-channel isolation, peak swap cost; see [Phase 7.5 benchmark profiles](../audio/stereo-panning-and-unison.md#1-performance-first) |
| Instrument/polyphony (after voice allocation) | 1/4/8/16 configured and active voices; rapid retrigger, tails | Voice allocation/stealing, worst-case load, peak headroom, memory |

Use safe gain staging. Example: for 100 oscillators with nominal magnitude 1, a conservative summed gain of 0.5/100 before master gives an upper bound of 0.5 if all waves align; add headroom where modulation/effects invalidate simple bounds. Do not make a loud test patch to demonstrate performance. Limit maximum complexity and test duration, make large/stress fixtures opt-in. Never auto-load a 200-node stress case for every user.

### 9.2 Reproducible workflow for developers and agents

1. Record environment: Orbit commit/build, browser/engine and version, OS, CPU when manually supplied, sample rate, audio output type (speaker/wired/Bluetooth; optional manually supplied), battery/power mode, display cadence, visible tabs/plots, audio buffer information where available, graph fixture version. Do not collect invasive fingerprints.
2. Build using current README instructions; distinguish development build from the deployed/published Release build. Startup/download/bundle cold-cache results are separate from warm DSP results. Ensure generated Faust compiler/tools match the pinned versions.
3. Run functional tests first; performance claims do not replace numerical DSP safety and correctness. Verify finite outputs and safe peak levels for test graphs.
4. Warm up, then run the identical fixture several times with fixed sample duration and configuration (suggest 3–5 repeats for a quick investigation, more when noise is high). For live tests use a meaningful duration and a defined workload, e.g. 60 seconds idle playback and 30 seconds of repeated controlled editing. Include min/median/p95/max where justified by sample count and report variance.
5. Capture offline throughput and live renderer capacity/underruns separately. Compare monitor hidden vs visible, passive vs profiling, and no-analysis vs multiple plots. Report CPU/load scope and missing capabilities.
6. Compare the previous accepted baseline on the same environment. Record both absolute numbers and relative change with known variability. If a result looks implausible, rerun and isolate cache, build, thermal or hardware variance.
7. Store an optional bounded JSON or CSV benchmark artifact in CI/diagnostics (not in every user's patch). Include fixture identifier, version, all settings, metrics and units, output validity checks and exact app revision. Never automatically transmit user patches or musical key data.

A simple regression calculation:

```text
relativeChangePercent = 100 × (candidate - baseline) / baseline
```

Example: median compiler round trip rising from 200 ms to 250 ms is +25%; offline throughput dropping from 20× to 16× is −20%. Direction matters. A 1 ms rise from 1 to 2 ms is +100% but may be within timing noise; decide with an absolute minimum threshold and repeated runs. Flag large regressions for investigation rather than declaring a universal limit before calibration.

### 9.3 CI strategy

Functional tests use deterministic fake clocks, synthetic timings and compiled signal fixtures; they must not rely on arbitrary sleep or an unstable CI host to pass. Add deterministic performance contracts (bounded buffers, no unnecessary recompilation, no growing event queue, correct session deltas, stable generation checks) to normal CI. A separate performance job can measure throughput against a published baseline. Shared-host wall-time fluctuations make strict timing gates flaky; initially report trends with generous noise margins and enforce only well-established regressions on comparable runners. Test performance in a real browser for UI/latency/capabilities and on actual laptops for audio underruns; headless/offline CI cannot guarantee device playback quality.

Define pass/fail targets through measured baselines at each supported sample rate. Candidate initial engineering targets (not claims):
- no valid reported underruns during standard representative 60-second foreground patch on a documented reference machine;
- monitoring changes no samples, graph revisions or runtime identity; demonstrate negligible overhead through paired runs (start with <2 percentage points of observed renderer capacity when measurement supports that precision, otherwise report uncertainty);
- no leaked worklets/capture leases across repeated cycles;
- no per-note compile, and no compilation for base-only or source-identical changes;
- stable UI frame cadence for normal editing on measured reference hardware, not a universal 60 FPS requirement.

### 9.4 Device-specific capacity and reference compositions (planned)

Phase 7.3.8 extends the existing fixture and report harness with an explicitly requested, bounded device-specific diagnostic. The purpose is to establish an interpretable capacity baseline on a user's phone or desktop and make the [health targets](performance-metrics.md#31-suggested-health-thresholds) actionable. Results are scoped to the actual device, power/thermal state, browser, sample rate, output route, display cadence, app revision and fixture version. Re-run after meaningful changes to that environment.

| Reference tier | Target reachable leaf nodes (recursive) | Intended performance goal (not a current claim) |
|---|---:|---|
| Simple composition | ~50 | Excellent audio/editing on supported reference mobile hardware |
| Complex composition | ~200 | Smooth audio/editing on supported reference mobile and desktop |
| Advanced composition | ~500 | Smooth audio/editing on supported reference desktop |
| Stress test | 1,000+ (explicit optional limit) | Find device-specific limits, not a production pass requirement |

Use composition-like mixed workloads (voice generators, audio-rate modulation, envelopes, sequencing, filters/effects and several nested composites), not just cheap sine nodes. Record exact reachable flattened leaf count, editable recursive count, connections, voices/channel and tap/plot configuration and actual compiled source identity; equal node counts do not imply equal cost. Retain the existing oscillator and FX fixture families for isolating costs. Include baseline and repeatable tiers, e.g. no plots versus representative open plots, stable playback versus drag/zoom/control changes and replacement, and paired mono/stereo/unison when available. Ensure numerical validity and conservative output levels.

Start with a light uncrowded foreground baseline to establish observed display cadence; never calibrate to the FPS of an overloaded patch. Test each selected tier for at least ~60 s steady playback plus a bounded editing workload, repeat where practical and include 10–30 minute endurance separately. Record p95/peak render capacity only from a validated measurement with scope; otherwise report headroom unavailable and compare offline throughput as a separate metric. Track valid underruns/missing audio, audio errors, UI frame intervals, compilation stages, cleanup and actual sound checks where possible. Aim for p95 30–50% render load in normal complex use, below 60% as Good, 60–85% as Caution; no reliable device-specific headroom claim if live load is absent. If optional underrun stats are absent, state that audio reliability is not fully verified.

Provide opt-in Start/Stop controls and explicit test cost, duration and staged limits. Run stress profiles in a separate diagnostic workflow with ordinary patch playback stopped; preserve the user's patch, transport, undo and saved content. Stop promptly on reported interruptions, errors, sustained critical pressure, cancellation or unexpected resource growth; release all temporary resources on completion/abort. Do not automatically start from the normal editor, emit loud test audio or upload results. A local report shows the highest tested passing tier by separate audio and UI criteria, known limitations and short reasons for each health badge. If the 1,000+ tier is unsafe or unnecessary on a device, do not run it; report the tested range, not an extrapolated maximum node count. These are target workloads, not fixed production node limits or an automatic quality-reduction policy.
