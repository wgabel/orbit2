using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Events;

/// <summary>One atomic ownership change; the selected nodes retain their identities.</summary>
public sealed record NodesGrouped(Guid GraphId, GraphNodeSnapshot Composite, IReadOnlyList<Guid> NodeIds) : IGraphEvent;
