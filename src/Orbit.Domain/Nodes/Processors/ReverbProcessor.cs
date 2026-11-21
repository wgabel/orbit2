using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>Mono algorithmic reverb with modulatable decay, damping and wet mix.</summary>
public sealed class ReverbProcessor : AudioEffectProcessor
{
    public const string NodeType = "processor.reverb";
    private static readonly ParameterDefinition DecayDefinition = new(
        "decay", "Decay (RT60)", 2.5, 0.1, 12, ParameterUnit.Seconds, ParameterScale.Linear, CanModulate: true);
    private static readonly ParameterDefinition DampingDefinition = new(
        "damping", "Damping", 0.4, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, CanModulate: true);
    private static readonly ParameterDefinition MixDefinition = new(
        "mix", "Wet mix", 0.35, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, CanModulate: true);

    public ReverbProcessor(Guid id) : this(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) { }

    private ReverbProcessor(Guid id, Guid decay, Guid damping, Guid mix)
        : base(id, NodeType, "Reverb",
            [new Parameter(Guid.NewGuid(), DecayDefinition, decay), new Parameter(Guid.NewGuid(), DampingDefinition, damping),
             new Parameter(Guid.NewGuid(), MixDefinition, mix)],
            [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input, SignalType.Audio),
             new PortDefinition(decay, "decay-modulation-in", "Decay", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(damping, "damping-modulation-in", "Damping", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(mix, "mix-modulation-in", "Wet mix", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
