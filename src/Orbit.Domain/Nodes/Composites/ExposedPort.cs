using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>One named boundary socket identity, shared by the composite and its rack.
/// Fan-in describes the receiving socket, whether outside or inside the composite.</summary>
public sealed record ExposedPort(Guid Id, string Name, SignalType SignalType,
    int ChannelCount = 1, FanInPolicy FanIn = FanInPolicy.Single)
{
    internal PortDefinition Project(PortDirection direction) =>
        new(Id, $"boundary-{Id:N}", Name, direction, SignalType, ChannelCount,
            direction == PortDirection.Input ? FanIn : FanInPolicy.Single);
}
