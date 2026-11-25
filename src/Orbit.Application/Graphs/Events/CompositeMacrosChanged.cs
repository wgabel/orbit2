namespace Orbit.Application.Graphs.Events;

/// <summary>Committed change to a composite's saved macro configuration.</summary>
public sealed record CompositeMacrosChanged(Guid GraphId, Guid NodeId) : IGraphEvent;
