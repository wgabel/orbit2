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
    IGenerator <|-- SineGenerator
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

Generators and processors are registered by stable node type keys, e.g. "generator.sine" or "processor.lowpass". A NodeRegistry holds the definitions, factory and metadata required to create new instances. DSP bindings are supplied separately by the compiler adapter.

### Graph ownership and fan-out

~~~mermaid
flowchart TD
    G[Sine generator] --> F[Low-pass filter]
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
| Sine generator | Frequency, amplitude, phase |
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

Tempo changes affect musical time, not audio sample rate. Integrate beat position over time so that tempo changes do not imply an arbitrary phase jump. Define whether a synchronized modulator is locked to the shared transport position or free-running at a synced rate. Two LFOs with the same frequency are not necessarily in phase unless they share an initial phase/reference or reset event.

The audio clock can continue while musical transport is stopped, allowing effects tails and free-running signals. The transport's start/stop/reset semantics need to be deliberate.

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

The UI can show a breadcrumb such as Patch / Drone A / Effect rack. Use stable graph IDs to resolve the currently edited graph.

### Shared canvas and component standards

Every root canvas and nested canvas uses the same editor and viewport behavior. A canvas is a view over a signal graph, not a separate audio engine.

- Pan freely along both axes using a background drag (middle mouse or space + drag also works over nodes). Panning must not move graph nodes.
- Zoom with the wheel or toolbar controls, anchored at the pointer when zooming on the canvas. Show the current percentage at all times, with zoom in/out, reset to 100%, and fit-to-content controls. Use a configurable bounded zoom range (initially 25–400%).
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

A parameter's immutable definition supplies its name, unit (Hz, ratio, dB, etc.), minimum, maximum, scale and modulation capability. The parameter instance supplies its editable base value. A range slider and number box edit that same value; invalid, non-finite or out-of-range input stays in the field with an explanatory message beneath it and is not committed to the graph. Use appropriate scales (linear initially, logarithmic/pitch-aware where required).

Modulation adds two read-only visual values to the editor:

- A colored interval on the rail displays the predicted effective-value range; it is computed from the known modulation bounds and routing amounts, not from an animation timer. Unknown bounds are displayed as unknown.
- A moving marker and numeric live readout display the actual effective value sampled from the processing engine. The base thumb and numeric input stay stable.

For example, a frequency base of 440 Hz with a sine LFO at ±100 Hz displays a 340–540 Hz band, while the live marker follows the DSP frequency. Where modulation exceeds the editable base limits, the rail is clipped and the full effective bounds and current value remain visible in text.

Live telemetry is a passive, display-rate snapshot (around 20–30 Hz initially, batched per graph when scaled up), not an application event for every sample. Audio processing never waits for the UI. Do not persist telemetry or write it back to editable parameter values. A stopped or unavailable processor has no live marker.

The first implementation is the shared ParameterSlider in the fixed modulation screen. Phase 4 implements the shared canvas, node frames, typed ports and connection renderer; Phase 5 reuses them for nested canvases.

### Reusable modules and presets

A saved composite definition consists of its graph, exposed ports and optional canvas layout. This supports:

- Drone presets and independent instances.
- Reusable effect racks and modulation systems.
- Grouping selected nodes into a composite and exposing crossing edges.
- Expanding/ungrouping composite nodes.
- Copying graph instances, including independent DSP state.

Define whether a preset is copied at instantiation or linked to a shared template. Start with independent instances, with stable instance-scoped IDs; do not accidentally share runtime phase, parameter state or filter state. Shared preset editing/versioning is a later feature.

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

Possible metadata includes units, ranges, unconnected default values, reorder position, labels and channel layout. For audio inputs, an unconnected default is normally silence; for a modulation input, zero is a useful default when the signal is additive. Defaults must be meaningful for the signal's declared type.

### Editing racks

- Add named port: parent composite socket appears automatically.
- Rename port: both views update without breaking connections because addressing uses IDs, not names.
- Reorder port: changes presentation without rewiring the graph.
- Change port type/channel count: revalidate all affected internal and parent edges.
- Remove port: detect affected connections on both sides; reject or remove them in one validated command according to the chosen UI policy.
- Expose optional outputs such as internal LFOs, gates or audio buses, not only the final sound.
- Permit no implicit references from a parent directly to a child oscillator: all external signals cross published boundaries.

The racks do not generate additional samples or require a separate clock. During compilation, the boundary wiring is resolved to internal signal dependencies without artificial audio latency. The root graph's master output represents the physical playback destination; an internal Output Rack represents a graph boundary, not another speaker.

## 9. Commands, events and transaction boundaries

### Why an event system?

The user suggested broadcasting node connection, disconnection and rack port changes to listeners. This is a good fit for application/domain events, but structural changes should be owned by the graph, not independent node listeners.

A connection belongs to the graph and involves two endpoints. Removing a boundary port may affect both a child graph and its parent. If nodes mutate each other by reacting to events, event ordering can cause inconsistent state, circular reactions, duplicate recompilations and hard-to-debug behavior.

### Event scope

| Scope | Example events |
|---|---|
| Node | ParameterChanged, NodeRenamed |
| Graph | NodeAdded, NodeRemoved, ConnectionCreated, ConnectionRemoved, PortAdded, PortRemoved, PortRenamed |
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
| SetParameterValue | ParameterChanged |
| AddExposedPort | PortAdded |
| RemoveExposedPort | PortRemoved |
| RenameExposedPort | PortRenamed |
| MoveNode | NodeMoved (layout-only) |

Commands may fail validation. Events communicate committed state, not an instruction to complete that state. Do not use events as a replacement for clear application service methods or as a mechanism for hidden mutation cascades.

### Example contracts

~~~csharp
public interface IGraphEvent
{
    Guid GraphId { get; }
}

public record ConnectionCreated(
    Guid GraphId,
    SignalConnection Connection
) : IGraphEvent;

public record ConnectionRemoved(
    Guid GraphId,
    SignalConnection Connection
) : IGraphEvent;

public record PortRemoved(
    Guid GraphId,
    Guid NodeId,
    ExposedPort Port
) : IGraphEvent;

public record ParameterChanged(
    Guid GraphId,
    Guid NodeId,
    Guid ParameterId,
    double PreviousValue,
    double CurrentValue
) : IGraphEvent;

public interface IEventDispatcher
{
    void Publish<TEvent>(TEvent graphEvent)
        where TEvent : IGraphEvent;

    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : IGraphEvent;
}
~~~

This is a minimal typed observer API. Production event delivery should define ordering, subscriber isolation, lifetime management and synchronous/asynchronous behavior. Subscriptions are disposable; do not use a static event bus retaining destroyed UI components.

Retaining removed connection/port data (not just the removed ID) is useful for undo and inspection. A command's reversible changes can also be stored as a separate change set.

For cross-graph editing, publish a single committed change set with a patch revision and all associated domain events. A projection should use the final consistent state, not attempt to reconstruct graph mutations from partially committed events. Sample shape:

~~~csharp
public record GraphChangeSet(
    Guid PatchId,
    long Revision,
    IReadOnlyList<IGraphEvent> Events,
    bool TopologyChanged
);
~~~

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
    Graph-->>App: Committed change set
    App->>Bus: Publish connection event
    Bus-->>UI: Refresh graph view
    Bus-->>DSP: Request topology update
~~~

### Example: removing an exposed input port

1. The user requests RemoveExposedPort.
2. Resolve the actual composite instance and shared boundary identity.
3. Find all parent edges connected to the composite's input and all internal edges connected to the rack's matching output.
4. Validate the requested behavior: remove all affected edges or reject and explain.
5. Atomically update parent/child graph and remove the boundary definition.
6. Validate the resulting patch; commit exactly one revision.
7. Publish ConnectionRemoved and PortRemoved notifications in one committed change set.
8. Update both canvas projections and schedule one DSP graph update.
9. Record the entire edit as one undoable command.

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

GraphCompiler resolves Input/Output Rack mappings and gives each flattened instance a unique DSP identity. The graph dependency order includes audio dependencies AND parameter modulation dependencies. A topological sort suffices for acyclic graphs. Any explicit feedback mechanism requires additional state/scheduling and compiler support.

The graph compiler should produce stable parameter addresses and metadata so the browser audio engine can update base controls. Validate names/escaping when generating Faust source; do not concatenate unchecked user labels into executable code. Cache compiled factories by a structural signature where appropriate.

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

Compile outside the audio rendering thread, preferably in an appropriate worker or build pipeline. A new graph should be prepared before swapping it in. Use revision numbers to discard stale compilation results and coalesce rapid structural edits. Keep the last good runtime active if compilation fails; report the error in the UI.

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

The current solution includes Domain, Application, Infrastructure, UI and Audio components. Orbit.Domain isolates the graph model from application services and browser-specific infrastructure.

~~~
Orbit.Domain/
  Graphs/
    SignalGraph.cs
    SignalConnection.cs
    PortAddress.cs
    GraphChangeSet.cs
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
  Events/
    IGraphEvent.cs
    ConnectionCreated.cs
    ConnectionRemoved.cs
    PortRemoved.cs
    ParameterChanged.cs
  Timing/
    IAudioClock.cs
    ITransport.cs

Orbit.Application/
  Commands/
    AddNode.cs
    ConnectNodes.cs
    RemoveConnection.cs
    RemoveExposedPort.cs
    SetParameterValue.cs
  Graphs/
    SignalGraphService.cs
    SignalGraphValidator.cs
    NodeRegistry.cs
  Events/
    IEventDispatcher.cs
    PatchEventDispatcher.cs
  Services/
    AudioGraphService.cs
    UndoRedoService.cs
    PatchPersistenceService.cs
  Interfaces/
    IAudioEngine.cs
    IGraphCompiler.cs

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
  State/
    CanvasStateService.cs
    CanvasNavigation.cs
    CanvasLayout.cs
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
        "type": "generator.sine",
        "parameters": { "frequency": 0.5 }
      },
      {
        "id": "oscillator",
        "type": "generator.sine",
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

- Add typed ConnectNodes, RemoveConnection, SetParameterValue, AddNode and RemoveNode commands.
- Implement validated transactions and committed change sets.
- Add patch-session dispatcher with disposable subscriptions.
- Separate layout events from graph events.
- Connect AudioGraphService and CanvasStateService to change notifications.

Deliverable: a graph edit reaches independent subscribers once, with valid model state.

### Phase 4 — Flat canvas and dynamic graph compiler

- Reuse the implemented JSON design token system for canvas, node, port, connection and parameter styles.
- Add reusable canvas renderer with nodes, ports and visible audio/modulation connections.
- Introduce drag-to-connect, delete connection, node placement and parameter editor.
- Implement dependency ordering and Faust graph builder/compiler.
- Compile asynchronously, track graph revision and safely swap runtime.
- Test fan-out and multiple modulation sources.
- Add low-pass effect and mixer.

Deliverable: construct and edit a graph of two generators, a filter, mixer/output and modulation edges on the canvas while audio runs.

### Phase 5 — Composite nodes and rack interfaces

- Implement ICompositeNode and graph navigation.
- Add Input Rack and Output Rack, projected automatically onto composite node sockets.
- Add transactional add/rename/reorder/remove-port commands crossing parent/child boundaries.
- Flatten graph boundaries while preserving identity and modulation dependencies.
- Reuse the same canvas UI for all levels.

Deliverable: open a drone node's internal canvas, connect external LFO to a named input, route it internally and hear the modulation.

### Phase 6 — Transport synchronization and drone presets

- Finish BPM, musical divisions, start/reset/phase policy and clock nodes.
- Add multi-generator drone graph preset, additional waveforms and effects.
- Persist root/nested graphs, rack definitions and separate canvas layout.
- Add copies/preset reuse with independent instances.
- Add undo/redo and debounced autosave.

Deliverable: several independently configurable drones from one reusable modular architecture.

### Phase 7 — Advanced functionality and performance

- More filters, EQ, delay, chorus, flanger, compression and reverb.
- Envelopes, random, step sequencers, automation, explicit feedback.
- Additional routing adapters, channel handling and signal visualization.
- Group/ungroup nodes; versioned reusable graph definitions.
- Improve graph swap continuity, compile cache and performance profiling.
- Validate original Orbit's clicking/underrun concerns with large patches.

Deliverable: a scalable modular environment, adding functionality through node definitions rather than hardcoded editors.

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
| Output safety | Controlled master gain, no unexpected discontinuity when changing base values. |
| Persistence | Save/load nested graph and layout with stable IDs and equivalent connectivity. |

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

## 16. References and glossary

### Technical references

- [Orbit 2 repository](https://github.com/wgabel/orbit2) — existing proof-of-concept project.
- [Faust documentation](https://faustdoc.grame.fr/) — DSP language and architecture.
- [Faust architecture files and sample-accurate control](https://faustdoc.grame.fr/manual/architectures/) — sample-rate processing and host control timing.
- [Faust optimization and control-rate behavior](https://faustdoc.grame.fr/manual/optimizing/) — explains block-level control reads versus sample-rate processing.
- [Faust signal API](https://faustdoc.grame.fr/tutorials/signal-api/) — programmatic creation/compilation of signal expressions.
- [FaustWasm](https://github.com/grame-cncm/faustwasm) — browser-compatible Faust compiler and Web Audio bindings.
- [Web Audio API specification](https://www.w3.org/TR/webaudio/) — AudioContext, AudioWorklet, sample rate, current frame/time and render quanta.

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

---

**Summary:** The C# graph describes the instrument, Faust executes it, and Blazor visualizes and edits it. Every drone is a graph that can be opened, connected, saved and reused. Every appropriate parameter can receive signals. All real-time computations share audio timing; application events coordinate edits without becoming part of the real-time processing loop.
