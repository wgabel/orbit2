using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>External inputs become internal outputs. Graph construction owns this rack;
/// it is never independently created or removed by a canvas.</summary>
public sealed class InputRack : SignalNode
{
    public const string NodeType = "rack.input";

    internal InputRack(Guid id, IReadOnlyList<ExposedPort> ports)
        : base(id, NodeType, "Input Parameters Rack", [], [],
            ports.Select(port => port.Project(PortDirection.Output)))
    {
        Ports = ports;
    }

    public IReadOnlyList<ExposedPort> Ports { get; private set; }

    internal void ReplaceBoundary(IReadOnlyList<ExposedPort> ports)
    {
        Ports = ports;
        ReplaceSockets([], ports.Select(port => port.Project(PortDirection.Output)));
    }
}
