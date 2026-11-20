using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Graphs;

/// <summary>Facts captured by a successful boundary edit, including the exact
/// edges disconnected on each side. Indices are zero-based from the public port list.</summary>
public sealed record BoundaryPortEditResult(Guid ParentGraphId, Guid CompositeNodeId, Guid RackNodeId,
    PortDirection Direction, ExposedPort? Before, ExposedPort? After, int? OldIndex, int? NewIndex,
    IReadOnlyList<SignalConnection> ParentConnections, IReadOnlyList<SignalConnection> ChildConnections);
