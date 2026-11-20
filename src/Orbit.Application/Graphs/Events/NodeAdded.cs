using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Events;

/// <summary>A node was added; the immutable node snapshot includes its generated identities.</summary>
public sealed record NodeAdded(Guid GraphId, GraphNodeSnapshot Node) : IGraphEvent;
