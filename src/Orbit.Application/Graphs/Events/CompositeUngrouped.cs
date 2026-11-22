using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Events;

/// <summary>The wrapper and its racks disappear; its ordinary nodes move to the parent.</summary>
public sealed record CompositeUngrouped(Guid GraphId, GraphNodeSnapshot Composite, IReadOnlyList<Guid> NodeIds,
    IReadOnlyList<Guid> MovedConnectionIds) : IGraphEvent;
