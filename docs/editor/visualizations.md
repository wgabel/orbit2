# Node visualizations

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [signal observation](../audio/signal-observation.md) · [canvas](canvas.md) · [design system](design-system.md).

<a id="signal-analysis-and-visualization-planned-phase-618"></a>

### Signal analysis and visualization (Phase 6.18 — partial)

**Status:** shared observation, scope/meter, FFT spectrum, integrated views and filter/EQ response (6.18.1–6.18.5) implemented. Dedicated sinks (6.18.6) and advanced history/comparison (6.18.7) planned.

Visualization is a shared observation system, not a DSP effect or a second audio engine. Distinguish actual live samples from a calculated/configured response. An oscillator's ideal shape is useful before playback; its live output can differ substantially due to modulation, anti-aliasing, mute and processing. Do not present a theoretical preview as measured audio.

| Visualization mode | Data | Typical use |
| --- | --- | --- |
| Oscilloscope | Audio-rate samples against elapsed time; adjustable timebase and optional trigger | Oscillator waveforms, LFOs, modulation and processed signals |
| Spectrum analyzer | FFT magnitude in dB versus frequency (logarithmic Hz axis for audio) | Harmonics, noise colors and effect output |
| Level meter | Peak and RMS over an explicit time window | Mixers, gain staging and clipping diagnosis |
| Frequency response | Calculated gain in dB versus frequency, based on processor configuration and sample rate | Parametric EQ and resonant filters |
| Spectrogram (later) | History of spectra versus time | Evolving drones and textures |
| Envelope preview (future envelope nodes) | Defined amplitude/control trajectory versus time or phase | Envelope generators and automation |

**Integrated UI.** Extend the reusable `NodeFrame` content with an optional `NodeVisualizationPanel` for eligible nodes. Use a small collapsible plot (initially 60–80 px high at normal zoom, design-token controlled), compact mode selector (waveform/spectrum/level/response as supported), input/output selector for eligible signals, and an accessible expand button. The expanded view can offer adjustable timebase, amplitude scaling, spectrum resolution, axes, cursor readout and comparisons without permanently increasing canvas height. Use the same plot components for compact and expanded views, root and nested canvases. Header and plot controls must not initiate node drags; layout changes must update measured port geometry. Indicate live, stopped, unavailable, preview and limited-capacity states explicitly, not only with color. An unconnected node that is not executing may display a labelled ideal preview if one is supported, otherwise report no live signal. Do not start audio merely to populate a preview.

**Reusable components and capability discovery.** Resolve supported modes and default observation targets through a registry keyed by node type/port capability, rather than a switch over DSP node names in Razor. `OscilloscopeView`, `SpectrumAnalyzerView`, `LevelMeterView` and `FrequencyResponseView` consume analysis/response data, not domain objects with browser handles. Reuse axes, scaling, plot viewport and controls where practical. Static styling (height, grid colors, traces, typography, spacing, focus and expanded-panel surfaces) uses existing JSON design tokens. Presentation settings such as visibility, mode, selected port and timebase belong to graph-qualified canvas layout preferences; extend the versioned workspace layout with backward-compatible defaults. Do not store live samples, FFT buffers, response caches or per-frame readings in patches or graph history. Toggling visibility, expanding, switching analysis modes and ordinary analysis subscriptions produce no graph command, undo entry, audio restart or graph compilation.

**Implemented Phase 6.18.4.** `NodeVisualizationCapabilities` discovers active mono audio/modulation ports for all node types and prefers output to input. `NodeVisualizationPanel` is composed once in `NodeFrame` and reused as the expanded workspace overlay; it replaces the old standalone scope panel. Both render the existing `SignalScope` with the same `scopeView` renderer and capture service. A compact plot is initially collapsed, with a tokenized 72 px waveform/spectrum canvas, source/mode selectors and peak/RMS readouts. Level mode uses only the meters. The expanded panel exposes all existing timebase/FFT settings. Opening an expanded plot unmounts that node's compact renderer, and closing restores it when configured visible. Only one expanded node per graph is stored; the existing selected-node scope button remains a shortcut. Intersection and document visibility pause animation and release leases; collapse, source change and graph navigation dispose inactive views. No separate DSP, fake previews or extra observation channels are added. Unavailable taps may reflect reachability or capacity.

`CanvasStateService` owns immutable node-qualified `NodeVisualizationSettings` and one expanded node ID in its local snapshot. `CanvasChangeKind.VisualizationChanged` triggers the existing autosave and post-render geometry measurement, not a graph edit. `SavedCanvas` schema 1 adds optional preferences and expanded node fields; missing fields use collapsed defaults, invalid settings normalize to supported choices, deleted ports fall back and dormant preferences reactivate when valid. Node deletion prunes preferences, with bounded restoration on graph undo; expanded selection closes. `CanvasNavigationService` captures each graph's independent preferences alongside positions and viewport. No runtime telemetry is serialized. Tests cover nested layout round-trips, old documents, invalid choices, history isolation, compact/expanded live modes, settings persistence and no playback revision change.

**Visualization sizing.** Both panels and their nested grids use shrinkable tracks; signal labels and select controls cannot expand the node. The plot canvas has a CSS-constrained width and inline-size containment so the high-DPI drawing buffer cannot feed back into intrinsic layout sizing. Meter tracks shrink independently of numeric readouts. Published browser tests assert that compact controls, plots and meters stay within node bounds in all three modes, including long custom rack port labels and a device-pixel ratio of two.

~~~mermaid
flowchart TD
    L[Canvas visualization preferences] --> N[NodeVisualizationPanel]
    N --> C[Compact view]
    N --> E[Expanded overlay]
    C --> S[Shared SignalScope]
    E --> S
    S --> T[Generation-qualified signal capture]
    L --> P[SavedCanvas layout]
~~~

**Processor response.** A frequency response plot is not a waveform, spectrum or amplitude envelope. For parametric EQ, frequency is the bell center, gain is the boost/cut and Q controls bandwidth. For filters, the same plot renders low-pass, high-pass, band-pass or notch responses. Define a pure numerical response contract/registry in `Orbit.Audio` for eligible DSP implementations. Derive frozen-coefficient transfer functions from the actual `FaustStateVariableFilter` algorithm, including its sample-rate frequency clamp, prewarping and gain-dependent damping; share coefficient definitions where feasible and test analytical predictions against offline Faust measurements. Do not substitute an unrelated browser biquad approximation. Use a logarithmic frequency axis up to Nyquist and a dB magnitude axis. Show the base-parameter response when stopped using a declared reference sample rate if no actual context exists; during playback use the active sample rate and optionally overlay the response at the latest effective parameter values from existing parameter telemetry. Time-varying modulation is not a stationary transfer function: label effective curves as instantaneous estimates, not exact spectral measurements. Mute/bypass state should be visible; distinguish the configured wet response from the currently passing signal. Later overlay measured input/output spectra on the same axis, keeping relative transfer gain and absolute signal magnitude clearly distinguished.

**Implemented Phase 6.18.5.** `IProcessorResponseAnalyzer` resolves configured and complete effective parameter snapshots through the pure `ProcessorResponseAnalyzer` registry. `StateVariableResponse` evaluates the bilinear-transform transfer magnitude of the emitted two-integrator filter, sharing its 0.45 × sample-rate cutoff limit. A bounded 161-point logarithmic curve plus the exact clamped center captures narrow bells and notches without infinity in render data. Both compact and expanded `NodeVisualizationPanel` instances expose Frequency response only for supported processors; the shared `ProcessorResponseView` is a tokenized SVG chart with relative gain in dB and 20 Hz–Nyquist frequency ticks. It uses a labelled 48 kHz reference before the audio clock is known and the observed context rate afterward, and caches unchanged curves. A dashed effective curve uses only current, complete modulation telemetry for the node and is explicitly an instantaneous estimate; unavailable telemetry removes that curve. The configured wet response remains visible when muted or bypassed, with a separate audition notice. Switching to response unmounts the sampled-signal scope; no additional DSP tap, sample transport, graph mutation or analysis timer is introduced. The existing persisted canvas mode preference accepts response for the two supported processor types and normalizes it for others. Numerical and offline Faust tests compare center, endpoints, boost/cut, Q, high-frequency clamp and measured response at 44.1 and 48 kHz. Implementation: [PR #4](https://github.com/wgabel/orbit2/pull/4).

**Dedicated analyzer node.** Integrated plots provide quick inspection without adding cables. A separate passive `Analyzer` sink can later be connected to any compatible audio or continuous modulation source, independently positioned and resized, with the same plot components. It adds no audible output, no delay to existing paths, and no DSP effect. Its input is an explicit observation root alongside the master sink, with ordinary validated graph commands, nested graph identity, persistence and undo/redo. Only the master remains the audible sink. A source connected solely to an analyzer may be evaluated for observation during playback; do not silently connect it to the master. Initial analyzer input is mono, with multi-channel selection deferred to the channel model.

~~~mermaid
flowchart TD
    A[Oscillator] --> M[Mixer]
    B[Noise] --> M
    M --> R[Reverb]
    R --> O[Master output]
    M --> V[Analyzer sink]
    M -. implicit observation .-> P[Integrated mixer plot]
~~~

The runtime signal tap design is described in [Faust and real-time execution](../audio/signal-observation.md#signal-observation-and-analysis-phase-618). Both forms consume the same backend; an integrated visualization remains preferable for routine inspection, while dedicated analyzers support comparison and arbitrary routing without cluttering every node.
