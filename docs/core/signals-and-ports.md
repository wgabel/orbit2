# Signals, nodes and ports

[Architecture overview](../architecture/overview.md) · [Roadmap](../roadmap/overview.md)

Related: [graph model](graph-model.md) · [parameters and modulation](parameters-and-modulation.md) · [commands and events](commands-and-events.md) · [envelopes](../audio/envelopes.md).

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

A Trigger and Clock carry one-sample binary pulses; a Gate carries a held binary level. All three are mono, single-source and unity-only. Clock can feed a Trigger input (a timing pulse is a valid occurrence), but other conversions require an explicit adapter.

The envelope accepts an ordinary oscillator signal on a dedicated continuous event-detection input, distinct from parameter modulation sockets. It also supports typed Trigger/Clock and Gate-rising input selection. See [Envelope generators and rhythmic triggers](../audio/envelopes.md#envelope-generators-and-rhythmic-triggers-planned-phase-71) for conversion, routing and timing rules.

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

**Planned channel expansion (Phase 7.5):** see [stereo audio, panning and unison](../audio/stereo-panning-and-unison.md#3-channel-aware-graph-and-audible-stereo). Explicit ordered L/R Audio ports and compatible rack pairs, channel-wise weighted fan-in and deliberate promotion/downmix/split/select adapters are proposed; modulation, pitch and rhythmic ports remain scalar. Existing mono port identities and saved routes must be preserved. Channel count metadata alone does not make stereo supported by the current compiler.
