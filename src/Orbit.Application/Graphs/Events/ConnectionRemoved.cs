using Orbit.Domain.Graphs;

namespace Orbit.Application.Graphs.Events;

/// <summary>An edge was removed; retain its endpoints and amount for future undo.</summary>
public sealed record ConnectionRemoved(Guid GraphId, SignalConnection Connection) : IGraphEvent;
