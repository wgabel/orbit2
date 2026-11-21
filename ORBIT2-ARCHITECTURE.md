# Orbit 2 — Modular audio engine and hierarchical graph architecture

**Status:** Architecture proposal and implementation roadmap  
**Target repository:** [wgabel/orbit2](https://github.com/wgabel/orbit2)  
**Date:** 2026-11-20

This document consolidates the design discussions for Orbit 2: the original browser boilerplate, separation of responsibilities, signal generation, modulation, audio processing, clocks, hierarchical node canvases, exposed input/output racks, and event-driven editing. It is intended as a reference for implementing the next iterations, not a claim that the proposed systems already exist.

## Contents

1. [Goals and current baseline](#1-goals-and-current-baseline)
2. [Fundamental concepts](#2-fundamental-concepts)
3. [Signals and node contracts](#3-signals-and-node-contracts)
4. [Parameters and modulation](#4-parameters-and-modulation)
5. [Ports and connections](#5-ports-and-connections)
6. [Audio time, oscillator phase and transport](#6-audio-time-oscillator-phase-and-transport)
7. [Hierarchical signal graphs and canvases](#7-hierarchical-signal-graphs-and-canvases)
8. [Input and output racks](#8-input-and-output-racks)
9. [Commands, events and transaction boundaries](#9-commands-events-and-transaction-boundaries)
10. [Faust and real-time execution](#10-faust-and-real-time-execution)
11. [Project organization and dependencies](#11-project-organization-and-dependencies)
12. [Worked patch example](#12-worked-patch-example)
13. [Implementation roadmap](#13-implementation-roadmap)
14. [Testing and acceptance criteria](#14-testing-and-acceptance-criteria)
15. [Decisions and open questions](#15-decisions-and-open-questions)
16. [References and glossary](#16-references-and-glossary)

## 1. Goals and current baseline

### Motivation

Orbit is a browser-based environment for designing synthesizers, drones, modulation systems and musical patches. The original React application provides inspiration for the graph editor, but Orbit 2 is intended to avoid tight coupling between the presentation, application state and real-time audio processing.

The goal is a modular architecture in which new generators, processors, parameter types and composite modules can be added without rewriting the rest of the application.

### Technology choices

| Technology | Responsibility |
|---|---|
| Blazor WebAssembly / C# | Browser UI, graph model, editing, validation and application services |
| Faust | Sample-level sound synthesis and DSP algorithms |
| WebAssembly | Execution format for compiled Faust DSP |
| Web Audio API / AudioWorklet | Real-time audio scheduling and output |
| Minimal JavaScript modules | Browser audio integration and lifecycle; not application business logic |
| GitHub Actions / Pages | Build, test and deployment of the static app |

The current implementation includes the independent Orbit.Domain graph model (Phase 1) and a continuously running, fixed two-sine modulation graph (Phase 2). The browser bridge, build-time Faust compilation and separated application/UI/infrastructure projects support this playback path. Existing infrastructure concepts include AudioEngine, AudioContextManager, AudioClock, FaustModuleLoader, FaustNodeFactory, AudioVoice, AudioOutput and AudioEnvelope.

The next stage adds validated commands and application events, then a reusable graph editor and general DSP compilation. The active modulation playback path and shared infrastructure provide the baseline.

### Design principles

1. Separation of concerns: UI, graph state, application commands and real-time DSP have different responsibilities.
2. Composition over specialization: a drone is a reusable graph, not a special monolithic synth class.
3. Depend on stable abstractions and use dependency injection at composition roots.
4. Generators and processors must not know which node is connected before or after them.
5. Modulation is a first-class feature of all appropriate continuous numerical parameters.
6. Every signal uses the same authoritative audio timing reference.
7. User-interface operations must not interrupt sound unnecessarily.
8. A new node should normally require a definition, a DSP implementation and registration, not changes to the graph editor's core.
9. Use descriptive names, small cohesive classes, and separate source files for independent objects.
10. Validate graph changes, preserve consistent state and test foundational behavior before adding numerous node types.

## 2. Fundamental concepts

| Concept | Definition |
|---|---|
| Signal | A stream of values indexed by time/sample frame. |
| Sample | A numerical value for one channel at a particular sample frame. |
| Sample frame | One time position containing a sample for each channel. |
| Node | A graph object with a stable identity, ports and parameters. |
| Generator | A node that produces a signal, e.g. sine oscillator, noise generator or clock. |
| Processor | A node that transforms one or more incoming signals, e.g. filter, gain, delay, mixer or reverb. |
| Modulator | A role: any suitable generator or processor whose output influences a parameter. |
| Parameter | A configurable object with a base value, metadata and, where supported, incoming modulation. |
| Port | A named, typed endpoint through which a signal enters or leaves a node. |
| Connection | An edge linking one output port to one input port. |
| SignalGraph | The model owning the nodes, connections and graph invariants. |
| Composite node | A node that contains an internal graph and exposes selected ports at its boundary. |
| Input Rack | The internal representation of a composite node's public inputs. |
| Output Rack | The internal representation of a composite node's public outputs. |
| AudioClock | The sample-frame and audio-time reference. |
| Transport | Musical time: play position, tempo, beats, bars and synchronization. |
| Phase accumulator | The evolving phase of an oscillator cycle. |
| Patch | A complete root graph with its nested graphs, settings and layout. |

The term **modulator** does not need to be a separate mandatory inheritance hierarchy. A sine generator at 440 Hz generates audible sound; the same generator at 0.5 Hz can modulate another signal. A filter can process an audible signal or a modulation signal. A low-pass-filtered noise source can create smoothly varying random modulation without requiring an entirely separate random-modulation architecture.

~~~mermaid
classDiagram
    class ISignalNode {
        Id
        Parameters
        Inputs
        Outputs
    }
    ISignalNode <|-- IGenerator
    ISignalNode <|-- IProcessor
    ISignalNode <|-- ICompositeNode
    IGenerator <|-- OscillatorGenerator
    IGenerator <|-- NoiseGenerator
    IProcessor <|-- Filter
    IProcessor <|-- Reverb
    IProcessor <|-- Mixer
~~~

Examples of future nodes:

- Generators: sine, triangle, band-limited sawtooth, square/pulse, noise, random, envelope, stepper, clock.
- Processors: gain, mixer, pan, low-pass/high-pass filters, EQ, delay, reverb, chorus, flanger, compressor, distortion, signal mapping, rectification.
- Composites: drone, effects rack, sequencer, reusable modulation system, complete synthesizer voice.

An ADSR envelope can be a generator with trigger/gate inputs. The distinction between generator and processor is useful for describing behavior, but the graph should rely on actual port contracts rather than assuming all generators have no inputs.

## 3. Signals and node contracts

### Signal representation

A sine wave, an LFO, an envelope and an audio recording are mathematically streams of values. Physical differences come from the waveform, time scale, channel count and the meaning assigned by the destination port.

For example, a bipolar sine signal at sample rate Fs can be generated as:

~~~
sample[n] = sin(2 * pi * phase[n])
~~~

An audio signal normally uses floating-point samples with a nominal range of -1 to +1. Intermediate floating-point calculations may exceed this range; the final output needs gain management. Modulation signals may be bipolar (-1 to +1), unipolar (0 to +1), or expressed in units explicitly defined by their port.

As a conceptual read-only description:

~~~csharp
public interface ISignal
{
    int SampleRate { get; }
    int ChannelCount { get; }
}
~~~

Do not construct a C# object for every individual sample or repeatedly transmit buffers between Blazor and JavaScript. Real-time buffers and state belong inside the DSP runtime. Audio-processing buffers should be allocated/reused efficiently.

### Common node contract

The following C# examples are design sketches for the proposed model, not existing compiled application files. Types referenced together are defined throughout this document; production implementations should live in separate files.

~~~csharp
public interface ISignalNode
{
    Guid Id { get; }
    string Name { get; }

    IReadOnlyList<Parameter> Parameters { get; }
    IReadOnlyList<PortDefinition> Inputs { get; }
    IReadOnlyList<PortDefinition> Outputs { get; }
}

public interface IGenerator : ISignalNode
{
}

public interface IProcessor : ISignalNode
{
}

public interface ICompositeNode : ISignalNode
{
    SignalGraph InternalGraph { get; }
}
~~~

The interface describes a node's graph-facing contract. It intentionally contains no ProcessSample method: the C# object describes DSP, while Faust performs DSP. Avoid exposing UI component references, JavaScript objects or AudioWorklet instances in the domain model.

Generators and processors are registered by stable node type keys, e.g. "generator.oscillator" or "processor.lowpass". A NodeRegistry holds the definitions, factory and metadata required to create new instances. DSP bindings are supplied separately by the compiler adapter.

### Graph ownership and fan-out

~~~mermaid
flowchart TD
    G[Oscillator] --> F[Low-pass filter]
    G --> D[Delay]
    F --> R[Reverb]
    D --> M[Mixer]
    R --> M
    M --> O[Output]
~~~

A generator output can have several outgoing edges. The generator itself does not own a NextNode pointer or a list of concrete downstream effects. SignalGraph owns connections and determines dependencies.

A mixer defines how several audio inputs are combined. A destination port must explicitly define its fan-in policy; for example, reject a second connection, sum inputs, or require an explicit mixer node.

## 4. Parameters and modulation

### Parameter definitions and instances

Every continuously adjustable numerical parameter should be an independent object rather than just a float property on an oscillator. The parameter has identity, units, range, a base value and semantics for applying incoming signals.

~~~csharp
public enum ParameterUnit
{
    Normalized, Hertz, Cents, Decibels, Seconds, Degrees,
    BeatsPerMinute, Ratio
}

public enum ParameterScale
{
    Linear, Logarithmic, PitchCents, Decibels
}

public record ParameterDefinition(
    string Key,
    string Name,
    double DefaultValue,
    double Minimum,
    double Maximum,
    ParameterUnit Unit,
    ParameterScale Scale,
    bool CanModulate
);

public sealed class Parameter
{
    public required Guid Id { get; init; }
    public required ParameterDefinition Definition { get; init; }

    // Changed through validated application commands.
    public double BaseValue { get; internal set; }

    // Stable input socket for incoming modulation, when supported.
    public Guid? ModulationPortId { get; init; }
}

public record ParameterAddress(
    Guid NodeId,
    Guid ParameterId
);
~~~

Parameter definitions can also describe control rate, display precision, smoothing time, bipolar/unipolar mapping, modulation limits and other metadata. Some discrete parameters, such as waveform selection, are configuration rather than continuously modulatable signals and should have a distinct contract where needed.

Examples:

| Component | Parameters |
|---|---|
| Oscillator | Frequency, amplitude, phase |
| Pulse generator | Frequency, pulse width, amplitude |
| Filter | Cutoff, resonance |
| Reverb | Mix, decay, size |
| Gain | Volume |
| LFO (same oscillator concept) | Frequency, amplitude, phase |
| Drone composite | Any explicitly exposed parameters, e.g. pitch, volume, cutoff |

BaseValue represents the user/preset setting. The runtime effective value can change at every sample independently of it. Never overwrite the stored base value with successive LFO samples.

### Modulation math

For additive linear modulation:

~~~
effective[n] = clamp(base + sum(source[i][n] * depth[i]), min, max)
~~~

For pitch modulation expressed in cents:

~~~
effectiveHz[n] = baseHz * 2^(sum(source[i][n] * depthCents[i]) / 1200)
~~~

For example:

- Oscillator B has a frequency of 440 Hz.
- Oscillator A produces a 0.5 Hz sine signal ranging from -1 to +1.
- The connection has a modulation depth of 100 Hz.
- B's frequency varies between 340 and 540 Hz, maintaining continuous phase.

~~~mermaid
flowchart LR
    A["Sine A: 0.5 Hz"] --> P["Frequency: base 440 Hz"]
    P --> B["Sine B: 340–540 Hz"]
    B --> O[Audio output]
~~~

For a musical vibrato, a modulation amount of +/-20 cents is more appropriate than +/-100 Hz. The exact mapping depends on ParameterScale, and parameter limits may apply after scaling.

For volume, decide whether the modulation is a linear amplitude offset, a multiplicative gain factor, or a dB offset. For cutoff, an exponential or logarithmic mapping often provides more musical behavior than a linear Hz offset. These decisions belong to the target's parameter definition.

### Multiple incoming modulation signals

| Source output | Target parameter | Example depth |
|---|---|---|
| LFO 1 | Oscillator 1 frequency | +/-20 cents |
| LFO 1 | Oscillator 2 frequency | +/-10 cents |
| LFO 2 | Drone volume | +/-0.2 linear gain |
| LFO 3 | Filter cutoff | +/-500 Hz |
| LFO 3 | LFO 1 frequency | +/-0.1 Hz |

A single source can feed several parameters; several sources can feed a single compatible parameter. The graph's modulation connections collectively form the modulation matrix; avoid maintaining a second independently editable list of the same routes.

A possible convenience view:

~~~csharp
public record ModulationRoute(
    PortAddress Source,
    ParameterAddress Target,
    double Amount
);
~~~

The route is derived from the canonical SignalConnection and the target parameter's modulation input.

### Audio-rate and control-rate

Slow modulation and audio-frequency modulation use the same conceptual signal machinery. Fine-grained continuous modulation must run on the audio engine, not through periodic C# events or JavaScript setInterval calls.

The DSP can optimize eligible control-rate or constant signals. Manual changes to base values should normally be smoothed to avoid abrupt jumps. Smoothing must not accidentally suppress intended audio-rate modulation; apply it deliberately to base changes and other appropriate control paths.

## 5. Ports and connections

### Ports

An explicit, typed port system separates nodes from their wiring.

~~~csharp
public enum PortDirection
{
    Input, Output
}

public enum SignalType
{
    Audio,
    Modulation,
    Trigger,
    Gate,
    Clock
}

public enum FanInPolicy
{
    Single,
    Sum
}

public record PortDefinition(
    Guid Id,
    string Name,
    PortDirection Direction,
    SignalType SignalType,
    int ChannelCount,
    FanInPolicy FanIn
);

public record PortAddress(
    Guid NodeId,
    Guid PortId
);
~~~

Signal types express semantics and validation rules, not necessarily different buffer implementations. Audio and modulation can both be floating-point streams. Converting an audio-rate oscillator output into modulation is permitted when compatible with the target; use explicit mapping or channel selection when required.

A trigger represents a discrete occurrence; a gate represents a level or duration; a clock carries rhythmic timing information. Their exact runtime representations can be introduced later without changing the core port concept.

### Connections

~~~csharp
public record SignalConnection(
    Guid Id,
    PortAddress Source,
    PortAddress Destination,
    double Amount = 1.0
);
~~~

Amount can be unity gain for normal audio routing and modulation depth for parameter inputs. The units and permitted mapping are determined by the receiving port/parameter, with optional future mapping configuration.

Each graph has a unique ID; node and port addresses are scoped within their graph. A fully qualified address includes the graph or composite instance path when flattening nested graphs.

Typical connection types:

| Source | Destination | Result |
|---|---|---|
| Generator output | Processor audio input | Sound processing |
| Generator output | Parameter modulation input | AM, FM, LFO modulation |
| Processor output | Parameter modulation input | Filtered/random/shaped modulation |
| Clock output | Sequencer clock input | Rhythmic synchronization |
| Envelope output | Gain parameter | Amplitude shaping |

### Validation

SignalGraphValidator verifies:

- Source/destination exist and have output/input direction respectively.
- Port types and channel counts are compatible or have an explicit adapter.
- The destination's fan-in rules are respected.
- Duplicate connections are rejected.
- Zero-delay dependency cycles are rejected, including cycles through modulation parameters and subgraph boundaries.
- Intentional feedback is allowed only via an explicitly supported delayed feedback mechanism; not all ordinary delay effects necessarily make arbitrary graph feedback safe.
- Connections never point to deleted nodes or ports.

For the first implementation, reject all cycles. Introduce explicit feedback later when the DSP compiler and tests support it.

## 6. Audio time, oscillator phase and transport

There are three independent concepts of time that must not be confused.

| System | Meaning |
|---|---|
| AudioClock | Absolute elapsed audio frames and sample rate |
| PhaseAccumulator | Progress through an individual generator cycle |
| Transport | Musical beat position, tempo, start/stop and synchronization |

### AudioClock

At 48 kHz, each channel has 48,000 samples per second. All nodes must use the same audio context's frame timing.

~~~csharp
public interface IAudioClock
{
    long CurrentSample { get; }
    int SampleRate { get; }
    double CurrentTime { get; }
}
~~~

In the real-time runtime, Web Audio's AudioWorkletGlobalScope supplies currentFrame, currentTime and sampleRate. The C# interface can expose a snapshot to the UI; the DSP itself uses its actual rendering context. It is not a separate C# timer.

~~~
timeSeconds = sampleFrame / sampleRate
samplePeriod = 1 / sampleRate
~~~

| Frames at 48 kHz | Duration |
|---:|---:|
| 1 | 20.833 microseconds |
| 128 | 2.667 ms |
| 480 | 10 ms |
| 24,000 | 0.5 s |
| 48,000 | 1 s |

Request 48 kHz if desired, but use the AudioContext's actual supported sample rate rather than assuming it. The engine must also work at 44.1 kHz and other supported sample rates. Use the actual provided buffer size rather than hardcoding 128 frames.

### Phase accumulator

An oscillator stores a fractional phase between 0 and 1. For each sample:

~~~
output[n] = sin(2 * pi * phase[n])
phase[n+1] = wrap(phase[n] + frequency[n] / sampleRate)
~~~

Example: 440 Hz at 48 kHz advances by 440 / 48000 = approximately 0.0091667 cycles per sample; one cycle lasts approximately 109.09 samples.

Frequency can change on every sample. Advance from the previous phase rather than recalculating phase as currentTime * currentFrequency, which can create discontinuities when the frequency changes.

At phase 0 the sine output is 0; at phase 0.25 it is +1; at 0.5 it is 0; at 0.75 it is -1.

A phase reset should be an explicit trigger with a defined behavior, not an accidental consequence of editing a parameter or reopening a canvas.

### Musical transport

~~~csharp
public interface ITransport
{
    double BeatsPerMinute { get; }
    double BeatPosition { get; }
    bool IsPlaying { get; }
}
~~~

While the transport is running:

~~~
beatsPerSecond = BPM / 60
beatIncrementPerSample = BPM / (60 * sampleRate)
~~~

At constant 120 BPM and 48 kHz:

| Note duration | Seconds | Frames |
|---|---:|---:|
| Eighth note | 0.25 | 12,000 |
| Quarter note (one beat) | 0.5 | 24,000 |
| Half note | 1 | 48,000 |
| Whole note / 4/4 bar | 2 | 96,000 |

Free mode uses an absolute frequency in Hz. Synchronized mode uses a musical division:

~~~
frequencyHz = BPM / (60 * beatsPerCycle)
~~~

At 120 BPM, one cycle per beat is 2 Hz; one cycle per 4/4 bar is 0.5 Hz.

The oscillator's synchronized frequency is also projected in the editor: the workspace supplies its committed BPM through the shared canvas to `NodeFrame`; `ParameterReadout` derives the value from the active division in the immutable node snapshot using `MusicalTime`. The shared `ParameterChoiceEditor` renders the optional readout below the choice. It is a calculated rate (including while stopped), not measured telemetry, a graph parameter or a stored value. The free frequency and its dormant connections remain unchanged. The readout updates with tempo, division, preset application and workspace restoration without a separate subscription or DSP update.

~~~mermaid
flowchart TD
    T[Committed workspace BPM] --> R[ParameterReadout]
    N[Active division in node snapshot] --> R
    R --> M[Shared MusicalTime conversion]
    M --> U[Choice editor: read-only Hz]
~~~

Tempo changes affect musical time, not audio sample rate. Integrate beat position over time so that tempo changes do not imply an arbitrary phase jump. Define whether a synchronized modulator is locked to the shared transport position or free-running at a synced rate. Two LFOs with the same frequency are not necessarily in phase unless they share an initial phase/reference or reset event.

The audio clock can continue while musical transport is stopped, allowing effects tails and free-running signals. The transport's start/stop/reset semantics need to be deliberate.

**Implemented Phase 6.1:** `MusicalTransportService` owns one domain `MusicalTransport` and reads the existing `IAudioClock`. It serializes asynchronous observations and edits, publishes immutable `TransportSnapshot` results, and never creates an audio context. The composition root registers one service for the patch. Tempo defaults to 120 BPM and is constrained to 20–300 BPM. The shared `TransportControls` panel displays tempo, beat position, sample rate and derived note/bar rates. Start follows observed root playback, Stop freezes position, replay resumes, and reset anchors beat zero while preserving tempo and running state. Tempo changes preserve accumulated beats. Suspension or clock identity changes freeze and re-anchor the timeline. Canvas navigation and worklet crossfades do not reset transport; tempo edits do not mutate the signal graph.

`MusicalDivision` and `MusicalTime` define reusable quarter-note-relative cycle lengths and the conversion to Hertz. The application timeline is calculated from actual sampled audio frames, not from UI timer ticks.

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

## 7. Hierarchical signal graphs and canvases

### Composite nodes

This is the architectural feature that replaces separate hardcoded drone and patch editors.

A root patch is a SignalGraph. A drone is a composite node containing another SignalGraph. The subgraph may itself contain other composites, subject to reasonable nesting and recursion validation.

~~~mermaid
flowchart TD
    Root[Root patch graph] --> D1[Drone A graph]
    Root --> D2[Drone B graph]
    Root --> E[Master effect graph]
    D1 --> M[Modulation subgraph]
    D1 --> R[Effects subgraph]
~~~

Externally, each child appears as an ordinary node with named input and output ports. Internally, opening it reveals generators, modulators, processors, mixers, connections and boundary racks.

~~~csharp
public sealed class SignalGraph
{
    public Guid Id { get; init; }

    public IReadOnlyList<ISignalNode> Nodes { get; internal set; }
        = Array.Empty<ISignalNode>();

    public IReadOnlyList<SignalConnection> Connections { get; internal set; }
        = Array.Empty<SignalConnection>();
}

public interface ICompositeNode : ISignalNode
{
    SignalGraph InternalGraph { get; }
}
~~~

Actual mutation methods should protect graph invariants. The above represents an illustrative read model; the application command handler or domain operations own controlled mutation, rather than exposing freely modifiable Lists.

The initial domain implementation now creates an owned child graph via `CompositeNode`, with mandatory `InputRack` and `OutputRack` instances. A composite cannot be inserted into itself, an ancestor, or two graphs simultaneously. Concrete node instances cannot be shared across graphs, so mutable parameters remain independent. Child graphs reject master outputs. Snapshots recursively capture child graphs, revision and rack IDs; `GraphSnapshot.GetGraph` resolves a graph within that immutable patch. The session routes commands to owned descendants, and the editor uses the active canvas graph ID.

### The canvases

A main patch might contain:

~~~mermaid
flowchart LR
    L[LFO] --> A[Drone A]
    L --> B[Drone B]
    A --> M[Master mixer]
    B --> M
    M --> O[Output]
~~~

Opening Drone A reveals its own graph:

~~~mermaid
flowchart TD
    S[Sine] --> M[Mixer]
    T[Triangle] --> M
    N[Noise] --> M
    L[Internal LFO] --> F[Filter cutoff]
    M --> LP[Low-pass]
    F --> LP
    LP --> R[Reverb]
    R --> G[Gain]
    G --> O[Output rack]
~~~

The same canvas UI, interaction rules, connection rendering and node registry should be used for both root and nested graphs.

### Separation from visual layout

| Object | Responsibility |
|---|---|
| SignalGraph | DSP-relevant topology, node configuration and connections. |
| CanvasLayout | Positions, size, collapsed state, visual grouping and appearance. |
| CanvasNavigation | Current graph path, parent navigation, breadcrumb and selection. |
| PatchDocument | Root graph, nested graphs, layout data, metadata and schema version. |

Moving a node or changing zoom/selection must not recompile DSP. Opening or closing a subcanvas must not reset any oscillator or interrupt audio. A graph edit can affect audio; a purely visual edit must not.

The UI shows a breadcrumb such as Patch / Drone / Drone. `CanvasNavigationService` uses stable graph IDs to resolve the currently edited graph, caches each visited canvas projection and exposes an immutable breadcrumb entry per level. It releases removed subtree projections and returns to the closest surviving ancestor. `GraphCanvas` is keyed by graph ID to reset transient gestures on navigation; it fits only on first visit, preserving the stored viewport on return.

The editor catalog includes `DroneTemplate`, a composite factory with independent Pitch and Gain modulation inputs and Audio output, an internal sine and gain stage, and all rack connections. It is an ordinary editable graph, not an opaque DSP processor. Racks cannot be deleted independently; the master output is available only in the root catalog.

~~~mermaid
flowchart TD
    Workspace["GraphWorkspace"] --> Nav["CanvasNavigationService"]
    Nav --> Canvas["Active CanvasStateService"]
    Workspace --> Root["Root PatchSession snapshot"]
    Root --> Playback["GraphPlaybackControls"]
    Canvas --> Editor["Reusable GraphCanvas"]
    Root --> Ranges["Graph modulation ranges"]
    Ranges --> Editor
~~~

UI gestures dispatch commands using the active graph ID. `GraphPlaybackControls` stays attached to the root snapshot across navigation, so the same audio voice and revision remain active. The range and telemetry projections select the active graph using its ID while retaining the root revision for correctness.

### Implemented canvas state foundation

The presentation-state library is `Orbit.Presentation` (a plain .NET class library). It references Application and has no dependency on Blazor, browser APIs or processing. Each canvas/subcanvas can own one `ICanvasStateService` for its graph. Phase 5 adds graph-ID-targeted projections in a shared session with independent layouts and viewports.

| State | Owner | Change notification |
| --- | --- | --- |
| Nodes, ports, base parameters and routing | PatchSession / SignalGraph | Committed graph revision |
| World-coordinate node positions, selection and viewport | CanvasStateService | Local canvas state change |
| Effective audio parameter values | DSP runtime | Throttled meter snapshot |

`CanvasStateService` subscribes to unfiltered patch `GraphChangeSet` notifications and resolves its own graph from the final immutable root snapshot. A configurable `INodePlacementPolicy` supplies initial positions (default: a three-column grid). Existing positions are preserved, removed nodes and their incident connections are cleared from selection, and each active canvas emits one update per patch commit. New placements and selection cleanup increment the layout revision; a parameter-only graph change does not. Removing an ancestor composite invalidates descendant canvases, which emit `GraphRemoved` and reject local edits while retaining their last read-only snapshot for navigation.

~~~mermaid
flowchart TD
    Command["Graph command"] --> Commit["PatchSession commit"]
    Commit --> Graph["Graph snapshot"]
    Graph --> Canvas["CanvasStateService"]
    Layout["Pan / zoom / move / select"] --> Canvas
    Canvas --> Read["CanvasStateSnapshot"]
    Read --> View["GraphCanvas renderer"]
    Commit --> Audio["Future audio graph subscriber"]
~~~

`CanvasViewport` is an immutable transform with a finite pan and zoom between 0.25 and 4. It is independent of browser input and CSS. It provides world-to-screen, screen-to-world, screen-delta-to-world and pointer-anchored zoom operations. Pointer positions are viewport-relative. A drag delta is divided by zoom before changing a node's world position, while panning is measured in screen pixels. Reset returns to zero pan and 100% zoom. Fit-to-content is a renderer concern once measured node bounds are available.

Each view consumes an immutable `CanvasStateSnapshot` with separate graph and layout revision numbers, world positions, selected node/connection IDs and viewport. Layout changes never call `IPatchSession.Execute` or publish graph events. Graph and canvas dispatchers share the generic disposable `NotificationChannel<T>` implementation but remain separate notification instances. Listeners are notified after state commits; their failures do not undo state. Use the presentation dispatcher thread for interactive canvas operations and graph commands observed by the canvas; immutable snapshots may be handed to asynchronous processing. Consumers must dispose canvas subscriptions and the service when closing the corresponding canvas.

The geometry and projection are implemented and independently tested. The renderer consumes this state, including measured node bounds, pointer gestures and Bézier paths. Phase 5 implements nested navigation using the same renderer and a cached state per graph.

### Shared canvas and component standards

Every root canvas and nested canvas uses the same editor and viewport behavior. A canvas is a view over a signal graph, not a separate audio engine.

- Pan freely along both axes using a background drag (middle mouse or space + drag also works over nodes). Panning must not move graph nodes.
- Zoom with the wheel or toolbar controls, anchored at the pointer when zooming on the canvas. Show the current percentage at all times, with zoom in/out, reset to 100%, and fit-to-content controls. Use a configurable bounded zoom range (initially 25–400%).
- Keep the top controls compact: one workspace toolbar composes zoom, node catalog and root-owned playback/transport. The workspace delegates viewport actions to its active canvas reference while retaining the keyed canvas lifecycle. The canvas expands to fill remaining viewport height; counts and gesture hints sit in fixed, non-interactive viewport overlays outside the transformed graph world.
- Store viewport pan and zoom separately for each graph. Preserve them when navigating between root and child canvases. Use a single world-to-screen transform shared by nodes, ports, connections and hit testing: screen = world × zoom + pan.
- Render connections as cubic Bézier curves between the actual port anchors. Use one connection renderer for draft, connected, selected and invalid-preview states; keep wire endpoints attached during node movement, pan and zoom.
- Input ports appear on the left, outputs on the right, with accessible names and adequate hit targets. The port shape encodes signal type, not direction. Use both labels and shapes so color is never the only distinction.

#### Visual vocabulary

All nodes have a filled background and a role-specific accent color from shared semantic theme tokens. The colors below are the initial palette, not values hard-coded into individual components.

| Node role | Accent |
| --- | --- |
| Audio generator | Cyan / blue |
| Modulation source (e.g. sine used as an LFO) | Green |
| Signal processor / effect | Amber |
| Composite / drone | Violet |
| Input rack, output rack, master output | Slate |

A sine used as an LFO remains the same generator implementation: the displayed role does not create a new DSP class. Processors that produce modulation retain their processor role; port types distinguish their signals.

| Signal kind | Port shape |
| --- | --- |
| Audio | Circle |
| Modulation / continuous control | Diamond |
| Trigger (future) | Triangle |
| Gate (future) | Square |
| Clock (future) | Hexagon |

Map these presentation shapes to the existing domain port kinds; introduce future kinds only when their domain semantics exist. Audio channel count is a label, not a separate signal kind.

#### Design token and theme system

The design system is a presentation-owned boundary. Static visual values must have a single JSON source, independent of markup, application commands and audio processing. Each root canvas and nested canvas uses the same semantic tokens and shared component styles.

| Level | Responsibility | Example |
| --- | --- | --- |
| Primitive | Raw shared palette, spacing, font sizes, radii, opacity and motion | `primitive.color.ink-950` |
| Semantic | Visual purpose independent of the widget | `semantic.color.generator` |
| Component | Reusable widget dimensions or semantic aliases | `component.node.min-width`, `component.slider.modulation-color` |

The base file is `src/Orbit.UI/DesignSystem/design-tokens.json`; `Themes/dark.json` and `Themes/light.json` supply the same semantic color keys. A theme cannot redefine component sizes: both themes share the same interaction geometry. Domain role colors, selection/focus, errors and modulation use semantic keys. New UI components should reference those keys instead of duplicating their values.

~~~mermaid
flowchart TD
    J["Shared token JSON"] --> G["Build-time generator"]
    T["Dark and light theme JSON"] --> G
    G --> V["Generated CSS variables"]
    V --> B["Base CSS"]
    V --> C["Component styles"]
    B --> R["Root and nested Blazor views"]
    C --> R
~~~

JSON aliases use `"{primitive.color.generator-blue}"` syntax and compile into CSS variable references. The deterministic generator validates token names, flattened name collisions, missing and circular references, and matching theme keys. It emits `wwwroot/css/tokens.css`, which is tracked and loaded before the base and component styles. CI verifies regenerated output and all published CSS assets; there is no runtime JSON reader or styling framework. Dark is the default, light can be activated by setting `data-theme="light"` on the HTML root. Theme selection UI/persistence is a future concern.

Example use in a shared node stylesheet:

~~~css
.node-frame {
    min-width: var(--component-node-min-width);
    border-radius: var(--component-node-border-radius);
    background: var(--semantic-color-surface);
}
.node-frame--generator {
    border-color: var(--semantic-color-generator);
}
~~~

Keep layout structure in CSS and markup, with numeric styling constants in JSON. Reuse token-based component styles for repeated UI controls (the existing button, slider and validation message are migrated). Only computed presentation geometry such as port coordinates, canvas transforms, live modulation percentages and Bézier path points can be provided dynamically by code. Parameter bounds, units, sample rates, transport settings, zoom interaction limits and graph rules are functional values, not visual tokens.

#### Reusable presentation building blocks

| Component | Single responsibility |
| --- | --- |
| GraphCanvas | Compose viewport, graph nodes, connections and local interaction state for any graph |
| CanvasViewport | Coordinate conversion, pan, zoom and viewport state |
| CanvasToolbar | Zoom controls, percentage readout, fit and reset |
| NodeFrame | Shared node background, role accent, header, input/output layout and content slot |
| PortConnector | Render shape, label, typed endpoint, hit area and connection gesture |
| ConnectionLayer / ConnectionPath | Render and hit-test Bézier connections using shared port geometry |
| ParameterSlider | Name, unit, editable numeric base value, min/max, range slider, inline error, modulation band and effective marker |
| ValidationMessage | Consistent field-level error presentation directly below an invalid value box |

Razor markup composes reusable components; code-behind handles presentation interaction. Domain validation and application commands remain authoritative. Node-specific views supply definitions and content, rather than duplicating viewport, slider, port or connection code. Theme tokens and reusable styles live in the UI project, never in Domain.

#### Parameter editor contract

A parameter's immutable definition supplies its name, unit (Hz, ratio, dB, etc.), minimum, maximum, modulation scale and capability. The parameter instance supplies its editable base value. A range slider and number box edit that same value; invalid, non-finite or out-of-range input stays in the field with an explanatory message beneath it and is not committed to the graph. Slider display scaling is independent of DSP modulation semantics.

Modulation adds two read-only visual values to the editor:

- A colored interval on the rail displays the predicted effective-value range; it is computed from the known modulation bounds and routing amounts, not from an animation timer. Unknown bounds are displayed as unknown.
- A moving marker and numeric live readout display the actual effective value sampled from the processing engine. The base thumb and numeric input stay stable.

For example, a frequency base of 440 Hz with a sine LFO at ±100 Hz displays a 340–540 Hz band, while the live marker follows the DSP frequency. Where modulation exceeds the editable base limits, the rail is clipped and the full effective bounds and current value remain visible in text.

Live telemetry is a passive, display-rate snapshot (around 20–30 Hz initially, batched per graph when scaled up), not an application event for every sample. Audio processing never waits for the UI. Do not persist telemetry or write it back to editable parameter values. A stopped or unavailable processor has no live marker.

**Frequency display scale (implemented).** `ParameterDefinition.DisplayScale` is optional immutable metadata, validated by `Parameter`; absent metadata preserves the original linear rail. A display scale contains strictly increasing positive physical values and normalized positions, from 0 to 1, spanning the parameter's physical bounds. Consecutive anchors define logarithmic segments (two anchors provide a conventional logarithmic rail). `ParameterSliderScale` in Orbit.Presentation provides forward and inverse mappings. `ParameterSlider` uses normalized native range input for non-linear scales, translates edits to physical values before validation/commands, and maps the base thumb, predicted interval endpoints and effective marker through the same forward mapping. Numeric edits remain exact; newly dragged values are rounded to six significant digits for readable precision. The normalized range provides the physical value through accessible value text. Marker percentages are clamped at rail limits, not written back to model or DSP. The oscillator's anchors are (0%, 0.01 Hz), (20%, 40 Hz), (85%, 1,000 Hz), (100%, 20,000 Hz), preserving the full range while assigning 65% of travel to the most-used musical band. `ParameterScale.Linear` still specifies additive Hz modulation and does not change; display metadata is excluded from DSP topology comparison and is not serialized in patches or presets. No node-specific UI is required.

~~~mermaid
flowchart TD
    R[Normalized slider position] -->|Inverse display scale| V[Physical base value]
    V --> D[DSP modulation in physical units]
    V --> M[Shared forward display scale]
    D --> E[Effective value]
    E --> M
    B[Predicted physical bounds] --> M
    M --> T[Thumb, band and marker]
~~~

The shared ParameterSlider is implemented in the editable node frames. The graph editor supports live base-value edits and effective modulation meters for reachable connected parameters. `GraphModulationRangeEstimator` calculates conservative parameter intervals from the committed snapshot using optional signal-bound rules on the registered DSP emitters. The weighted inputs are summed and clamped to parameter limits; unknown bounds propagate rather than inventing a range. `FaustNodeContext` inserts a passive meter after the effective value clamp, and the playback service maps its stable name to a parameter address. `GraphPlaybackControls` polls the service at 25 Hz; the workspace accepts only the matching graph revision and passes display data to the shared node frames. The base thumb is independent of the marker. Disconnected nodes can show predicted bounds but have no live sample.

Phase 4 implements the shared canvas, node frames, typed ports and connection renderer; Phase 5 reuses them for nested canvases.

### First visible editor implementation

`GraphWorkspace` constructs the root patch session from `ModulationDemoPatch.OpenSession`, owns one presentation state service, and translates completed user actions into typed commands. `GraphCanvas` consumes that state and is reusable for future child graphs. It composes `CanvasToolbar`, `NodeFrame`, `PortConnector`, `ConnectionLayer`, `ConnectionPath` and the existing `ParameterSlider` inside node frames. There is no per-node renderer duplication.

The canvas world is one transformed coordinate space shared by HTML node frames and SVG Bézier paths. The browser adapter (`wwwroot/js/canvasDom.js`) only measures actual DOM bounds/port centers, captures a pointer, and identifies the input under a dropped wire. The renderer converts measured offsets back into world coordinates, so zoom and panning do not change the topology or require new measurements. Nodes move using the state service and connections follow their positions. Fit uses measured frame bounds; `BezierConnection.Between` supplies horizontal tangents even for backwards edges.

The editor supports node addition/removal, base parameter edits, drag-to-connect or keyboard/click port activation, selection, background panning, middle-button panning from node headers, pointer zoom and fit, and nested canvas navigation. Space-drag remains a future enhancement. The fixed modulation proof is preserved separately from the dynamic Faust compiler.

### Reusable modules and presets

A saved composite definition consists of its graph, exposed ports and optional canvas layout. This supports:

- Drone presets and independent instances.
- Reusable effect racks and modulation systems.
- Grouping selected nodes into a composite and exposing crossing edges.
- Expanding/ungrouping composite nodes.
- Copying graph instances, including independent DSP state.

Define whether a preset is copied at instantiation or linked to a shared template. Start with independent instances, with stable instance-scoped IDs; do not accidentally share runtime phase, parameter state or filter state. Shared preset editing/versioning is a later feature.

**Implemented Phase 6.6:** The registry accepts composite preset catalog keys separately from the resulting domain type (`composite.graph`). Factory identity and type validation remain intact. Catalog labels are supplied by registration so adding a preset does not require a UI case. `DroneTemplate` shares the starter/layered graph-construction path. The Layered fifth preset creates three ratio-linked oscillators, pink noise, a `MixerProcessor` and final gain with editable Pitch/Gain rack inputs. The mixer exposes a summed mono input; its emitter and range rule use the existing weighted-input resolution. Each instance owns all its nodes, parameters, ports and DSP state independently; no saved-preset persistence is implied.

~~~mermaid
flowchart TD
    C[Preset catalog key] --> F[Composite factory]
    F --> G[Fresh composite graph]
    G --> S[Committed snapshot]
    S --> U[Shared canvas]
    S --> D[Existing Faust compiler]
~~~

### Node mute and processor bypass (Phase 6.16)

Provide fast, reversible auditioning without deleting nodes, rewiring a graph, changing base parameters or overwriting connection amounts. A compact **M** action in the shared node header mutes a node's complete outgoing signal; a **B** action on an eligible processor bypasses its processing and passes its input signal through. These are distinct operations.

| Node category | Mute (M) | Bypass (B) |
|---|---|---|
| Generators and modulation sources | Silence all outputs | Not applicable (no dry input) |
| Eligible audio processors | Silence all outputs | Pass the original audio input unchanged |
| Composite drones and other composites | Silence all exposed outputs | Deferred until explicit dry routing is defined |
| Boundary racks and master output | Not applicable initially | Not applicable initially |

Bypass eligibility is an explicit reusable node capability, not a UI type switch or an assumption that every `IProcessor` has a meaningful dry path. Initially enable B for effects with one unambiguous compatible audio input/output. Only audio input passes through; modulation inputs continue controlling the inactive wet computation. Nodes can retain both states; mute takes precedence over bypass, and unmuting restores the prior bypass state.

~~~mermaid
flowchart TD
    I[Audio input] --> P[Processor DSP]
    I --> B[Smoothed bypass blend]
    P --> B
    B --> M[Smoothed mute gain]
    M --> O[Audio output]
~~~

For a linear lane ending at a mixer, muting its final node silences the entire lane's contribution, including effect tails, while the other lanes remain audible. This also works when the lane ends at a composite output. Muting an earlier node does not necessarily silence downstream delay or reverb tails immediately. Muting a source affects every outgoing branch, including modulation connections, so a fan-out graph is not equivalent to independently muting a single edge.

~~~mermaid
flowchart TD
    A[Oscillator A] --> E[Effects A] --> X[Mixer]
    B[Oscillator B] --> F[Muted last effect] -.-> X
    C[Oscillator C] --> G[Effects C] --> X
    X --> O[Audio output]
~~~

**Domain and application.** Model `IsMuted` and, where supported, `IsBypassed` as explicit node state separate from `ParameterDefinition` and layout; they are not numeric modulation targets. Introduce validated `SetNodeMutedCommand` and `SetNodeBypassedCommand` with graph/node IDs and desired state, plus typed before/after facts. Unsupported targets and no-op edits must not create revisions or history entries. Commit each action once through `PatchSession`; snapshots, history restoration, nested-graph routing, range projection and autosave observe the same committed state. Both operations are undoable and persisted. Update the patch schema compatibly so legacy patches restore unmuted and unbypassed; explicit states round-trip through named saves, recovery and export. Individual parameter presets do not include these audition switches.

**Live audio.** Extend graph lowering and runtime control manifests with stable graph-qualified host controls for mute gain and bypass blend. Do not encode the flags as structural Faust constants or treat their changes as topology changes. Preserve ordinary parameter controls, input routes, oscillator phase, delay/reverb state and shared transport; smoothly ramp mute toward silence and crossfade between dry and processed signals for bypass. Continue processing both paths initially, even while inaudible; CPU-saving suspension and state restoration are separate future features. Composite mute gates every flattened exposed output at the composite boundary (including modulation outputs), not each child node. Apply the gate before downstream fan-out. Effective modulation/range reporting must account for mute and bypass without pretending to know arbitrary wet signal bounds. Initial playback and crossfade replacements initialize from the current committed flags; stale runtime updates must not overwrite newer states.

**Presentation.** Extend the reusable `NodeFrame` header with compact M/B toggle buttons, accessible names, pressed state, tooltips and visible active styling drawn from the JSON design tokens. Prevent button pointer actions from starting node drags; keep selection, navigation and cable geometry intact. The same controls work in root and nested canvases without DSP-specific Razor. Make muted nodes recognizable even without relying on color alone; retain their wires and parameter editors.

**Implemented 6.16.1 — mute.** `SignalNode.SupportsMute` exposes the shared generator/processor/composite capability; `IsMuted` is a separate graph-owned flag. `SetNodeMutedCommand` validates the target and publishes `NodeMutedChanged` only on change; `GraphNodeSnapshot` captures both fields. Schema 1 `SavedNode.isMuted` is optional and missing flags restore as false. History uses the existing validated reconstruction path. `GraphFlattener` marks only composite output routes with their owning composite's mute state and origin. One host mute control is allocated per reachable eligible graph/node identity (shared across all its exposed output routes), independently of the ordinary parameter/meter manifest. The gate is downstream of the emitted expression and upstream of fan-out: the host initializes its target before playback and a sample-rate-aware 10 ms linear slew reaches exact silence/unity without recreating the voice. `GraphTopology` ignores mute state while playback resolves control targets from the latest accepted snapshot; topology replacements initialize from committed flags. Range estimation reports the settled muted output as zero even when the wet range was unknown; effective meters remain actual DSP observations during the ramp.

**Implemented 6.16.2 — bypass.** `AudioEffectProcessor` explicitly declares a compatible single dry audio path, validates its port contract and exposes the stable `BypassInputKey`. Gain, Filter, Parametric EQ, Delay, Reverb and Chorus opt in; Mixer remains a routing processor with no B action. `IsBypassed`, `SetNodeBypassedCommand` and `NodeBypassedChanged` follow the existing independent mute state and command pattern; optional schema 1 `SavedNode.isBypassed` restores false for legacy documents. `FaustAuditionControls` and `FaustAuditionControl` replace the mute-only manifest with one shared kind-qualified host control system for both flags. A bypass blend of 1 sends the weighted dry audio input unchanged; 0 sends the ordinary processor output. The wet computation and all modulation dependencies remain running. The shared 10 ms slew initializes from the committed flag, and the final mute gate takes precedence over the dry/wet blend. No recompilation or processor-specific emitter change is needed for either action. The interval estimator uses the routed dry input bound for settled bypass and zero for mute, including when wet bounds are unknown; temporary fade trajectories are not predicted as settled ranges. `NodeAuditionToggle` renders M/B with distinct active token colors and accessible pressed states inside `NodeFrame`; muted and bypassed parameter controls remain editable. Tests compare dry samples and resumed reverb tails with continuous reference DSP at 44.1/48 kHz, including simultaneous mute/bypass.

**Deferred routing actions.** Do not implement mute buttons on individual connections, connection solo, global node solo or group mute/solo in this phase. An existing connection amount of zero can temporarily silence a single branch, while endpoint mute covers ordinary linear mixer lanes. Revisit dedicated edge mute only if preserving independent amounts or fan-out auditioning becomes a genuine requirement. If introduced later, edge state must be distinct from configured amount and use the same reversible, click-safe runtime-control approach. Solo scope and multi-output bypass routing require explicit contracts rather than implicit graph traversal.

### Individual node presets (Phase 6.17, in progress)

A node preset is a reusable immutable configuration of an existing registered node type, not a new node type, a factory, a DSP implementation, a patch or a live link to another node. Initially support leaf generators and processors with editable parameters; racks, master output without editable parameters, and composite graph definitions are not individual preset targets. Custom composite definitions remain a separate graph-template feature.

Use a versioned data-only `NodePreset` containing a stable preset ID, node type key, name, group, optional usage description and a map of stable parameter definition keys to validated base values. Include discrete structural selections and inactive conditional parameters so selecting a preset restores the complete configuration, including dormant settings. Do not store instance GUIDs, connections, connection amounts, positions, effective modulation samples or DSP state. The current patch stores its actual base values, not a dependency on the preset library. Editing or deleting a preset cannot change nodes that previously used it.

~~~json
{
  "schemaVersion": 1,
  "id": "reverb-dark-ambient",
  "nodeType": "processor.reverb",
  "name": "Dark Ambient",
  "group": "Atmospheric",
  "description": "Long, dark reverb for evolving drones.",
  "parameters": { "decay": 8.0, "damping": 0.8, "mix": 0.65 }
}
~~~

**Catalog and responsibilities.** Put the immutable model, catalog, validator and `NodePresetService` in `Orbit.Application/Presets`. Keep factory registration in `NodeRegistry`; use its stable type keys and parameter definitions to validate preset compatibility. Ship built-in presets as versioned JSON resources per node type, loaded by infrastructure through an application-facing catalog contract. Adding another built-in configuration must not require changes to C#, Faust or Razor. The service merges read-only built-ins with the custom library, groups by usage label and sorts consistently. Reserve the `Custom` group for user-created presets; do not allow imported content to impersonate built-ins. Use stable IDs rather than display names as selection keys; validate names, group labels, schema, duplicate IDs and finite in-range values. Explicitly reject incompatible or incomplete configurations unless a versioned migration supplies missing values.

**Implemented 6.17.1.** `NodePreset` defensively copies its complete stable-key parameter dictionary and validates version and metadata. `NodePresetValidator` derives eligible leaf types and parameter definitions from the composed `NodeRegistry` (generators/processors with editable parameters, excluding composites and catalog aliases). The same `ParameterDefinition.IsValidBaseValue` contract validates continuous values and discrete choices, including inactive settings. `NodePresetJsonReader` rejects unknown fields, duplicate JSON keys and malformed configurations without reflection-based deserialization. `EmbeddedNodePresetCatalog` discovers the per-type JSON arrays in `Orbit.Infrastructure/Presets/BuiltIn/` via `INodePresetCatalog`; the resources ship inside the assembly, avoiding independent downloads or stale catalog caches. `NodePresetService` validates the complete set at initialization, rejects globally duplicate stable IDs and the reserved Custom group, and returns read-only groups ordered by label and preset name with ordinal ID tie-breaking. Every eligible type has an empty Custom group ready for the future library. The composition root shares one registry between the editor and validator and eagerly verifies the catalog at startup. Ten initial examples cover all eight eligible types; application, selector and broader content follow in separate steps. No graph or DSP mutation is introduced here.

~~~mermaid
flowchart TD
    J[Embedded JSON resources] --> C[INodePresetCatalog]
    C --> S[NodePresetService]
    R[NodeRegistry] --> V[NodePresetValidator]
    V --> S
    S --> G[Immutable usage groups]
~~~

| Node | Presets | Built-in usage groups | Example presets |
|---|---:|---|---|
| Oscillator | 14 | Bass, Drones, Harmonic layers, Modulation, Rhythmic modulation | Sub Bass, Slow LFO, Deep Sine, Bright Saw |
| Noise | 7 | Atmosphere, Modulation, Texture | Wind, Deep Rumble, Bright Hiss |
| Gain | 6 | Gain staging, Layering, Modulation | Half Level, Quiet Bed, Modulation Center |
| Filter | 8 | Corrective, Resonance, Texture, Tone shaping | Warm Low-pass, Gentle High-pass, Vocal Resonance |
| Parametric EQ | 7 | Corrective, Texture, Tone shaping | Warmth, Presence, Remove Muddiness |
| Delay | 8 | Atmospheric, Rhythmic, Short reflections | Quarter Note, Triplet, Long Echo |
| Reverb | 8 | Atmospheric, Spaces, Texture | Small Room, Large Hall, Dark Ambient |
| Chorus | 7 | Modulated pitch, Texture, Thickening | Gentle Chorus, Deep Ensemble |

Examples describe intended usage, not a guarantee of a particular sound without suitable input/routing. Each supported node also exposes `Custom` even when empty. Presets should demonstrate meaningful settings, sensible output headroom and compatible parameter modes; factory defaults remain available independently.

**Implemented 6.17.4.** The embedded catalog now contains 65 presets across all eight eligible types. The 55 additions preserve the original ten IDs and configurations and introduce no new runtime mechanism or storage format. All entries include descriptions and complete stable-key values (including dormant parameters). CI validates every entry against the registry, applies it through the atomic graph command, checks exact selector matching and unchanged connections, and generates a reachable Faust graph for each configuration. Duplicate complete configurations within a node type would fail the matching test. Custom persistence is the next step.

**Atomic application.** Introduce `ApplyNodePresetCommand` targeting a graph ID and node ID with a validated type-key/value configuration. A domain batch parameter operation resolves and validates every key, value and target before mutating anything, including structural choices and stored inactive values. A failed or unchanged configuration produces no revision. A successful application preserves node, parameter and port IDs, connections and layout; publishes one `GraphChangeSet` with the individual `ParameterChanged` facts; records one undo entry; and triggers existing audio, range projection and autosave subscribers once. Do not loop through separate session commands. Topology comparison already distinguishes base-only changes from structural changes, so ordinary presets update active controls and structural choices use the existing crossfade policy. No preset-specific logic belongs in DSP or audio infrastructure. Existing pending slider history is finalized before a distinct preset application.

**Implemented 6.17.2.** `ApplyNodePresetCommand` carries an immutable `NodePreset` and the target graph/node IDs. `GraphCommandHandler` uses the registry-derived `NodePresetValidator` before calling `SignalGraph.SetNodeParameterValues`. The domain operation independently checks the actual node type, resolves the complete ordinal key set, validates every value through the same parameter validation as individual edits, and prepares all changed `ParameterValueChange` records before mutation. It returns only changed values in parameter definition order. The handler maps these to the existing `ParameterChanged` facts; `PatchSession` commits one root snapshot, one revision and one history entry. Only `SetParameterValueCommand` can participate in slider gesture grouping, so even a single-value preset remains a distinct operation. Rejected and identical configurations have no notification or history entry. Nested parameter and port IDs, dormant wires, audit flags and layouts stay intact; history restoration and patch serialization already carry all base settings. Playback consumes the final committed snapshot through its unchanged topology comparison and control manifest, so one live batch or one structural replacement is sufficient. Tests exercise invalid late values, no-ops, nested and root targets, undo/redo, audition state, and running playback updates.

~~~mermaid
flowchart TD
    C[ApplyNodePresetCommand] --> V[Registry compatibility validation]
    V --> G[SignalGraph: preflight all keys and values]
    G --> P[Apply changed base values]
    P --> E[One committed change set]
    E --> H[History and autosave]
    E --> D[Existing playback update]
    E --> U[Canvas projection]
~~~

~~~mermaid
flowchart TD
    B[Built-in JSON catalog] --> S[Node preset service]
    C[(Custom preset library)] --> S
    S --> U[Grouped preset selector]
    U --> A[Apply preset command]
    A --> P[Patch session and validation]
    P --> H[One undo entry]
    P --> D[Existing audio update]
    P --> V[Canvas and autosave]
~~~

**Presentation.** Add a shared `NodePresetSelector` inside `NodeFrame`, below the header, using design tokens and the same component at every canvas level. Render groups via a compact accessible selector, provide a usage description and allow loading a preset without extra permanent toolbars. Show an unselected/modified state when the current base configuration does not match a preset; modulation alone does not mark it modified. Saving captures the current base configuration from an immutable snapshot. Built-ins cannot be overwritten, renamed or deleted; users can apply them, adjust values and save a new custom preset. Management actions (save as, deliberate overwrite, rename, delete) belong to a compact shared menu/dialog, not per-node implementations. Editing the preset library is separate from graph undo/redo; applying a preset is a graph edit.

**Implemented 6.17.3.** `NodePresetSelector` is composed once in `NodeFrame` immediately below the header. It consumes `NodePresetService` and an immutable `GraphNodeSnapshot`, resolves eligibility and ordered usage groups through the existing catalog, and exposes a native accessible grouped select with a description and an explicit modified/unmatched option. The empty Custom group stays visible but disabled. `NodePresetService.FindMatchingPreset` compares the complete stable-key base configuration, including inactive parameters, and chooses the first match in deterministic catalog order; effective telemetry and audition flags are not involved. The component skips rematching the same snapshot on layout/telemetry-only refresh. Selecting a preset emits `NodePresetApplyRequest` through the shared frame and canvas to the workspace, which addresses the active graph and executes the atomic command. The control is bound to the committed matching value, not a separately persisted selection; undo/redo, reload and direct edits therefore stay consistent. Pointer and keyboard events do not start canvas gestures, and committed edits trigger the existing measured connection geometry refresh. CSS reuses parameter-choice styling and existing JSON-derived semantic/node tokens, with a single-line description and full tooltip. Published browser tests cover grouped options, applied values, modified/undo state and nested playback without interruption. Custom management remains a later phase.

~~~mermaid
flowchart TD
    S[Node snapshot] --> M[Catalog match]
    C[Grouped preset catalog] --> M
    M --> U[NodePresetSelector]
    U --> R[Preset apply request]
    R --> W[GraphWorkspace]
    W --> P[ApplyNodePresetCommand]
    P --> S
~~~

**Custom persistence.** Define `INodePresetStorage` separately from `IPatchLibraryStorage`, with a browser adapter using the existing versioned IndexedDB interop and a dedicated logical store/namespace. Reuse validated serialization, transaction and error patterns where appropriate; never mix node presets with recovery records or named complete patches. Persist versioned data by stable ID, enforce unique names per node type (case-insensitive) and explicit overwrite semantics, and report storage failure without changing the current node. Custom presets survive reload and patch switching; eventually support portable individual JSON import/export with validation and conflict handling. Future audio-asset-valued configurations need explicit references and portability rules from Phase 8; scalar parameter presets do not embed recordings.

**Implemented 6.17.5.** `NodePresetLibraryService` owns serialized library operations. The application-facing `INodePresetStorage` is implemented by `BrowserPatchStorage` through its existing versioned persistence module. A disjoint `node-presets:custom` key contains the whole array of versioned configurations, using the strict reader and reflection-free `NodePresetJsonWriter`. Save as generates a GUID; rename and overwrite preserve identity; only custom IDs of the current node type may be managed. The catalog validates complete parameters, group, IDs and case-insensitive names (including built-ins) before the IndexedDB transaction. A compare-and-replace inside that same read/write transaction prevents lost updates from another tab; a rejected write or invalid loaded document leaves the published catalog unchanged. Initial loading is independent of patch recovery, and the manager can explicitly reload the library. `NodePresetService` merges sorted custom entries after built-in groups and assigns a catalog revision only after a committed change or successful load. Matching keeps deterministic built-in-first precedence when two configurations are identical. No library ID is stored on a graph node.

The shared `NodePresetSelector` opens one `NodePresetManager` panel at workspace level, scoped to the active node's immutable snapshot. It reuses the patch-manager panel styling and offers save as, rename, overwrite and delete without modifying node geometry. A catalog-revision parameter invalidates all visible selector matches even if their graph snapshots are unchanged. Workspace navigation or replacement closes the manager; the singleton library remains available across every canvas and patch. Library writes do not dispatch graph commands, change playback, or enter graph history; applying a preset still follows the existing atomic command.

~~~mermaid
flowchart TD
    N[Current node snapshot] --> M[Shared preset manager]
    M --> L[NodePresetLibraryService]
    L --> V[Validate candidate catalog]
    V --> S[IndexedDB compare-and-replace]
    S --> C[Publish custom catalog revision]
    C --> U[Refresh shared selectors]
    U --> A[Apply preset command]
~~~

**Implemented 6.17.6.** Portable files contain one schema-versioned `NodePreset` object, not the internal array or a patch document. The shared reflection-free JSON writer and strict reader use the same field names and stable parameter keys for both formats. Existing schema-1 library arrays remain unchanged; unknown schema versions are rejected until an explicit migration exists. Export is restricted to a saved custom preset and delegates to the existing browser download adapter through `INodePresetStorage`. The `NodePresetManager` limits file reads to 256 KiB, previews a completely validated custom configuration for the active node type, and offers three explicit actions: preserve identity (reject ID/name conflicts), save a copy with a new GUID and unique name, or replace a matching custom ID of the same type. Built-in IDs/groups cannot be imported or replaced. The candidate is validated again inside the serialized library edit, including name uniqueness and full parameter compatibility. Only a successful compare-and-replace publishes a catalog revision; erroneous files, cancellations, stale writes and failed storage leave existing entries unchanged. Import/export do not mutate the patch or history; applying a configuration is still a separate graph command.

~~~mermaid
flowchart TD
    F[Single preset JSON] --> V[Strict schema and node type validation]
    V --> P[Preview and explicit conflict choice]
    P --> C[Validate candidate catalog]
    C --> S[Existing atomic library transaction]
    S --> R[Refresh custom group]
~~~

### Signal analysis and visualization (planned Phase 6.18)

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

**Processor response.** A frequency response plot is not a waveform, spectrum or amplitude envelope. For parametric EQ, frequency is the bell center, gain is the boost/cut and Q controls bandwidth. For filters, the same plot renders low-pass, high-pass, band-pass or notch responses. Define a pure numerical response contract/registry in `Orbit.Audio` for eligible DSP implementations. Derive frozen-coefficient transfer functions from the actual `FaustStateVariableFilter` algorithm, including its sample-rate frequency clamp, prewarping and gain-dependent damping; share coefficient definitions where feasible and test analytical predictions against offline Faust measurements. Do not substitute an unrelated browser biquad approximation. Use a logarithmic frequency axis up to Nyquist and a dB magnitude axis. Show the base-parameter response when stopped using a declared reference sample rate if no actual context exists; during playback use the active sample rate and optionally overlay the response at the latest effective parameter values from existing parameter telemetry. Time-varying modulation is not a stationary transfer function: label effective curves as instantaneous estimates, not exact spectral measurements. Mute/bypass state should be visible; distinguish the configured wet response from the currently passing signal. Later overlay measured input/output spectra on the same axis, keeping relative transfer gain and absolute signal magnitude clearly distinguished.

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

The runtime signal tap design is described in [Faust and real-time execution](#signal-observation-and-analysis-planned-phase-618). Both forms consume the same backend; an integrated visualization remains preferable for routine inspection, while dedicated analyzers support comparison and arbitrary routing without cluttering every node.

## 8. Input and output racks

The user's proposed solution is the standard interface between canvases: a special internal Input Rack node with named ports that automatically appear as input sockets on the composite node in the parent canvas. The corresponding internal sockets are outputs, allowing external signals to be routed to any compatible internal node.

Output Rack is its mirror image: internal input sockets correspond to public output sockets on the composite.

### Parent graph and child graph

~~~mermaid
flowchart TD
    L["Parent LFO"] --> P["Drone: Pitch input"]
    P --> IR["Child Input Rack: Pitch output"]
    IR --> A["Oscillator A pitch modulation"]
    IR --> B["Oscillator B pitch modulation"]
    A --> M[Internal mixer]
    B --> M
    M --> OR["Child Output Rack: Audio input"]
    OR --> O["Drone: Audio output"]
    O --> OUT[Parent mixer/output]
~~~

| Name | Parent composite socket | Inside corresponding rack |
|---|---|---|
| Pitch | Input | Output on Input Rack |
| Volume | Input | Output on Input Rack |
| Cutoff | Input | Output on Input Rack |
| Reverb mix | Input | Output on Input Rack |
| Audio | Output | Input on Output Rack |
| LFO out (optional) | Output | Input on Output Rack |

A single exposed input can fan out internally to multiple consumers. Named pairs represent the same boundary contract rather than two independent parameter values. For the initial version, allow one Input Rack and one Output Rack per composite to keep the interface easy to understand. The composite may have no exposed inputs, e.g. a self-contained drone.

### Shared definitions

~~~csharp
public record ExposedPort(
    Guid Id,
    string Name,
    SignalType SignalType,
    int ChannelCount,
    double? DefaultValue,
    FanInPolicy FanIn = FanInPolicy.Single
);

public interface IInputRack : ISignalNode
{
    IReadOnlyList<ExposedPort> Ports { get; }
}

public interface IOutputRack : ISignalNode
{
    IReadOnlyList<ExposedPort> Ports { get; }
}
~~~

A shared boundary definition is projected into two socket views with opposite directions. The stable port identity is preserved within the composite instance; the node/address distinguishes parent socket from internal rack socket. This is not two separately mutable collections.

The implementation uses immutable `ExposedPort` records (ID, name, signal type, channel count and receiver fan-in). Both socket views are created from that same definition. Port IDs are unique across the composite and names are unique per direction. Racks are graph-owned and cannot be added or removed separately. The domain transaction replaces the complete validated boundary views rather than mutating records in place; IDs remain stable across rename/reorder. Unconnected default values other than zero await further support.

Possible metadata includes units, ranges, unconnected default values, reorder position, labels and channel layout. For audio inputs, an unconnected default is normally silence; for a modulation input, zero is a useful default when the signal is additive. Defaults must be meaningful for the signal's declared type.

### Editing racks

- Add named port: parent composite socket appears automatically.
- Rename port: both views update without breaking connections because addressing uses IDs, not names.
- Reorder port: changes presentation without rewiring the graph.
- Change port type/channel count/fan-in: future operation; revalidate all affected internal and parent edges.
- Remove port: remove every incident connection on both sides in one validated command.
- Expose optional outputs such as internal LFOs, gates or audio buses, not only the final sound.
- Permit no implicit references from a parent directly to a child oscillator: all external signals cross published boundaries.

The implemented `CompositeBoundaryEditor` resolves the child graph's attached owner and parent, prepares a candidate list and uses `CompositeNode.ReplaceBoundary` to validate it before updating the two projections. The selected rack and outer composite share stable socket IDs and mirrored order, while separate port addresses keep connections local to each graph. The reusable `RackPortEditor` and `RackPortRow` send add, rename, move and remove commands through the existing workspace; the UI offers mono Audio and Modulation ports with a selectable receiver fan-in policy. The graph layer, not a listener or UI view, implements synchronization.

The racks do not generate additional samples or require a separate clock. During compilation, the boundary wiring is resolved to internal signal dependencies without artificial audio latency. The root graph's master output represents the physical playback destination; an internal Output Rack represents a graph boundary, not another speaker.

## 9. Commands, events and transaction boundaries

### Why an event system?

The user suggested broadcasting node connection, disconnection and rack port changes to listeners. This is a good fit for application/domain events, but structural changes should be owned by the graph, not independent node listeners.

A connection belongs to the graph and involves two endpoints. Removing a boundary port may affect both a child graph and its parent. If nodes mutate each other by reacting to events, event ordering can cause inconsistent state, circular reactions, duplicate recompilations and hard-to-debug behavior.

### Event scope

| Scope | Example events |
|---|---|
| Node | ParameterChanged, NodeRenamed |
| Graph | NodeAdded, NodeRemoved, ConnectionCreated, ConnectionRemoved, ConnectionAmountChanged, PortAdded, PortRemoved, PortRenamed |
| Patch/session | GraphChanged, PatchLoaded, TransportStarted, TransportStopped |
| Layout/UI | NodeMoved, SelectionChanged, CanvasOpened, CanvasClosed |

Use one application event dispatcher per active patch session. Events identify the originating GraphId and NodeId where applicable. A subscriber may filter by graph or subscribe across the patch.

A domain object can record events while being changed, but it should not directly call audio or UI listeners. The application layer publishes notifications only after validating and committing the complete change.

### Commands and events

| Command (request) | Event (successful fact) |
|---|---|
| AddNode | NodeAdded |
| RemoveNode | NodeRemoved |
| ConnectNodes | ConnectionCreated |
| RemoveConnection | ConnectionRemoved |
| SetConnectionAmount | ConnectionAmountChanged |
| SetParameterValue | ParameterChanged |
| AddExposedPort | PortAdded |
| RemoveExposedPort | PortRemoved |
| RenameExposedPort | PortRenamed |
| MoveNode | NodeMoved (layout-only) |

Commands may fail validation. Events communicate committed state, not an instruction to complete that state. Do not use events as a replacement for clear application service methods or as a mechanism for hidden mutation cascades.

### Implemented contracts (Phase 3 foundation)

The application layer provides `IPatchSession` for one root graph and its owned descendants. It owns the mutable graph tree, a command handler and a private event dispatcher. The domain remains independent of observers and the UI. All edits use typed commands; the five initial commands in the preceding table are implemented with the `Command` suffix. Exposed-port and layout commands are planned.

~~~csharp
using IPatchSession session = new PatchSession(BuiltInNodes.CreateRegistry());

using var canvasSubscription = session.Subscribe(change =>
    Console.WriteLine($"Render revision {change.Revision}"), session.GraphId);
using var nodeSubscription = session.Subscribe<NodeAdded>(change =>
    Console.WriteLine($"Added {change.Node.Id}"));

GraphCommandResult result = session.Execute(
    new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType));
~~~

The read contract is an immutable `GraphSnapshot` with graph ID, revision, node/parameter snapshots and immutable connections. The published change set contains the patch ID, command target graph ID, final root snapshot, all ordered facts from the operation, and a derived `TopologyChanged` flag. `ChangedGraph` resolves the edited graph within that root snapshot. It never exposes the mutable graph or mutable domain parameter instances. A snapshot taken at revision 3 does not change at revision 4.

| Class / interface | Responsibility |
| --- | --- |
| `IGraphCommand` | Target graph ID and requested edit |
| `GraphCommandHandler` | Apply one validated domain operation, construct facts |
| `IPatchSession` / `PatchSession` | Own graph, serialize edits, commit each successful revision |
| `GraphSnapshot` | Stable consistent read model for views and compiler |
| `IGraphEvent` | Typed committed fact, includes graph ID |
| `GraphChangeSet` | One revision's snapshot and all ordered facts |
| `GraphChangeDispatcher` | Session-local notification channel (internal) |
| `GraphCommandResult` | Committed change set, or no change, and subscriber errors |

A rejected command throws the existing domain validation exception (or registry/parameter validation error) before mutation. It produces no revision and no event. Setting a parameter to its current base value is a no-op. Node removal is one atomic domain operation: it removes all incident connections, then publishes their original immutable edge records and the removed node snapshot in one committed revision. This preserves data useful to future undo/redo.

Subscribe to a whole `GraphChangeSet` for graph projection and audio graph compilation; subscribe to typed events for a specific fact (for example, `ConnectionRemoved`). Subscription filters can target one graph. Registration order and fact order are deterministic within each changeset; registration changes during notification apply to the next revision. Subscribers are independently disposable, and closing the session clears them. Listener callbacks are synchronous and must return quickly; they should schedule independent asynchronous work rather than perform reentrant graph mutations. A listener exception is recorded in the command result, not treated as a failed edit or rollback. Other listeners still receive the facts.

The transaction boundary is **one validated command on an owned graph** (root or descendant). A boundary command targets the child and atomically changes its immediate parent too. The patch session resolves the target under its lock, applies domain validation, captures the entire root graph and publishes a single shared patch revision. Unfiltered listeners see every commit once; changeset filters match the command target or any affected fact's graph ID. Typed event filters match only the local fact. Foreign and removed child graphs are rejected. General multi-command transactions remain future work.

The first application consumer is `ModulationDemoPatch`: it uses the session to construct the existing fixed graph and maps the final snapshot to a playback request. The canvas and dynamic playback consume immutable snapshots; live playback subscriptions will follow when runtime replacement is implemented.

### Example: connecting two nodes

~~~mermaid
sequenceDiagram
    participant UI as Canvas
    participant App as Command service
    participant Graph as SignalGraph
    participant Bus as Event dispatcher
    participant DSP as Audio graph service
    UI->>App: ConnectNodes command
    App->>Graph: Validate and add connection
    Graph-->>App: Validated mutation
    App->>App: Commit snapshot and revision
    App->>Bus: Publish committed change set
    Bus-->>UI: Refresh graph view
    Bus-->>DSP: Request topology update
~~~

### Example: removing an exposed input port

1. The user requests `RemoveBoundaryPortCommand` with child graph ID, outer direction and port ID.
2. Resolve the actual composite instance and shared boundary identity.
3. Find all parent edges connected to the composite's input and all internal edges connected to the rack's matching output.
4. Validate the candidate boundary before mutation. Removal policy is to disconnect all affected edges.
5. Atomically update parent/child graph and remove the boundary definition.
6. Validate the resulting patch; commit exactly one revision.
7. Publish `ConnectionRemoved` facts and two `BoundaryPortChanged` facts (local node ID and opposite socket directions, before/after definitions and indices) in one committed change set.
8. Update both canvas projections and schedule one DSP graph update.
9. Future undo can restore the captured port definitions and removed edge records as one edit.

~~~mermaid
flowchart TD
    A[Remove port command] --> V[Validate parent and child]
    V --> P[Remove parent edges]
    V --> I[Remove child edges]
    P --> R[Remove shared port]
    I --> R
    R --> C[Commit revision]
    C --> E[Publish change set]
    E --> U[Update views]
    E --> D[Schedule DSP update]
~~~

### Subscribers

| Service | Responsibility |
|---|---|
| CanvasStateService | Updates rendered nodes, ports and edges; filters by current graph. |
| AudioGraphService | Applies base parameter changes or requests a topology compile. |
| UndoRedoService | Records reversible commands and change sets, not audio samples. |
| PatchPersistenceService | Saves compatible graph and layout state; may debounce autosave. |
| GraphDiagnosticsService | Logs or reports relevant application changes and compile status. |

Notice that the graph has no direct dependencies on these subscribers. Undo/redo must use validated commands or equivalent transactions, not reverse DSP samples. Subscriber failure should not silently roll back an already committed graph; report runtime sync errors separately.

### Real-time signal versus event

| Application events | DSP signals |
|---|---|
| User actions, commands and state changes | Continuous real-time audio/modulation |
| C# application layer | Faust runtime / AudioWorklet |
| Occur when editing | Up to one value per frame per signal |
| May trigger a graph compile | Never trigger a compile simply because a waveform varies |

An LFO does not publish ParameterChanged 48,000 times a second. Its effective target values are calculated inside DSP. UI monitoring can receive throttled snapshots for metering/visualization; it is not the source of audio time.

## 10. Faust and real-time execution

### Reusable DSP implementations

Small Faust components should accept the signals they need rather than hardcoding UI sliders and modulation.

~~~faust
import("stdfaust.lib");

// Reusable oscillator: inputs may vary at sample rate.
sineGenerator(frequency) = os.osc(frequency);

gainProcessor(signal, gain) = signal * gain;

// A reusable, simple combined oscillator and gain.
oscillator(frequency, volume) =
    sineGenerator(frequency) * volume;

// Minimal standalone example for a build-time test only.
frequency = hslider("frequency", 440, 20, 20000, 1);
volume = hslider("volume", 0.1, 0, 1, 0.001);
process = oscillator(frequency, volume);
~~~

For a real composed patch, the compiler emits base controls and composes the signal-valued inputs. In particular, an ordinary Faust slider is typically read at control/block rate; it is not the same as a sample-by-sample signal input. Avoid using setParamValue in a high-frequency loop to implement LFO modulation.

Conceptual Faust program with internal modulation:

~~~faust
import("stdfaust.lib");

baseFrequency = hslider("baseFrequency", 440, 20, 20000, 1);
lfoRate = hslider("lfoRate", 0.5, 0.01, 20, 0.01);
depth = hslider("depthHz", 100, 0, 400, 1);

lfo = os.osc(lfoRate);
effectiveFrequency = max(20, min(20000, baseFrequency + lfo * depth));
process = os.osc(effectiveFrequency) * 0.1;
~~~

This proves the important distinction: C# changes the base values and depth; Faust computes the modulation and oscillator phase at audio rate.

### Runtime options

| Aspect | Separate DSP nodes | Compiled composite graph |
|---|---|---|
| Execution | Runtime worklets/nodes connected by Web Audio | A Faust program containing the connected nodes |
| Structural edits | Often dynamic reconnection | Recompile or use preallocated flexible routing |
| Audio-rate routing | Requires compatible signal ports and channel mapping | Internal signal composition |
| Overhead | Potentially many nodes/worklets and routing buffers | Potentially more compiler optimization |
| Complexity | Easier initial wiring in some cases | Requires graph compiler and safe replacement |

Preferred long-term direction: compile a flattened, connected patch (or a carefully partitioned graph) to Faust. This permits internal sample-level modulation and avoids a JavaScript object for each DSP step. For a first prototype, a fixed graph compiled at build time is useful to verify the execution and modulation path. Then implement a dynamic compiler. One worklet per independently compilable drone is also a feasible intermediate choice if exported signal inputs/outputs support cross-drone modulation.

### Graph compilation pipeline

~~~mermaid
flowchart TD
    P[Root SignalGraph] --> V[Validate graphs]
    V --> F[Flatten composite boundaries]
    F --> O[Order dependencies]
    O --> B[Build Faust program]
    B --> C[Compile to WASM]
    C --> E[AudioWorklet execution]
    E --> A[Audio output]
~~~

The implemented `GraphFlattener` resolves Input/Output Rack mappings before dependency ordering. For each mirrored socket pair, it introduces a backend-only mono pass-through expression with one input and one output. The original local edges are mapped to those endpoints without changing their amounts. This preserves sums, fan-out and nested routes; a disconnected input contributes zero. No extra DSP processor or sample delay is introduced. Only the root master is the audio sink.

Child leaf and edge IDs are deterministically qualified by owning graph ID; each synthetic route has a distinct identity derived from the composite and port IDs. The manifest reverses this mapping to the original graph ID and local parameter address. The model's snapshots, rack definitions and canvas state remain unchanged. The graph dependency order includes audio dependencies AND parameter modulation dependencies. A topological sort suffices for acyclic graphs. Any explicit feedback mechanism requires additional state/scheduling and compiler support.

The first implementation lives in the plain .NET `Orbit.Audio` library. `FaustGraphCompiler.Compile(GraphSnapshot)` returns a `FaustGraphProgram` containing the source, graph ID and revision, and a list of `FaustParameterControl` mappings. `GraphDependencyOrder` topologically sorts reachable audio and modulation dependencies. `IFaustNodeEmitter` is the strategy interface for one mono output; `OscillatorFaustEmitter` and `GainFaustEmitter` delegate shared routing, control creation and modulation to `FaustNodeContext`. The master output is the terminal expression. No UI or browser runtime is involved.

The first backend supports linear parameters, applying weighted incoming modulation to the smoothed base value and clamping to its definition. Oscillator frequencies also respect the actual sample rate. Disconnected components do not affect the source; master output without an incoming edge produces silence. Exactly one master output is required. Unsupported reachable node types and cycles are rejected.

Parameter controls use stable IDs and definition defaults in the source; edited base values are supplied separately so base-only changes can reuse compiled factories. `FaustParameterControl.GraphId` identifies its owning canvas. The playback service reads values from the owning graph and routes passive meters back via `GraphParameterTelemetry.GetValues(graphId)`. `IGraphModulationRangeEstimator.Estimate(rootSnapshot, graphId)` resolves the same boundaries so internal parameters account for external sources. Edge amounts currently appear as invariant literal coefficients, so depth edits require compiling another program until explicit depth controls are implemented. The probe CLI and offline WASM tests exercise an external LFO controlling internal pitch and gain and audio returning through the output rack.

The graph compiler produces stable parameter addresses and metadata so the browser audio engine can update base controls. Validate names/escaping when generating Faust source; do not concatenate unchecked user labels into executable code. Cache compiled factories by a structural signature where appropriate.

### Resonant filter implementation (Phase 6.7)

`FilterProcessor` is a mono processor with a structural mode choice and two continuously modulatable parameters, cutoff in Hz and resonance Q. `FilterFaustEmitter` selects a response from one topology-preserving state-variable filter. `FaustStateVariableFilter` calculates trapezoidal integrator updates with audio-rate coefficients derived from cutoff and Q; two delayed feedback states remain internal to DSP, not editable graph cycles. The cutoff is limited to 0.45 times the actual sample rate and Q is constrained to 0.5–8. Mode changes preserve model identities and crossfade replacement DSP; base cutoff/Q changes preserve runtime state. Output bounds are deliberately unknown given resonance and time variation.

~~~mermaid
flowchart TD
    A[Audio input] --> F[Two-state filter]
    C[Cutoff and Q signals] --> F
    F --> S[Selected response]
    S --> O[Audio output]
~~~

### Parametric EQ (Phase 6.12)

`ParametricEqProcessor` is a mono single-band bell equalizer. It registers through the ordinary node and Faust emitter catalogs; generic parameter UI, modulation, persistence, history and nested graph compilation apply without special cases. Frequency (20–20,000 Hz), gain (−18 to +18 dB) and Q (0.2–10) are all continuous additive modulation targets in Hz, dB and ratio respectively. Defaults are 1 kHz, 0 dB and Q 1. The emitter bounds frequency at 0.45 times the actual sample rate.

The filter and EQ share `FaustStateVariableFilter.EmitCore`, which implements a two-state trapezoidal integrator. With `A = 10^(gain/40)` and damping `k = 1/(Q*A)`, the bell response is `x + k*(A*A-1)*band`. This gives unity at zero gain, reciprocal static boost/cut and audio-rate coefficient updates without resetting DSP state. Output range remains unknown for time-varying EQ; use gain stages for headroom. Offline tests measure gain and width at 44.1/48 kHz and stress all modulation inputs.

### Feedback delay (Phase 6.13)

`DelayProcessor` is a mono processor with one audio input/output, free delay time (0.01–2 s), feedback (0–0.9) and wet mix (0–1). The continuous parameters use the shared modulation system. Timing mode is structural; synchronized mode activates the shared musical-division definition and deactivates free-delay time and its incoming wires without deleting them. Musical divisions and their Faust beat conversion are shared with the oscillator. Delay time is `60 * beats / BPM` in synchronized mode, with smoothed transitions; the transport's phase-reset semantics do not reset an echo buffer.

The emitter uses one bounded `de.fdelay` buffer, linear fractional-sample interpolation and internal sample-delayed feedback; the additional `mem` aligns the first echo and subsequent repeat spacing with the requested time. Maximum allocation is mode-specific for up to 192 kHz (2 s free or 12 s synchronized at 20 BPM). The dry/wet mix is linear. Feedback is capped below unity, but returned output bounds remain unknown for arbitrary modulated inputs. Changing delay time can pitch-shift existing echoes; structural replacements crossfade and may lose the previous echo tail. Do not model internal feedback as an editable graph cycle.

Transport-aware DSP requests distinguish beat phase and absolute BPM. `FaustNodeContext.TransportTempo` requests one reserved host control `orbit_tempo`, excluded from editable parameter manifests. Existing beat consumers still use a single audio-rate input. `TransportSignalSource` initializes the BPM control from its current anchor and updates active and retiring DSP nodes on tempo changes, without deriving tempo from a quantized wrapped phase or requiring a phase worklet for tempo-only graphs. Unregister controls on voice disposal. Offline waveform tests verify timing and feedback at 44.1/48 kHz; live host control tests verify current tempo and subsequent edits.

### Algorithmic reverb (Phase 6.14)

`ReverbProcessor` is mono with continuous decay (RT60, 0.1–12 s), damping (0–1) and wet mix (0–1) controls. `ReverbFaustEmitter` reuses the standard parameter/modulation context and delegates to `FaustSchroederReverb`. Eight parallel feedback combs with distinct 25–37 ms delay lengths and four serial Schroeder all-pass diffusers form the late reverberation. Delay lengths are fixed in seconds, converted at the actual sample rate, and bounded by compile-time allocations sized up to 192 kHz. Each comb uses feedback `10^(-3 * delaySeconds / decay)` and a one-pole damping filter whose cutoff moves logarithmically from 20 kHz toward 500 Hz. Coefficients remain bounded below unity even under audio-rate modulation. RT60 is an approximate low-frequency decay target, not an exact broadband measurement. Wet summing has attenuation for headroom, not clipping or adaptive normalization. Output range remains unknown for arbitrary input and time-varying parameters.

Base edits and modulation retain the active delay state; dry/wet mix is linear (zero wet hides the tail but does not stop its computation). Stop and structural graph swaps use existing envelope/crossfade behavior and do not promise preservation of long reverb tails. This first implementation uses fixed geometry and mono routing; room size, stereo and alternative algorithms are later extensions. Offline tests use noise bursts to measure tail energy, decay, damping and dry bypass, with extreme modulation at 44.1/48 kHz.

### Chorus (Phase 6.15)

`ChorusProcessor` is a mono effect with rate (0.05–5 Hz), depth (0–10 ms), base delay (5–40 ms) and wet mix (0–1), all audio-rate modulatable. Milliseconds are a new shared parameter unit; append it to the unit enum and render it through the common unit formatter. Defaults are 0.6 Hz, 3 ms, 15 ms and 0.5. `ChorusFaustEmitter` uses an integrated free-running phase and two sine-modulated fractional delay taps separated by one-third of a cycle. The time excursion is limited to `min(depth, baseDelay - 1 ms)`; taps use actual sample rate and fixed buffers bounded to 50 ms at 192 kHz. Linear interpolation and equal averaging form the wet signal, which is blended linearly with dry input. No feedback, new audio clock, structural constants or special UI are required.

The conservative signal range is the input range expanded to include zero (initially empty delay buffers). All combinations are convex weighted sums, so no extra gain occurs. Base edits retain the phase and delay states; high-speed modulation may deliberately pitch-shift the wet signal. Stereo widening, alternate voice counts and tempo-locked chorus are future extensions. Offline tests verify dry amplitude, fixed fractional delay at zero depth, sideband spacing controlled by rate, and bounded output under extreme modulation at 44.1/48 kHz.

### Signal observation and analysis (Phase 6.18)

**Phase 6.18.1 implementation.** Live playback enables up to eight auxiliary channels; the compiler's default mono-only mode remains available for offline comparisons and engines without capture support. The master input maps to channel zero, before the playback gain/envelope. The immutable manifest is keyed by patch, owning graph, node, port, direction and channel. Flattened endpoint aliases reuse compiled expressions; output candidates precede inputs in deterministic identity order, identical expressions share a slot, and omitted addresses report unavailable. No new dependency roots are introduced. The fixed conservative cap (nine total channels) is a prototype limit, not a device-specific performance guarantee.

The playback service also implements `ISignalAnalysisService`; both interfaces resolve to the same singleton. Each subscription lazily acquires a capture lease when read. Browser `SignalCapture` isolates master channel zero via a channel splitter for multichannel programs and allocates one analyser per requested channel, shared by at most 32 leases per voice. The diagnostic time-domain window is 1024 samples; Phase 6.18.2 extends the shared browser buffer to 8192 samples for coherent short windows, while diagnostic reads still copy only 1024 samples with sample rate and context time (not an exact sample-frame timestamp). Playback generation IDs change only on accepted voice replacement, not base edits or display subscriptions. Reads reject mismatched generations, warm-up is pending, and stop/replacement releases previous leases; a failed replacement leaves the last-good voice and manifest intact.

**Phase 6.18.2 implementation.** The selected-node scope is a reusable overlay, separate from graph history and node-specific editors. `SignalScopePanel` chooses eligible graph-qualified mono ports; `SignalScope` owns a browser renderer and resolves a lightweight binding every 250 ms while visible. The renderer calls the same versioned audio module as playback directly, reads shared typed arrays at up to 30 Hz and never moves waveform samples through Blazor. A generation/source change, missing window, hidden view or disposal clears the bounded history; visibility changes release leases. Short windows use a newest complete zero-crossing or free-running window, with per-pixel min/max decimation. Long history stores up to 1000 ten-millisecond extrema buckets, skips overlapping samples and leaves gaps on missed windows. Timing is approximate audio-context time, not an exact render-frame timestamp. Peak/RMS use the latest 20 ms and 0 dBFS at unit amplitude, independent of selected timebase. The view is stopped/unavailable until real capture is ready. Compact node integration and expanded mode persistence remain Phase 6.18.4.

**Phase 6.18.3 implementation.** The same scope selects waveform or spectrum without touching the accepted runtime, graph history or capture lease. `SpectrumAnalyzer` is a pure browser-side radix-2 FFT plan with precomputed bit-reversal, twiddles and window weights, reusable typed arrays and bounded size 1024–8192. It reads the newest complete segment of the shared capture synchronously; DC mean is removed before applying the Hann/rectangular window. Single-sided peak amplitude uses the coherent window gain; the Nyquist bin is not doubled. Squared amplitudes are exponentially smoothed using elapsed audio-context time (off, 150 ms, 500 ms); repeat timestamps reuse the last result. Settings, mode, source, generation, visibility and capture loss reset the plan/smoothing. The positive-frequency log axis spans the first bin to Nyquist, the peak readout reports the dominant bin center rather than false continuous-frequency precision, and max-per-pixel decimation preserves narrow peaks. The amplitude dBFS scale references a full-scale peak sine, not RMS or power spectral density; window leakage and finite frequency resolution still apply. `signalPlot` centralizes theme-driven Canvas preparation and `spectrumPlot` owns frequency-domain geometry. No additional analyser nodes, audio callback work or sample interop are introduced.

~~~mermaid
flowchart TD
    C[Shared captured samples] --> V{Selected view}
    V --> T[Waveform and trigger]
    V --> F[Window and FFT]
    F --> S[Power smoothing]
    S --> P[Log frequency plot]
    C --> L[Peak and RMS]
~~~

~~~mermaid
flowchart TD
    P[Selected port address] --> B[Accepted binding]
    B --> C[Shared browser capture]
    C --> W[Window and trigger]
    C --> H[Bounded min/max history]
    C --> L[Peak and RMS]
    W --> D[Canvas renderer]
    H --> D
    L --> D
~~~

~~~mermaid
flowchart TD
    F[Faust program] --> S[Channel splitter]
    S -->|Channel zero| G[Voice envelope and gain]
    G --> D[Audio destination]
    S -->|Selected channels| A[Shared analyzers]
    A --> L[Bounded capture leases]
~~~

The current compiler lowers the entire reachable, flattened patch to one Faust program with a mono master output. `FaustOutputMonitor` reads passive scalar bargraphs for effective parameters; those readings are throttled scalar telemetry, not consecutive samples suitable for waveform or FFT analysis. Do not split the graph into one AudioWorklet per node, reconstruct oscillator samples in Blazor, or infer a live waveform from parameter telemetry. Extend the shared compiler/runtime with passive signal taps that reference actual existing expressions, never independent copies of oscillator or effect state.

**Addresses and manifest.** Use an immutable graph-qualified `SignalObservationAddress` (patch/graph/node/port identity plus direction and channel, where appropriate). An input observation reads the actual weighted/routed input expression; an output observation reads the emitted signal including mute/bypass gates, not the raw pre-gate expression. Resolve composite/rack boundary ports through `GraphFlattener` origins and routing mappings so parent and child views refer to the correct real signal. Return a `SignalTapManifest` alongside the existing Faust control manifest: map stable observation addresses to backend channels/slots and associate them with the accepted program/playback generation. Do not expose generated Faust identifiers as durable graph identities. The master output tap must have an explicit documented placement relative to the shared playback envelope/master gain.

**Initial backend strategy.** Prototype a bounded set of host-only observation channels in the compiled Faust output, in addition to channel zero carrying the existing mono master. Faust parallel composition can return multiple signals without introducing additional audible paths. The browser audio adapter must explicitly split/select the master channel before the existing gain/envelope/output path; observation channels must never be implicitly downmixed, connected to speakers or treated as user-facing multichannel ports. Prove the pinned FaustWasm worklet's output/channel configuration, browser limits and offline equivalence before integrating this strategy. Expose a bounded set of reachable observation addresses per compiled topology; support live slot selection with precompiled routing or a bounded set of fixed channels as practical, measuring the cost of evaluating selectors. Pick the strategy/capacity by profiling and expose an unavailable/capacity state for addresses that cannot be observed. Opening/collapsing a plot, changing modes or inspecting an available tap must not trigger a structural rebuild; avoid unbounded per-node auxiliary channels. Only intentional graph edits (such as adding a dedicated analyzer or changing its input) follow existing compilation/replacement policy. Keep optional observation failures isolated from the last good audio runtime.

~~~mermaid
flowchart TD
    F[Flattened Faust program] --> S[Channel splitter and tap manifest]
    S --> M[Master channel only]
    S --> T[Observation channels]
    M --> E[Existing gain and envelope]
    E --> O[Audio destination]
    T --> A[Shared analysis service]
    A --> W[Waveform and level]
    A --> P[Spectrum]
~~~

**Analysis lifecycle.** Define application-facing `ISignalAnalysisService`/subscription contracts that address signals without browser or Faust types and identify the accepted playback, sample rate, sample frame/time where available and availability. Keep latest observation data outside graph revisions; the runtime owns buffers and view lifetimes. `Orbit.Audio` compiles tap manifests and owns pure response math; `Orbit.Infrastructure` manages Web Audio channel isolation, `AnalyserNode` or bounded sample capture, FFT/level acquisition and disposal. Native `AnalyserNode` provides time-domain and frequency-domain buffers without altering its input; allocate reusable typed arrays and share analysis for multiple views of the same tap. Use a bounded decimated/min-max history for slow LFOs and long timebases rather than a huge FFT or unbounded history; preserve peaks when downsampling. Use coherent time windows and clearly labelled dBFS/reference levels. Render live Canvas plots on the browser side, with Blazor supplying sparse configuration and subscriptions, not sending sample-rate arrays or triggering render-tree updates per frame. Response curves are computed on parameter changes or throttled effective-value updates, independent of live audio capture.

~~~mermaid
sequenceDiagram
    participant V as Visualization panel
    participant S as Analysis service
    participant R as Active audio runtime
    V->>S: Subscribe by signal address
    S->>R: Resolve active tap
    loop Visible view refresh
        R-->>S: Latest bounded buffer
        S-->>V: Waveform or spectrum
    end
    V->>S: Hide, navigate or dispose
    S->>R: Release unused subscription
~~~

Keep analysis subscriptions reference-counted and restricted to visible compact/expanded plots; prioritize explicitly expanded/selected targets when capacity is reached. Suspend drawing and unnecessary analysis off-screen, in hidden tabs and on stopped playback; throttle visible refresh to a configurable target around 30 Hz. Bound memory, analyzer count, sample window and frequency-bin count. Avoid allocations, locks, blocking operations, FFT/UI drawing or message flooding in the audio rendering callback. Measure CPU and memory with representative nested drone patches at 44.1/48 kHz and establish device-appropriate limits. A shared analyser/tap must not create extra audible routing, change peak level, phase, timing, or transport, even while a graph is crossfading.

Rebind subscriptions atomically to the latest accepted runtime and manifest on graph replacement, discard stale frames/callbacks, and label a brief transition or missing tap rather than showing old-node data under a new identity. Failed replacement retains last-good audio and its corresponding analysis; Stop, workspace replacement and node removal release buffers and browser handles. Preserve the existing effective-parameter meter path independently. Analyzer-only graph branches, if added, are explicit reachability roots; ordinary visual preferences do not change graph reachability. Use deterministic offline fixtures to prove identical audible master samples with observations enabled/disabled, correct root/nested mapping, known sample and FFT/level results, and no output leakage even when taps contain a distinct louder signal.

### Sample-based synthesis and audio assets (planned Phase 8)

Imported recordings (voice, instruments, field recordings) are reusable immutable assets, not graph parameters or DSP source code. A `SamplePlayer` generator reads an asset and emits an ordinary audio signal, allowing the same routing, modulation, nesting and effects as oscillator generators. Other sample-based generators reuse the asset subsystem.

**Storage and ownership.** Introduce a domain asset identity and data-only metadata (ID, name, format, duration, source sample rate and channel count). Application interfaces such as `IAudioAssetStore` and `AudioAssetLibraryService` manage importing, listing, resolving and validating assets. Browser infrastructure stores original encoded bytes as IndexedDB blobs in a separate asset store; the patch document holds stable asset IDs, not PCM arrays or base64. A decoded PCM cache is runtime-only and bounded by memory usage. Multiple node instances can share one immutable asset while retaining independent parameters, playheads and DSP state. Replacing a recording creates a new identity; do not overwrite data referenced by saved patches or undo history. Deletion must account for saved patches, recovery, active sessions, history and in-flight runtime builds; defer automatic garbage collection until reference tracking is reliable. Browser storage is local, subject to quota and eviction; report failures and retain existing assets and patches.

~~~mermaid
flowchart TD
    F[Imported recording] --> L[Audio asset library]
    L --> I[(IndexedDB binary assets)]
    L --> D[Browser decoder]
    D --> B[Decoded PCM cache]
    P[Patch snapshot with asset IDs] --> R[Audio runtime preparation]
    B --> R
    R --> G[Faust graph and AudioWorklet]
    G --> O[Audio output]
~~~

**Playback contract.** The initial generator supports one-shot, forward/reverse looping and ping-pong playback; amplitude, signed playback rate (pitch and time change together), start/end region and loop crossfade are continuous controls where appropriate. Use the existing modulation and conditional-parameter mechanisms; an explicit mode defines whether a playhead advances normally or follows a modulatable normalized position (scrubbing). Treat mode and asset selection as discrete configuration, not numeric modulation. Begin with mono output and an explicit downmix policy for stereo; preserve original channels so multichannel support can follow. Define region bounds, interpolation, zero-rate behavior, end-of-file silence and restart semantics. Trigger/gate integration follows the clock/trigger node contract when available. Waveform previews use decimated peak data, not audio-thread callbacks.

**DSP integration.** Prototype Faust `soundfile` indexed reads and the installed FaustWasm runtime before choosing the binding strategy. Keep decoded PCM and resource manifests outside generated Faust source; no recompilation solely to embed sample bytes. Resolve all assets and prepare buffers off the render thread before starting or swapping voices. Use actual source/runtime sample rates, interpolated fractional indices, bounded memory, and loop crossfades to prevent clicks. Runtime identity must include the resource bindings as well as DSP topology/source so identical code cannot reuse obsolete assets. Cancel stale preparations by revision, keep the last good graph on failure, and reuse the existing crossfade and disposal lifecycle. Do not promise playhead continuity across arbitrary structural swaps until tested. No sample arrays pass through Blazor interop per audio block.

**Persistence and portability.** Version the patch schema for asset references with backwards-compatible migration of existing asset-free patches. Asset selection is a validated graph command, participates in snapshots, undo/redo and autosave, and preserves IDs across nested graphs. Missing assets remain explicit unresolved references with relink UI; a runtime requiring missing data must not silently play substitute audio. Extend patch export with a versioned self-contained `.orbit` archive (JSON manifest and binary assets); keep existing JSON import/export for asset-free patches and clearly flag reference-only exports. Validate archive size, paths, formats, hashes and references before committing imported data; deduplicate immutable assets and remap conflicting IDs transactionally. No cloud upload is required.

**Future consumers.** `GranularGenerator` reads overlapping enveloped grains from the same asset, with modulatable position, grain length, density, pitch, spread and deterministic random seed where appropriate. `WavetableGenerator` uses separately prepared periodic tables derived from recordings, with interpolation and anti-aliasing; an arbitrary speech segment is not necessarily a suitable single cycle. `EnvelopeFollower` is an audio-input processor emitting a modulation signal with attack/release and gain, usable with recordings or any other audio source; it does not need direct asset ownership.

### Updating without clicks

| Change | Runtime operation |
|---|---|
| Move/resize/open node | UI-only; no audio change |
| Set oscillator frequency | Update/smooth existing DSP control |
| Change LFO speed or depth | Update/smooth existing DSP control |
| Add/remove node | Recompile if topology cannot be updated dynamically |
| Create/remove connection | Update routing or compile a new graph |
| Change effect order | Update routing/compile |
| Rename a rack port | Update model/projection, typically no audio compile if stable addresses are unchanged |

The browser integration captures one committed snapshot per Play request and maps it to Faust source and initial values via `FaustGraphPlaybackService`. The pinned complete compiler bundle loads lazily inside `FaustCompilerWorker.js`, coordinated by `FaustCompilerClient`. It compiles outside both the UI and audio threads, and returns a WebAssembly module and metadata to the existing AudioWorklet node factory. The node uses the source-specific factory hash for its processor identity. The shared audio envelope handles start and stop.

Live base-only changes use the active compiler control manifest instead of recompiling. `GraphTopology` compares node/port/parameter definitions and connections (including weights), but ignores base values. `ActiveGraphPlayback` serializes revisions, stores the last accepted topology and values, and maps changed `ParameterAddress` instances to stable control names. The browser engine writes the existing worklet's parameters; Faust's `si.smoo` smooths the change without resetting oscillator phase. While initial compilation is in progress, the browser coalesces base updates before the initial attack.

Structural changes compile new source and compare it with the current program; identical source reuses the existing voice. A changed program is compiled by the worker and instantiated silently before replacement. `MarkRevisionAsync` supersedes in-flight work, and the browser rejects stale results before connecting them. `AudioEngine` treats the Play task as a logical lifetime that can span multiple voices. `AudioVoice` and `AudioEnvelope` schedule complementary linear gain ramps over 50 ms on the same audio clock, after which the retiring voice is disposed. Compiler failures retain the last accepted graph; Stop releases both voices and invalidates pending replacements. This preserves continuity of playback, not arbitrary DSP state.

Compile outside the audio rendering thread, preferably in an appropriate worker or build pipeline. A new graph should be prepared before swapping it in. Use revision numbers to discard stale compilation results and coalesce rapid structural edits. Keep the last good runtime active if compilation fails; report the error in the UI.

Deployment versions the complete audio and persistence JavaScript module trees, not just their entry points. `BrowserModulePaths` resolves both adapters' imports from the shared `OrbitBrowserAssetVersion` assembly metadata; local builds use default unversioned directories. CI rejects unversioned interop paths in the published editor test. IndexedDB database names and keys do not depend on the asset version.

When swapping graphs, use an appropriate short fade/crossfade and preserve state where possible. A newly compiled oscillator may restart phase and filters/effects may lose state; maintaining continuity across topology changes is a distinct engineering problem. Do not promise seamless arbitrary graph edits until tested. Consider the cost of running old and new graphs during a crossfade.

### Real-time requirements

- A 128-frame block at 48 kHz represents only about 2.67 ms.
- Avoid allocation-heavy work, blocking I/O, graph compilation or ordinary UI event dispatch inside processing callbacks.
- Use actual sample rate/block sizes and reuse buffers.
- Manage summing levels, master gain and headroom to prevent clipping.
- Use suitable band-limited waveform implementations for saw/pulse etc. to control aliasing.
- Avoid abrupt parameter changes, accidental phase resets and unsafe feedback.
- Measure CPU and underruns with representative complex patches, including multiple drones.

## 11. Project organization and dependencies

The current solution includes Domain, Application, Presentation, Infrastructure, UI and Audio components. Orbit.Domain isolates the graph model from application services and browser-specific infrastructure.

~~~
Orbit.Domain/
  Graphs/
    SignalGraph.cs
    SignalConnection.cs
    SignalGraphValidator.cs
  Nodes/
    ISignalNode.cs
    IGenerator.cs
    IProcessor.cs
    ICompositeNode.cs
    CompositeNode.cs
    InputRack.cs
    OutputRack.cs
  Parameters/
    Parameter.cs
    ParameterDefinition.cs
    ParameterAddress.cs
    ParameterScale.cs
    ParameterUnit.cs
  Ports/
    PortDefinition.cs
    ExposedPort.cs
    SignalType.cs
  Timing/
    IAudioClock.cs
    ITransport.cs

Orbit.Application/
  Graphs/
    NodeRegistry.cs
    Commands/                  # typed graph commands and command handler
    Events/                    # graph facts, change set, internal dispatcher
    Sessions/                  # IPatchSession, PatchSession and result
    Snapshots/                 # immutable graph/node/parameter records
  Notifications/
    NotificationChannel.cs
  Services/
    AudioGraphService.cs
    UndoRedoService.cs
    PatchPersistenceService.cs
  Interfaces/
    IAudioEngine.cs
    IGraphCompiler.cs

Orbit.Presentation/
  Canvas/
    CanvasPoint.cs
    CanvasViewport.cs
    CanvasStateService.cs
    ICanvasStateService.cs
    CanvasStateSnapshot.cs
    GridNodePlacementPolicy.cs
    INodePlacementPolicy.cs
    CanvasStateChange.cs

Orbit.Audio/
  Dsp/
    Generators/
      Sine.dsp
      Triangle.dsp
      Noise.dsp
    Processors/
      Gain.dsp
      LowPass.dsp
      Reverb.dsp
    Utilities/
      Mixer.dsp
      ParameterModulation.dsp

Orbit.Infrastructure/
  Audio/
    BrowserAudioEngine.cs
    FaustGraphCompiler.cs
  wwwroot/audio/
    audio.js
    AudioEngine.js
    AudioContextManager.js
    FaustModuleLoader.js
    FaustNodeFactory.js
    AudioOutput.js
    ...

Orbit.UI/
  Components/
    Canvas/
    Nodes/
    Ports/
    Parameters/
    Navigation/
  Layout/
  State/                       # future navigation and Blazor view state
  Program.cs

Orbit.Tests/
  Domain/
  Application/
  Compiler/
  Integration/
~~~

The exact folders are illustrative and should be adjusted to existing solution naming and conventions, not blindly added all at once.

### Dependency direction

~~~mermaid
flowchart TD
    UI[Orbit.UI] --> APP[Orbit.Application]
    APP --> DOM[Orbit.Domain]
    INF[Orbit.Infrastructure] --> APP
    INF --> DOM
    INF --> DSP[Orbit.Audio / Faust assets]
    UI --> INF
~~~

The UI-to-infrastructure dependency is restricted to composition/setup, where dependencies are injected (e.g. Program.cs). Presentation components consume application contracts. Domain must not reference Blazor, infrastructure or Faust-specific runtime objects.

### Application startup and connection of layers

Program.cs in the Blazor UI is the composition root. It registers the application services, graph registry, event dispatcher, compiler and browser audio engine implementations through dependency injection. The canvas renders the graph model through a view state/projection. User input invokes commands on the application services, not direct modifications of DSP state by the UI.

~~~mermaid
flowchart TD
    P[Program.cs / DI] --> U[Blazor canvas]
    U --> C[Application commands]
    C --> G[Domain graph]
    G --> E[Committed events]
    E --> S[Audio graph service]
    S --> B[Browser audio bridge]
    B --> F[Faust AudioWorklet]
    F --> O[Browser output]
~~~

The existing bridge manages browser restrictions such as starting/resuming AudioContext after a user gesture. Faust is the processing engine, not the author of the application model. JavaScript remains the infrastructure boundary, not a second independently managed graph database.

## 12. Worked patch example

A concrete example that can be used as a reference for the first-to-later implementation.

### Root patch

- External sine generator acting as LFO at 0.1 Hz.
- Drone A composite node exposing Pitch, Volume, Cutoff and Reverb mix.
- Drone B composite node exposing similar controls.
- Master mixer/output.

The external LFO can affect Drone A pitch with a depth of +/-20 cents and Drone B volume with a different mapping. Each drone has independent internal generators and state, but both take timing from the same audio engine.

### Drone A internal graph

- Input Rack: Pitch, Volume, Cutoff, Reverb mix.
- Sine A: base 110 Hz.
- Triangle B: base 220 Hz.
- Noise source: low level.
- Internal LFO at 0.2 Hz modulating the filter cutoff.
- Mixer sums oscillator outputs with gain staging.
- Low-pass filter.
- Reverb.
- Gain processor.
- Output Rack: Audio (stereo, with channel adaptation as required).

~~~mermaid
flowchart TD
    IR["Input rack: Pitch"] --> A[Sine 110 Hz]
    IR --> B[Triangle 220 Hz]
    A --> M[Mixer]
    B --> M
    N[Noise] --> M
    M --> F[Low-pass]
    L["Internal LFO"] --> F
    F --> R[Reverb]
    R --> G[Gain]
    G --> OR[Output rack]
~~~

Other exposed inputs connect to the matching cutoff, reverb mix and gain parameter sockets. The pitch input maps to cents for both pitched generators so their intervals remain intact.

### Illustrative persisted graph

The following JSON is a compact illustrative schema only; a production schema uses stable IDs uniformly (examples use short strings for readability). It shows the idea of recording nodes, parameters, connections and boundary definitions rather than serializing live browser objects.

~~~json
{
  "schemaVersion": 1,
  "name": "Two-sine modulation proof",
  "transport": { "beatsPerMinute": 120 },
  "rootGraph": {
    "id": "root",
    "nodes": [
      {
        "id": "lfo",
        "type": "generator.oscillator",
        "parameters": { "frequency": 0.5 }
      },
      {
        "id": "oscillator",
        "type": "generator.oscillator",
        "parameters": { "frequency": 440, "amplitude": 0.1 }
      },
      {
        "id": "output",
        "type": "output.master"
      }
    ],
    "connections": [
      {
        "id": "mod-route",
        "source": { "nodeId": "lfo", "portId": "signal-out" },
        "destination": {
          "nodeId": "oscillator",
          "portId": "frequency-modulation-in"
        },
        "amount": 100,
        "amountUnit": "hertz"
      },
      {
        "id": "audio-route",
        "source": { "nodeId": "oscillator", "portId": "signal-out" },
        "destination": { "nodeId": "output", "portId": "audio-in" }
      }
    ]
  },
  "canvasLayouts": {
    "root": {
      "lfo": { "x": 20, "y": 80 },
      "oscillator": { "x": 250, "y": 80 },
      "output": { "x": 480, "y": 80 }
    }
  }
}
~~~

A composite node would additionally contain its nested graph and the shared exposed port definitions. Persist data, not C# interface implementations/delegates or live DSP runtime instances. Use a type registry and versioned DTOs to deserialize.

## 13. Implementation roadmap

Do not attempt to build the entire modular synth in one change. Establish and test each foundation.

### Phase 1 — Domain primitives, graph ownership and tests

- Define SignalGraph, ISignalNode, generator/processor contracts, Parameter, PortDefinition and SignalConnection.
- Define stable ID rules, signal compatibility and fan-in/fan-out.
- Add registry with a minimal sine generator, gain and master output.
- Implement graph validation and reject dependency cycles.
- Add unit tests for the independent graph model, without requiring a browser or audio device.

Deliverable: create and validate a small graph without a UI or a real audio device.

### Phase 2 — Shared timing and first sample-rate modulation

- Use the actual AudioContext sample rate/frame clock.
- Introduce transport model and a time snapshot interface; add BPM synchronization incrementally.
- Create reusable Faust sine function taking a signal-valued frequency.
- Compile an initial fixed graph: LFO -> target frequency -> audible oscillator -> output.
- Support base frequency, modulation depth, start/stop with smoothing.
- Verify continuous phase and parameter behavior at 44.1/48 kHz.

Deliverable: audible two-sine modulation proof, no per-sample C#/JS message loop.

### Phase 3 — Commands and event dispatch

- Implemented: typed ConnectNodes, RemoveConnection, SetParameterValue, AddNode and RemoveNode commands.
- Implemented: validated single-command commits, immutable snapshots and revisioned change sets.
- Implemented: patch-session dispatcher with disposable typed and whole-change-set subscriptions, isolation and failure reporting.
- Implemented: the fixed modulation patch is constructed through the session commands.
- Implemented: independently tested canvas state projection, layout-only events, node placement, selection, pan and pointer-anchored zoom in Orbit.Presentation.
- Next: the actual canvas renderer and dynamic audio graph subscriber in Phase 4. Multi-graph transactions await composite support.

Deliverable: a graph edit reaches independent subscribers once, with valid model state.

### Phase 4 — Flat canvas and dynamic graph compiler

- Reuse the implemented JSON design token system for canvas, node, port, connection and parameter styles.
- Implemented: reusable canvas renderer with role-colored nodes, typed ports and visible Bézier connections, plus pointer pan/zoom and fit.
- Implemented: drag-to-connect, click-to-connect, delete selected connections/nodes, node placement and shared base parameter editor.
- Implemented: dependency ordering and extensible C# graph-to-Faust source compiler with stable parameter controls (sine, gain, master output), plus offline WASM probe tests.
- Implemented: on-demand graph playback from the editor, with a lazy Faust compiler worker, revision status and safe start/stop. The original fixed UI is retired.
- Implemented: live base-value updates on the running worklet, including startup queuing, revision ordering, smoothed controls and structural-change detection.
- Implemented: automatic structural replacement, stale-revision invalidation, same-source reuse, 50 ms audio-clock crossfade and last-good-graph recovery.
- Implemented: effective parameter telemetry and animated modulation indicators; conservative ranges are estimated from the graph and live values come directly from Faust passive meters.
- Next: composite nodes and nested canvases (Phase 5).
- Compile asynchronously, track graph revision and safely swap runtime.
- Test fan-out and multiple modulation sources.
- Add low-pass effect and mixer.

Deliverable: construct and edit a graph of two generators, a filter, mixer/output and modulation edges on the canvas while audio runs.

### Phase 5 — Composite nodes and rack interfaces

- Implemented: ICompositeNode, graph-owned Input Rack and Output Rack, immutable shared boundary identities, recursion checks and nested snapshots.
- Implemented: child graph command routing, patch-wide committed snapshots, exact graph event filters and independent graph-ID-targeted canvas projections.
- Implemented: recursive DSP boundary flattening, stable graph-qualified control/meter mappings, child modulation range estimation and live child parameter edits.
- Implemented: graph navigation, breadcrumb component, cached independent canvas layouts, starter drone catalog and root playback across canvas changes.
- Implemented: editable Input Rack and Output Rack ports, projected automatically onto composite node sockets.
- Implemented: transactional add/rename/reorder/remove-port commands crossing parent/child boundaries, with one changeset and affected-graph notification filters.
- Implemented: reuse the same canvas UI for all levels.
- Implemented: selected-wire amount editing in root and nested canvases. Keep immutable edge identity and endpoints; commit a graph-local `ConnectionAmountChanged` with before/after values. Signed finite amounts support polarity inversion and silent connected routes. Direct parameter targets use their own unit; boundary/audio edges use multipliers. Existing range estimation, topology comparison and crossfade apply the revised coefficient from the committed snapshot.

Deliverable: open a drone node's internal canvas, connect external LFO to a named input, route it internally and hear the modulation.

### Phase 6 — Transport synchronization and drone presets

- Implemented: shared patch-wide transport service, BPM editor, sample-frame-derived musical position, start/stop/resume/reset semantics, and musical divisions.
- Implemented: audio-rate shared transport phase, tempo-synchronized sine LFO, validated musical division parameter choices, root/nested and crossfade phase sharing.
- Implemented: one reusable oscillator with waveform selector (sine, triangle, square, sawtooth), phase, normalized output and structural change crossfade.
- Implemented: unified free/synchronized oscillator timing modes, conditional rate controls, dormant wires and active DSP lowering.
- Implemented: separate white/pink/brown noise generator with modulatable amplitude and structural color selection.
- Implemented: independent layered drone preset, generic composite preset catalog registration and mono weighted mixer.
- Implemented: unified resonant filter processor (low-pass, high-pass, band-pass, notch), with audio-rate cutoff and Q modulation.
- Implemented (6.8): versioned `PatchDocument` serialization, including root/nested graph identities, rack definitions, primitive parameter values and separate canvas layout. Reconstruction uses registered leaf factories and validated domain graph operations, not persisted DSP definitions.
- Implemented (6.9): browser-local IndexedDB, debounced autosave and startup recovery of all visited layouts, active canvas and BPM; playback starts stopped.
- Implemented (6.10): named independent patch snapshots, new/open/overwrite/delete, JSON import/export, validated workspace replacement while stopped.
- Implemented (6.11): bounded patch-wide graph command history with atomic undo/redo and fresh committed revisions, including nested boundaries and stable edge/node identities.
- Implemented (6.12): single-band parametric EQ with continuously modulatable frequency, gain in dB and Q, sharing the state-variable filter core.
- Implemented (6.13): mono feedback delay with free and synchronized timing, fractional delay, audio-rate modulation and live shared BPM.
- Implemented (6.14): mono algorithmic reverb with audio-rate decay, damping and wet mix, bounded feedback combs and all-pass diffusion.
- Implemented (6.15): two-voice mono chorus with modulatable rate, depth, base delay and wet mix; shared millisecond parameter unit and conservative output bounds.
- Implemented (6.16.1): reversible node mute with shared header control, persisted state, boundary-wide smooth gain and settled modulation range projection.
- Implemented (6.16.2): explicit effect bypass capability, independent persisted state, shared click-safe audition controls, dry range projection and continuously running wet DSP.
- Implemented (6.17.1): immutable versioned individual node preset model, registry-derived validation and grouped embedded JSON catalog.
- Implemented (6.17.2): atomic preset application through validated batch parameter changes, one revision/undo entry, and existing live playback.
- Implemented (6.17.3): shared grouped node preset selector, description and exact base-configuration matching in every canvas.
- Implemented (6.17.4): expanded data-only catalog of 65 built-in configurations across all eight eligible node types.
- Implemented (6.17.5): custom preset library with stable IDs, explicit management, independent IndexedDB persistence and stale-write protection.
- Implemented (6.17.6): portable individual JSON presets with preview, compatibility validation and explicit conflict choices; signal observation next.
- Planned (6.18): shared signal observation and visualization, integrated node plots, processor response curves and passive analyzer sinks.
- Deferred: deployment-version detection and safe workspace-save/reload prompt for long-lived tabs whose versioned lazy modules have been removed by a later Pages deployment.
- Add copies/preset reuse with independent instances.

Workspace persistence uses a data-only schema version 1. `PatchDocumentSerializer` captures immutable session snapshots with stable node, parameter, socket, connection and graph IDs. Parameter and port definitions are reconstructed by the registry using their stable keys; composites reconstruct their boundary definitions, preserving both rack identities and receiving-side fan-in. Invalid definitions, unknown schemas and invalid connections are rejected before a session is exposed. `CanvasNavigationService` captures and restores layout and navigation independently. `PatchPersistenceService` coalesces changes for 350 ms and serializes writes; `IPatchStorage` is implemented by `BrowserPatchStorage` through IndexedDB transactions. No live runtime state or selection is stored. Startup loads before rendering the workspace; a missing document seeds the demo, while a failed restore preserves the saved record and disables autosave for that session. Very fast reloads within the debounce window can lose the latest edit; storage is browser-local and can be cleared by the browser.

Phase 6.10 adds `PatchLibraryService` and `IPatchLibraryStorage`, using distinct named keys alongside the recovery key without changing the schema. Named snapshots are not live links to the editor; Save as rejects duplicate names and overwriting is explicit. JSON import/export uses the same serializer. Workspace replacement validates a candidate session and navigation, requires stopped playback, drains pending recovery writes and commits the replacement immediately; then the workspace recreates its keyed presentation and playback components. Invalid imports retain the existing graph and named snapshots.

Phase 6.11 puts `PatchHistory` inside `PatchSession`, retaining up to 100 successful graph command before/after snapshots. Undo/redo reconstructs the target graph using the persistence reconstruction path (without JSON or storage), validates before replacing, advances the current revision and publishes one `GraphChangeSet` containing `GraphHistoryRestored` facts for affected graphs, including added/removed descendants. This preserves the original patch identity, subscriptions, and current audio lifetime. Failed/no-op edits do not enter history; divergent commands clear redo. History is not persisted and begins empty after startup or patch replacement. Presentation retains bounded last-known layouts for recently removed nodes/graphs; standalone layout gestures and tempo are outside graph history.

Continuous parameter editing groups history separately from graph transactions. The shared parameter slider gives pointer drags and range keyboard gestures unique identities; `PatchSession.Execute` accepts an optional history gesture identity. `PatchHistory` retains the initial and latest immutable snapshots for consecutive parameter changes with the same identity, graph and parameter address, while every accepted value still commits and notifies the normal DSP, canvas and autosave subscribers. Release, cancellation, blur, navigation, undo/redo or an intervening successful edit finalizes the pending gesture. A stale completion cannot finalize a newer gesture. If the final value equals the initial value, no entry is added and redo is preserved. Numeric edits and discrete choices retain their ordinary command behavior. No UI component manages history snapshots directly.

~~~mermaid
sequenceDiagram
    participant U as Shared slider
    participant S as Patch session
    participant H as History
    participant D as DSP and autosave
    loop Drag updates
        U->>S: Value and gesture ID
        S->>H: Keep first and latest snapshots
        S->>D: Committed revision
    end
    U->>S: Gesture ended
    S->>H: Finalize one entry
~~~

~~~mermaid
flowchart TD
    C[Graph command] --> H[Patch session history]
    U[Undo or redo] --> H
    H --> V[Validated graph reconstruction]
    V --> R[New committed revision]
    R --> A[Audio update]
    R --> P[Canvas and autosave]
~~~

~~~mermaid
flowchart TD
    M[Patch manager] --> L[Patch library service]
    L --> S[Document serializer]
    L --> I[(Named snapshots)]
    S --> V[Validated candidate]
    V --> W[Workspace replacement]
    W --> R[(Recovery record)]
~~~

~~~mermaid
flowchart TD
    S[Patch session snapshot] --> D[Patch document serializer]
    L[Canvas navigation layouts] --> D
    T[Tempo] --> D
    D --> P[Patch persistence service]
    P --> B[IPatchStorage]
    B --> I[(IndexedDB)]
~~~

Deliverable: several independently configurable drones from one reusable modular architecture.

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

Deliverable: choose or create a grouped configuration for any eligible node without changing its identity or wiring; apply in one undoable operation during playback and reuse across patches. Custom composite graph templates (including internal wiring and exposed racks) remain separate Phase 7 work, with a consistent catalog experience.

#### Phase 6.18 — Signal analysis and visualization (planned)

- **6.18.1 — Observation infrastructure (implemented):** bounded Faust output taps, explicit master-channel isolation, graph-qualified manifest, generation-aware capture leases and analysis contracts. Offline/worklet regression coverage for master equivalence, nested routing, mute/bypass, leakage and bounded resources. Device-scale profiling and long-history capture remain later analysis work.
- **6.18.2 — Oscilloscope and level meter (implemented):** reusable selected-node scope, browser-side time-domain display, peak/RMS, timebase, zero-crossing trigger and bounded slow-modulation history. Known sine, clipped and low-rate modulation regression coverage.
- **Parameter editing UX refinement (implemented):** independent display-scale metadata, segmented logarithmic oscillator frequency rail and consistent modulation visualization; unchanged physical values and DSP semantics.
- **6.18.3 — Spectrum analyzer (implemented):** reusable FFT display with logarithmic frequency and calibrated peak dBFS axes, selectable window/resolution and time-based power smoothing; known fundamental/harmonic/noise tests and shared capture with the oscilloscope.
- **6.18.4 — Integrated and expanded views:** capability-driven shared `NodeVisualizationPanel`, compact collapsible plots, expand/mode/source controls, visible-only rendering, tokenized design and compatible per-graph layout persistence; no graph history or DSP restarts for display changes.
- **6.18.5 — Processor response curves:** pure tested response providers matching the actual parametric EQ and filter DSP, base and instantaneous effective parameter response, sample-rate-aware frequency axis and stopped previews.
- **6.18.6 — Dedicated analyzer sink:** validated passive mono observation node for any compatible signal, reusable visualization panels, independent placement/resizing, analyzer-only reachability, nested graph persistence and undo/redo; prove audible master isolation.
- **6.18.7 — Advanced analysis:** synchronized input/output spectral comparisons, bounded spectrogram history and profiling of multi-drone workloads; reuse chart infrastructure for future envelopes, automation and sample-based nodes.

Deliverable: inspect an oscillator waveform, harmonics, modulation and levels directly on the node; expand the same visualization without changing playback; see an EQ/filter's configured response when stopped and effective response during playback; attach an independent analyzer anywhere in a patch without changing its audible path.

### Phase 7 — Advanced functionality and performance

- More filters, EQ, delay, chorus, flanger, compression and reverb.
- Envelopes, random, step sequencers, automation, explicit feedback.
- Additional routing adapters and channel handling; extend Phase 6.18 observation infrastructure to multichannel signals and future node types.
- Group/ungroup nodes; versioned reusable graph definitions.
- Improve graph swap continuity, compile cache and performance profiling.
- Validate original Orbit's clicking/underrun concerns with large patches.

Deliverable: a scalable modular environment, adding functionality through node definitions rather than hardcoded editors.

### Phase 8 — Audio assets and sample-based synthesis (planned)

- **8.1 — Audio asset library:** import browser-decodable audio files, validate size/format, store original binary assets and metadata in IndexedDB, list and resolve by stable ID, and verify reload and deduplication. Prototype the FaustWasm soundfile binding and measure decoded memory requirements.
- **8.2 — Sample Player:** integrate asset references into graph commands, snapshots, persistence and history; implement independent playheads, amplitude/rate modulation, playback modes, bounded loop region and click-safe looping. Validate nested graphs, live edits and resource-aware runtime replacement.
- **8.3 — Waveform editor:** reusable waveform preview with region selection, playhead telemetry and relink/missing-asset feedback; reuse design tokens and shared parameter controls.
- **8.4 — Portable patch packages:** versioned `.orbit` archives containing the graph document and required binary assets; safe transactional import, deduplication, missing-asset handling and backwards-compatible JSON patches.
- **8.5 — Granular Generator:** overlapping grains, positional and grain-parameter modulation, bounded polyphony and CPU/memory tests.
- **8.6 — Wavetable Generator:** extract or import periodic tables, prepare anti-aliased mip levels, implement morph/interpolation and pitch/phase modulation.
- **8.7 — Envelope Follower:** general audio-to-modulation processor with adjustable attack/release and output scaling; test routing into existing parameters.

Deliverable: import a voice recording, persist and reuse it in multiple independent nodes or nested drones, share a self-contained patch, and transform the same source into evolving granular and wavetable textures. Existing Phase 6/7 effects work remains on the roadmap.

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
| Node audition (planned) | Mute last node in one mixer lane while preserving other lanes; mute fan-out including composite and modulation outputs; bypass eligible effects with unchanged dry signal; retain DSP state, parameters and wiring, verify smooth transitions without recompilation, mute precedence, undo/redo and compatible persistence. |
| Individual node presets (planned) | Validate every shipped configuration; reject invalid batch without mutation; application preserves identities and wiring, emits one revision and undo entry; base-only updates avoid recompilation; custom groups survive reload and library edits do not alter existing nodes. |
| Signal analysis (planned) | Compare master samples with taps on/off; no observation-channel leakage, phase reset or graph compile on view changes; verify root/nested port mapping, weighted input and post-mute/bypass output, known waveform/FFT/peak/RMS, slow LFO history, stale-frame rejection, hidden-view cleanup and bounded resources. |
| Processor response (planned) | EQ center gain, Q bandwidth, filter modes and Nyquist limits match offline Faust measurements at 44.1/48 kHz; distinguish base/effective estimates, live spectra and stopped previews. |
| Analyzer sink (planned) | Analyzer-only branches are observable but silent at the master; explicit graph edits round-trip with undo and persistence; visualization preferences restore without changing graph topology or audio. |
| Output safety | Controlled master gain, no unexpected discontinuity when changing base values. |
| Persistence | Save/load nested graph and layout with stable IDs and equivalent connectivity. |
| Audio assets (planned) | Import and reload binary recordings, share immutable data across nodes, preserve IDs through undo/redo, handle quota and missing assets without corrupting patches. |
| Sample playback (planned) | Verify pitch/rate at 44.1/48 kHz, independent playheads, interpolation, loop bounds/crossfades, reverse and one-shot termination; reject stale asset loads during live replacement. |
| Portable packages (planned) | Round-trip a patch and its assets in a fresh browser; reject malformed, oversized or conflicting archives without partial workspace replacement. |

Use OfflineAudioContext or an appropriate offline DSP renderer for deterministic audio tests where possible. Measure spectral/frequency behavior, sample continuity and expected modulation envelopes. Browser playback and workload tests are needed to catch real-time underruns and device-specific behavior.

## 15. Decisions and open questions

### Agreed or recommended architectural direction

- Use C#/Blazor for application logic and presentation; Faust/WASM for the real-time processing.
- Model every generator, processor and composite with a common node contract.
- Treat modulation as a connection of signals to parameter inputs.
- Keep base values separate from DSP-effective values.
- Own topology in SignalGraph and separate its state from canvas geometry.
- Build a single universal graph editor with nested canvases.
- Represent a drone as a composite graph, not a dedicated monolithic engine.
- Use named Input Rack and Output Rack ports as explicit graph boundaries.
- Keep boundary identity stable across renames and reordering.
- Run all audio-rate computation on a shared authoritative audio clock.
- Provide transport and phase accumulation as distinct concepts.
- Use validated commands and graph/patch-level events with node identification.
- Publish changes only after complete validated transactions.
- Prefer combined/flattened Faust processing for the long term; verify initial simpler execution first.
- Observe actual graph signals through bounded passive taps and shared analysis subscriptions; keep live telemetry and chart settings separate from DSP graph state. Use response calculations matching the real DSP rather than simulated live audio.

### To decide during implementation

| Question | Initial recommendation |
|---|---|
| Runtime partitioning | Start fixed small Faust graph, then prototype whole-graph compilation; partition only if justified. |
| Parameter mapping | Explicit target scale and units; linear, cents, dB, then custom mappings. |
| Audio/modulation compatibility | Explicit port semantics with safe adapters; allow audio-rate FM. |
| Multiple input connections | Per-port policy; explicit mixer for complex audio routing. |
| Feedback | Reject cycles first; add explicitly delayed feedback after tests. |
| Rack count | One Input Rack and one Output Rack per composite initially. |
| Transport sync | Define phase lock/reset versus rate sync separately. |
| Topology changes | Prepare/compile asynchronously, preserve last good runtime, fade swap; improve state transfer later. |
| Preset templates | Independent copies initially; linked/versioned definitions later. |
| Undo history | Store reversible commands/change sets, including multi-graph operations. |
| Discrete parameters | Distinct configuration contract where continuous modulation is inappropriate. |
| Signal tap capacity | Prototype fixed channels versus precompiled selectable slots with explicit master isolation; profile before choosing a bounded capacity, never recompile for ordinary plot mode/visibility changes. |

## 16. References and glossary

### Technical references

- [Orbit 2 repository](https://github.com/wgabel/orbit2) — existing proof-of-concept project.
- [Faust documentation](https://faustdoc.grame.fr/) — DSP language and architecture.
- [Faust architecture files and sample-accurate control](https://faustdoc.grame.fr/manual/architectures/) — sample-rate processing and host control timing.
- [Faust optimization and control-rate behavior](https://faustdoc.grame.fr/manual/optimizing/) — explains block-level control reads versus sample-rate processing.
- [Faust signal API](https://faustdoc.grame.fr/tutorials/signal-api/) — programmatic creation/compilation of signal expressions.
- [FaustWasm](https://github.com/grame-cncm/faustwasm) — browser-compatible Faust compiler and Web Audio bindings.
- [Faust soundfile support](https://faustdoc.grame.fr/manual/soundfiles/) — indexed access to externally supplied audio data.
- [Web Audio decoding](https://developer.mozilla.org/en-US/docs/Web/API/BaseAudioContext/decodeAudioData) — asynchronous decoding and sample-rate conversion.
- [Web Audio API specification](https://www.w3.org/TR/webaudio/) — AudioContext, AudioWorklet, sample rate, current frame/time and render quanta.
- [Faust parallel composition](https://faustdoc.grame.fr/manual/syntax/) — returning multiple signals from a compiled program.
- [Web Audio AnalyserNode](https://www.w3.org/TR/webaudio-1.0/#AnalyserNode) — time-domain and frequency-domain observation.

### Glossary

- **DSP:** Digital signal processing.
- **LFO:** Low-frequency oscillator; a generator used at low frequency, commonly as modulation.
- **FM:** Frequency modulation.
- **AM:** Amplitude modulation.
- **Hz:** Cycles per second.
- **Cent:** 1/100 of a semitone; 1,200 cents = one octave.
- **BPM:** Beats per minute.
- **Sample rate:** Number of sample frames processed per second.
- **Render quantum:** A block of frames delivered for audio processing.
- **Phase:** Position within a repeating oscillator cycle.
- **Graph topology:** The pattern of nodes and connections independent of canvas positions.
- **Composite:** A node encapsulating a nested graph.
- **Boundary port:** A public input/output of a composite represented internally by a rack socket.
- **Domain event:** A notification describing a successfully committed domain change.
- **Signal tap:** A passive observation of an existing DSP signal, not an audible routing edge or an independent DSP instance.
- **FFT:** Fast Fourier transform, used to estimate frequency content from a finite time-domain buffer.
- **Frequency response:** Gain versus frequency of a system with fixed coefficients; distinct from the spectrum of a particular input/output signal.

---

**Summary:** The C# graph describes the instrument, Faust executes it, and Blazor visualizes and edits it. Every drone is a graph that can be opened, connected, saved and reused. Every appropriate parameter can receive signals. All real-time computations share audio timing; application events coordinate edits without becoming part of the real-time processing loop.
