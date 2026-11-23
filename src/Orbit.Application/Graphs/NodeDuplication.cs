using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>
/// Copies a persisted node subtree into the same graph. All internal identifiers,
/// including composite graph/rack IDs and internal edges, are remapped together.
/// Parent wiring is deliberately unchanged. Restore validates the entire candidate
/// before the session replaces its live graph, keeping the command atomic.
/// </summary>
internal static class NodeDuplication
{
    public static SignalGraph Build(GraphSnapshot root, DuplicateNodeCommand command, NodeRegistry registry)
    {
        var source = root.GetGraph(command.GraphId).GetNode(command.SourceNodeId);
        if (source.TypeKey is InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType)
            throw new GraphValidationException("Boundary racks and the master output cannot be duplicated.");
        if (command.NewNodeId == Guid.Empty || HasNodeId(root, command.NewNodeId))
            throw new GraphValidationException("A duplicate needs a new, unused node ID.");

        var identifiers = new Dictionary<Guid, Guid> { [command.SourceNodeId] = command.NewNodeId };
        Guid Remap(Guid oldId)
        {
            if (!identifiers.TryGetValue(oldId, out var next))
                identifiers.Add(oldId, next = Guid.NewGuid());
            return next;
        }

        SavedNode CopyNode(SavedNode node) => node with
        {
            Id = Remap(node.Id),
            Parameters = node.Parameters.Select(p => p with { Id = Remap(p.Id) }).ToArray(),
            Ports = node.Ports.Select(p => p with { Id = Remap(p.Id) }).ToArray(),
            Composite = node.Composite is { } composite ? composite with
            {
                InputRackId = Remap(composite.InputRackId),
                OutputRackId = Remap(composite.OutputRackId),
                Inputs = composite.Inputs.Select(p => p with { Id = Remap(p.Id) }).ToArray(),
                Outputs = composite.Outputs.Select(p => p with { Id = Remap(p.Id) }).ToArray(),
                Graph = CopyChildGraph(composite.Graph)
            } : null
        };

        SavedGraph CopyChildGraph(SavedGraph child) => new(
            Remap(child.Id),
            child.Nodes.Select(CopyNode).ToArray(),
            child.Connections.Select(edge => edge with
            {
                Id = Remap(edge.Id),
                Source = new PortAddress(Remap(edge.Source.NodeId), Remap(edge.Source.PortId)),
                Destination = new PortAddress(Remap(edge.Destination.NodeId), Remap(edge.Destination.PortId))
            }).ToArray());

        SavedGraph Insert(SavedGraph graph)
        {
            if (graph.Id == command.GraphId)
            {
                var original = graph.Nodes.Single(node => node.Id == command.SourceNodeId);
                return graph with { Nodes = [.. graph.Nodes, CopyNode(original)] };
            }
            return graph with
            {
                Nodes = graph.Nodes.Select(node => node.Composite is { } composite
                    ? node with { Composite = composite with { Graph = Insert(composite.Graph) } }
                    : node).ToArray()
            };
        }

        return PatchDocumentSerializer.RestoreGraph(Insert(PatchDocumentSerializer.CaptureGraph(root)), registry);
    }

    private static bool HasNodeId(GraphSnapshot graph, Guid id) =>
        graph.Nodes.Any(node => node.Id == id ||
            node.Composite is { } composite && HasNodeId(composite.Graph, id));
}
