using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>Single-band mono peaking equalizer with audio-rate modulation.</summary>
public sealed class ParametricEqProcessor : AudioEffectProcessor
{
    public const string NodeType = "processor.parametric-eq";

    private static readonly ParameterDefinition FrequencyDefinition = new(
        "frequency", "Frequency", 1000, 20, 20000, ParameterUnit.Hertz, ParameterScale.Linear, CanModulate: true);
    private static readonly ParameterDefinition GainDefinition = new(
        "gain", "Gain", 0, -18, 18, ParameterUnit.Decibels, ParameterScale.Linear, CanModulate: true);
    private static readonly ParameterDefinition QualityDefinition = new(
        "q", "Q", 1, 0.2, 10, ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: true);

    public ParametricEqProcessor(Guid id) : this(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) { }

    private ParametricEqProcessor(Guid id, Guid frequency, Guid gain, Guid quality)
        : base(id, NodeType, "Parametric EQ",
            [new Parameter(Guid.NewGuid(), FrequencyDefinition, frequency),
             new Parameter(Guid.NewGuid(), GainDefinition, gain),
             new Parameter(Guid.NewGuid(), QualityDefinition, quality)],
            [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input, SignalType.Audio),
             new PortDefinition(frequency, "frequency-modulation-in", "Frequency", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(gain, "gain-modulation-in", "Gain", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(quality, "q-modulation-in", "Q", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
