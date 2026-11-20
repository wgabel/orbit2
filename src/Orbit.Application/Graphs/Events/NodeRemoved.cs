using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Events;

/// <summary>A node was removed. Its previous state is available for inspection and future undo.</summary>
public sealed record NodeRemoved(Guid GraphId, GraphNodeSnapshot Node) : IGraphEvent;
