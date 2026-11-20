using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>Internal inputs become external outputs. A boundary, not a speaker.</summary>
public sealed class OutputRack : SignalNode
{
    public const string NodeType = "rack.output";

    internal OutputRack(Guid id, IReadOnlyList<ExposedPort> ports)
        : base(id, NodeType, "Output Rack", [],
            ports.Select(port => port.Project(PortDirection.Input)), [])
    {
        Ports = ports;
    }

    public IReadOnlyList<ExposedPort> Ports { get; }
}
