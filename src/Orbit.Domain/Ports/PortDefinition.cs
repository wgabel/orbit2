namespace Orbit.Domain.Ports;

/// <summary>
/// A stable socket in a node instance. Key identifies the socket's role within
/// its node type; Id remains unchanged throughout the node instance's lifetime.
/// </summary>
public sealed record PortDefinition(
    Guid Id,
    string Key,
    string Name,
    PortDirection Direction,
    SignalType SignalType,
    int ChannelCount = 1,
    FanInPolicy FanIn = FanInPolicy.Single);
