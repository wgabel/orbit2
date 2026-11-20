using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Add a named port to a child graph's rack and its parent's composite.
/// Direction is relative to the outer composite socket.</summary>
public sealed record AddBoundaryPortCommand(Guid GraphId, PortDirection Direction, string Name,
    SignalType SignalType, FanInPolicy FanIn = FanInPolicy.Single, int ChannelCount = 1) : IGraphCommand;
