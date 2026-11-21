namespace Orbit.Application.Graphs.Events;

public sealed record NodeMutedChanged(Guid GraphId, Guid NodeId, bool Before, bool After) : IGraphEvent;
