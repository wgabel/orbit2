using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Graphs;

/// <summary>Domain transaction for one exposed port of an attached composite.
/// Never mutates one side of the boundary without the other.</summary>
internal sealed class CompositeBoundaryEditor
{
    private readonly SignalGraph child;
    private readonly SignalGraph parent;
    private readonly CompositeNode composite;
    private readonly PortDirection direction;
    private readonly Guid rackId;
    private readonly List<ExposedPort> ports;

    public CompositeBoundaryEditor(SignalGraph child, PortDirection direction)
    {
        this.child = child;
        this.direction = direction;
        composite = child.Owner ?? throw new GraphValidationException("Only an owned child graph has a boundary.");
        parent = composite.OwningGraph ?? throw new GraphValidationException("The composite must be attached to a parent graph.");
        (rackId, ports) = direction switch
        {
            PortDirection.Input => (composite.InputRack.Id, composite.InputRack.Ports.ToList()),
            PortDirection.Output => (composite.OutputRack.Id, composite.OutputRack.Ports.ToList()),
            _ => throw new GraphValidationException("Invalid boundary direction.")
        };
    }

    public BoundaryPortEditResult Add(ExposedPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        ports.Add(port);
        return Commit(null, port, null, ports.Count - 1);
    }

    public BoundaryPortEditResult? Rename(Guid portId, string name)
    {
        var index = IndexOf(portId);
        var before = ports[index];
        if (before.Name == name) return null;
        var after = before with { Name = name };
        ports[index] = after;
        return Commit(before, after, index, index);
    }

    public BoundaryPortEditResult? Move(Guid portId, int index)
    {
        var previous = IndexOf(portId);
        if (index < 0 || index >= ports.Count)
            throw new GraphValidationException("Boundary port index is out of range.");
        if (previous == index) return null;
        var port = ports[previous];
        ports.RemoveAt(previous);
        ports.Insert(index, port);
        return Commit(port, port, previous, index);
    }

    public BoundaryPortEditResult Remove(Guid portId)
    {
        var index = IndexOf(portId);
        var port = ports[index];
        ports.RemoveAt(index);
        return Commit(port, null, index, null);
    }

    private int IndexOf(Guid portId)
    {
        var index = ports.FindIndex(port => port.Id == portId);
        return index >= 0 ? index : throw new GraphValidationException("Boundary port does not exist in this rack.");
    }

    private BoundaryPortEditResult Commit(ExposedPort? before, ExposedPort? after, int? oldIndex, int? newIndex)
    {
        // Capture exact incident edges before replacing sockets. The full boundary
        // is validated before mutating anything; unchanged IDs keep their edges.
        SignalConnection[] parentEdges = before is not null && after is null
            ? Incident(parent, new PortAddress(composite.Id, before.Id)) : [];
        SignalConnection[] childEdges = before is not null && after is null
            ? Incident(child, new PortAddress(rackId, before.Id)) : [];
        composite.ReplaceBoundary(
            direction == PortDirection.Input ? ports : composite.InputRack.Ports,
            direction == PortDirection.Output ? ports : composite.OutputRack.Ports);
        foreach (var edge in parentEdges) parent.RemoveConnection(edge.Id);
        foreach (var edge in childEdges) child.RemoveConnection(edge.Id);
        return new BoundaryPortEditResult(parent.Id, composite.Id, rackId, direction, before, after, oldIndex, newIndex,
            Array.AsReadOnly(parentEdges), Array.AsReadOnly(childEdges));
    }

    private static SignalConnection[] Incident(SignalGraph graph, PortAddress address) =>
        graph.Connections.Where(edge => edge.Source == address || edge.Destination == address).ToArray();
}
