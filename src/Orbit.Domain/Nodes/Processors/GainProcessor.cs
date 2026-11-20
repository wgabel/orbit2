using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>Describes a mono gain stage with a modulatable linear gain parameter.</summary>
public sealed class GainProcessor : SignalNode, IProcessor
{
    public const string NodeType = "processor.gain";

    private static readonly ParameterDefinition GainDefinition = new(
        "gain", "Gain", 1, 0, 1, ParameterUnit.Normalized,
        ParameterScale.Linear, CanModulate: true);

    public GainProcessor(Guid id) : this(id, Guid.NewGuid())
    {
    }

    private GainProcessor(Guid id, Guid modulationPortId)
        : base(id, NodeType, "Gain",
            [new Parameter(Guid.NewGuid(), GainDefinition, modulationPortId)],
            [
                new PortDefinition(Guid.NewGuid(), "audio-in", "Audio",
                    PortDirection.Input, SignalType.Audio),
                new PortDefinition(modulationPortId, "gain-modulation-in", "Gain",
                    PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)
            ],
            [
                new PortDefinition(Guid.NewGuid(), "audio-out", "Audio",
                    PortDirection.Output, SignalType.Audio)
            ])
    {
    }
}
