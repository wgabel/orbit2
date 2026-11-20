using Orbit.Domain.Graphs;

namespace Orbit.Application.Graphs.Events;

/// <summary>A committed edge coefficient change; both immutable records retain the same identity.</summary>
public sealed record ConnectionAmountChanged(Guid GraphId, SignalConnection Before, SignalConnection After) : IGraphEvent;
