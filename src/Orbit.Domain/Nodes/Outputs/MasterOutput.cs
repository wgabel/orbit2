using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Outputs;

/// <summary>
/// Terminal mono output for the first graph prototype. It has no outgoing ports
/// and deliberately does not own the browser's physical audio device.
/// </summary>
public sealed class MasterOutput : SignalNode
{
    public const string NodeType = "output.master";

    public MasterOutput(Guid id)
        : base(id, NodeType, "Master output", [],
            [
                new PortDefinition(Guid.NewGuid(), "audio-in", "Audio",
                    PortDirection.Input, SignalType.Audio)
            ],
            [])
    {
    }
}
