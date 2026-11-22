using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>Pure candidate construction over the persisted graph shape. No live instances are moved.</summary>
public static class CompositeGrouping
{
    public sealed record Crossing(SignalConnection Edge, bool IsInput, string Name, SignalType SignalType, int Channels);

    public static IReadOnlyList<Crossing> Preview(GraphSnapshot graph, IEnumerable<Guid> nodeIds)
    {
        var selected = nodeIds.ToHashSet();
        if (selected.Count == 0 || selected.Count > 512)
            throw new GraphValidationException("Select between 1 and 512 module nodes.");
        foreach (var id in selected)
            if (graph.GetNode(id).TypeKey is InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType)
                throw new GraphValidationException("Master output and boundary racks cannot be grouped.");

        var inputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Crossing>();
        foreach (var edge in graph.Connections)
        {
            var from = selected.Contains(edge.Source.NodeId);
            var to = selected.Contains(edge.Destination.NodeId);
            if (from == to) continue;
            var source = graph.GetNode(edge.Source.NodeId);
            var output = source.Outputs.Single(port => port.Id == edge.Source.PortId);
            var inside = to ? graph.GetNode(edge.Destination.NodeId) : source;
            var port = to ? inside.Inputs.Single(port => port.Id == edge.Destination.PortId) : output;
            var names = to ? inputNames : outputNames;
            var basis = $"{inside.Name} · {port.Name}";
            var name = basis;
            for (var suffix = 2; !names.Add(name); suffix++) name = $"{basis} ({suffix})";
            // The boundary uses the source type; adapters and the original weight remain
            // at the original receiver. Audio→Modulation and Clock→Trigger stay valid.
            result.Add(new Crossing(edge, to, name, output.SignalType, output.ChannelCount));
        }
        return result.AsReadOnly();
    }

    internal static (SignalGraph Root, Guid CompositeId) Build(GraphSnapshot root, GroupNodesCommand command, NodeRegistry registry)
    {
        if (root.Revision != command.ExpectedRevision)
            throw new GraphValidationException("The patch changed while grouping was being reviewed. Review the selection again.");
        var name = command.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new GraphValidationException("Enter a composite name of 1–100 characters.");
        var graph = root.GetGraph(command.GraphId);
        var crossings = Preview(graph, command.NodeIds);
        var selected = command.NodeIds.ToHashSet();
        var compositeId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var inputRack = Guid.NewGuid();
        var outputRack = Guid.NewGuid();
        var inputs = new List<ExposedPort>();
        var outputs = new List<ExposedPort>();
        var parentEdges = new List<SignalConnection>();
        var childEdges = new List<SignalConnection>();
        var crossingById = crossings.ToDictionary(crossing => crossing.Edge.Id);
        // Preserve the existing connection order at each receiver, including sums.
        foreach (var edge in graph.Connections)
        {
            if (!crossingById.TryGetValue(edge.Id, out var crossing))
            {
                (selected.Contains(edge.Source.NodeId) ? childEdges : parentEdges).Add(edge);
                continue;
            }
            var port = new ExposedPort(Guid.NewGuid(), crossing.Name, crossing.SignalType, crossing.Channels);
            if (crossing.IsInput)
            {
                inputs.Add(port);
                parentEdges.Add(new SignalConnection(Guid.NewGuid(), edge.Source, new PortAddress(compositeId, port.Id)));
                childEdges.Add(edge with { Source = new PortAddress(inputRack, port.Id) });
            }
            else
            {
                outputs.Add(port);
                childEdges.Add(new SignalConnection(Guid.NewGuid(), edge.Source, new PortAddress(outputRack, port.Id)));
                parentEdges.Add(edge with { Source = new PortAddress(compositeId, port.Id) });
            }
        }
        SavedGraph Rewrite(SavedGraph saved)
        {
            if (saved.Id != graph.Id)
                return saved with { Nodes = saved.Nodes.Select(node => node.Composite is { } composite
                    ? node with { Composite = composite with { Graph = Rewrite(composite.Graph) } } : node).ToArray() };
            var child = new SavedGraph(childId, saved.Nodes.Where(node => selected.Contains(node.Id)).ToArray(), childEdges.ToArray());
            var wrapper = new SavedNode(compositeId, CompositeNode.NodeType, [], [],
                new SavedComposite(name, inputRack, outputRack, inputs.ToArray(), outputs.ToArray(), child));
            return saved with { Nodes = saved.Nodes.Where(node => !selected.Contains(node.Id)).Append(wrapper).ToArray(), Connections = parentEdges.ToArray() };
        }
        try
        {
            return (PatchDocumentSerializer.RestoreGraph(Rewrite(PatchDocumentSerializer.CaptureGraph(root)), registry), compositeId);
        }
        catch (GraphValidationException exception)
        {
            throw new GraphValidationException($"Cannot group these nodes: {exception.Message} Select intervening nodes if the boundary creates a cycle.");
        }
    }
}
