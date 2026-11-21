namespace Orbit.Application.Graphs.Events;

public sealed record NodeBypassedChanged(Guid GraphId, Guid NodeId, bool Before, bool After) : IGraphEvent;
