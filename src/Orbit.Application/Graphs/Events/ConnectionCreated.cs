using Orbit.Domain.Graphs;

namespace Orbit.Application.Graphs.Events;

/// <summary>A validated connection was added to the graph.</summary>
public sealed record ConnectionCreated(Guid GraphId, SignalConnection Connection) : IGraphEvent;
