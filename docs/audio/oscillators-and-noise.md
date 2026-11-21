# Oscillators and noise

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [transport and timing](transport-and-timing.md) · [parameters and modulation](../core/parameters-and-modulation.md) · [dsp engine](dsp-engine.md).

**Implemented Phase 6.2:** The transport-locked oscillator was initially proven with a separate sine node (superseded by Phase 6.4). A generic `ParameterChoiceEditor` renders discrete parameters without adding modulation ports. `ITransportSignalSink` publishes a new transport anchor on state changes through the existing browser adapter. A lazy `TransportSignalSource` owns one audio worklet per context, producing the shared beat position at the actual sample rate. The graph compiler adds one beat input only when an emitted node requests it through `FaustNodeContext.TransportBeat`, including for nested emitters. The same beat stream reaches old and new graphs during a crossfade. All supported cycle lengths divide a 12-beat wrap exactly in musical time, limiting floating-point drift. Audio-rate phase is shared; application commands still arrive at message boundaries rather than being scheduled at exact future samples.

**Implemented Phase 6.3:** A single `OscillatorGenerator` (type key `generator.oscillator`) replaces the sine-only domain model. Its `OscillatorWaveform` enumeration defines sine, triangle, square and sawtooth, with shared continuous frequency, amplitude and phase parameters and stable sockets. `RequiresRecompile` marks a structural parameter; choices are validated by the same graph command system, included in topology comparison, and compiled as constants instead of live sliders. The UI uses `ParameterChoiceEditor` with no waveform-specific Razor code. `OscillatorFaustEmitter` selects the DSP expression through `FaustOscillatorWaveforms`; the waveform is evaluated at the actual sample rate, with polynomial anti-alias corrections for steps/slopes and a normalized output bound. A waveform change crossfades the compiled graph; continuous edits still retain oscillator state. A free-running waveform may restart its phase on replacement. Noise is a separate generator family.

**Implemented Phase 6.4:** `OscillatorTimingMode` selects Free or Synchronized. `FaustOscillatorTiming` resolves phase separately from waveform evaluation: free frequency integration or shared beat/division. Both paths use the same `FaustOscillatorWaveforms` implementation. The redundant tempo sine node and emitter are removed. Musical division is a live discrete control; timing mode is structural. `ParameterActivation` describes a condition on an unconditional structural selector. `GraphNodeSnapshot.IsParameterActive` and `IsInputActive` drive both the editor and backend lowering. Inactive rate settings are retained, their editors hidden, and associated sockets/wires visibly dormant. `GraphFlattener` filters dormant inputs before dependency ordering and range estimation, so dead upstream nodes are not compiled. The editable graph continues to validate dormant routes to prevent reactivation cycles. Waveform and timing replacements share the original node and socket identities; synchronized phase follows the shared beat throughout free/sync changes and crossfades. Changing division can intentionally change phase; tempo changes preserve it.

**Implemented Phase 6.5:** `NoiseGenerator` (`generator.noise`) is independent of periodic oscillator timing. `NoiseColor` is a structural discrete choice (White, Pink, Brown); amplitude is the only continuous parameter and uses the existing modulation and control manifest. `NoiseFaustEmitter` delegates spectral shaping to `FaustNoiseSpectra`. A deterministic pseudorandom recurrence uses a graph-qualified node identity to distinguish streams; pink uses octave-spaced low-pass responses, brown uses a sample-rate-aware leaky integrator. The shaped signal is bounded to ±1 before effective amplitude scaling. Color replacement uses the established graph crossfade; amplitude updates preserve DSP state. Noise can drive audio or modulation routes, with no additional UI or infrastructure implementation.

~~~mermaid
flowchart TD
    N[Noise definition] --> E[NoiseFaustEmitter]
    E --> R[Node-local random recurrence]
    R --> C[Selected spectrum]
    C --> A[Bounded signal times effective amplitude]
    A --> O[Audio or modulation route]
~~~
