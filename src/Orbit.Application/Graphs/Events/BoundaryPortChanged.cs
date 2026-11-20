using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs.Events;

/// <summary>One local socket view changed in a committed boundary transaction.
/// Before/After and indices describe add, remove, rename or reorder; the same
/// exposed port ID is published for the other graph with the opposite direction.</summary>
public sealed record BoundaryPortChanged(Guid GraphId, Guid NodeId, PortDirection Direction,
    ExposedPort? Before, ExposedPort? After, int? OldIndex, int? NewIndex) : IGraphEvent;
