using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>Mono chorus with two phase-offset modulated delay voices.</summary>
public sealed class ChorusProcessor : AudioEffectProcessor
{
    public const string NodeType = "processor.chorus";
    private static readonly ParameterDefinition RateDefinition = new(
        "rate", "Rate", 0.6, 0.05, 5, ParameterUnit.Hertz, ParameterScale.Linear, CanModulate: true, Smoothing: ParameterSmoothing.Rate);
    private static readonly ParameterDefinition DepthDefinition = new(
        "depth", "Depth", 3, 0, 10, ParameterUnit.Milliseconds, ParameterScale.Linear, CanModulate: true, Smoothing: ParameterSmoothing.DelayTime);
    private static readonly ParameterDefinition DelayDefinition = new(
        "delay", "Base delay", 15, 5, 40, ParameterUnit.Milliseconds, ParameterScale.Linear, CanModulate: true, Smoothing: ParameterSmoothing.DelayTime);
    private static readonly ParameterDefinition MixDefinition = new(
        "mix", "Wet mix", 0.5, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, CanModulate: true, Smoothing: ParameterSmoothing.EffectMix);

    public ChorusProcessor(Guid id) : this(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) { }

    private ChorusProcessor(Guid id, Guid rate, Guid depth, Guid delay, Guid mix)
        : base(id, NodeType, "Chorus",
            [new Parameter(Guid.NewGuid(), RateDefinition, rate), new Parameter(Guid.NewGuid(), DepthDefinition, depth),
             new Parameter(Guid.NewGuid(), DelayDefinition, delay), new Parameter(Guid.NewGuid(), MixDefinition, mix)],
            [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input, SignalType.Audio),
             new PortDefinition(rate, "rate-modulation-in", "Rate", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(depth, "depth-modulation-in", "Depth", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(delay, "delay-modulation-in", "Base delay", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(mix, "mix-modulation-in", "Wet mix", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
