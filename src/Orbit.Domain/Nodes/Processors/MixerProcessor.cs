using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>Explicit mono summing junction; connection amounts control each input level.</summary>
public sealed class MixerProcessor(Guid id) : SignalNode(id, NodeType, "Mixer", [],
    [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input,
        SignalType.Audio, FanIn: FanInPolicy.Sum)],
    [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]), IProcessor
{
    public const string NodeType = "processor.mixer";
}
