namespace Orbit.Application.Graphs.Snapshots;

/// <summary>Read-only child graph and the identities of its mandatory racks.
/// Boundary sockets are already projected onto the parent and rack node snapshots.</summary>
public sealed record CompositeSnapshot(GraphSnapshot Graph, Guid InputRackId, Guid OutputRackId);
