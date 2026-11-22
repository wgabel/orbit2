using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>Conservative single-level expansion of the actual rack topology, not a grouping recipe.</summary>
public static class CompositeUngrouping
{
    public sealed record Plan(GraphNodeSnapshot Wrapper, IReadOnlyList<Guid> NodeIds,
        IReadOnlyList<SignalConnection> Connections, IReadOnlyList<Guid> MovedConnectionIds);

    public static Plan Preview(GraphSnapshot parent, Guid compositeId)
    {
        var wrapper = parent.GetNode(compositeId);
        var composite = wrapper.Composite ?? throw new GraphValidationException("Select one composite to ungroup.");
        if (wrapper.IsMuted)
            throw new GraphValidationException("Unmute the composite before expanding it; its output mute cannot be retained without the wrapper.");
        var child = composite.Graph;
        var moved = child.Nodes.Where(node => node.Id != composite.InputRackId && node.Id != composite.OutputRackId).Select(node => node.Id).ToArray();
        if (moved.Length is 0 or > 512)
            throw new GraphValidationException("Ungrouping requires between 1 and 512 internal nodes.");
        if (parent.Nodes.Any(node => moved.Contains(node.Id)))
            throw new GraphValidationException("An internal node identity conflicts with a node in the parent graph.");

        var incoming = parent.Connections.Where(edge => edge.Destination.NodeId == wrapper.Id).ToLookup(edge => edge.Destination.PortId);
        var outgoing = parent.Connections.Where(edge => edge.Source.NodeId == wrapper.Id).ToLookup(edge => edge.Source.PortId);
        var inputRoutes = child.Connections.Where(edge => edge.Source.NodeId == composite.InputRackId).ToLookup(edge => edge.Source.PortId);
        var outputRoutes = child.Connections.Where(edge => edge.Destination.NodeId == composite.OutputRackId).ToLookup(edge => edge.Destination.PortId);
        void ValidateBoundary(PortDefinition port, IEnumerable<SignalConnection> sources, IEnumerable<SignalConnection> receivers)
        {
            var count = sources.Count();
            if (count > 1)
                throw new GraphValidationException($"Boundary '{port.Name}' mixes several sources. Keep the composite or add an explicit mixer before expanding.");
            if ((count == 0) != !receivers.Any())
                throw new GraphValidationException($"Boundary '{port.Name}' is connected on only one side. Remove the unused cable or connect both sides before expanding.");
        }
        foreach (var port in wrapper.Inputs) ValidateBoundary(port, incoming[port.Id], inputRoutes[port.Id]);
        foreach (var port in wrapper.Outputs) ValidateBoundary(port, outputRoutes[port.Id], outgoing[port.Id]);

        // Each terminal receiver retains its edge ID and the product of its path's
        // coefficients. A boundary may fan out, but cannot sum multiple sources.
        SignalConnection BypassInput(SignalConnection receiver)
        {
            if (receiver.Source.NodeId != composite.InputRackId) return receiver;
            var source = incoming[receiver.Source.PortId].Single();
            return receiver with { Source = source.Source, Amount = Multiply(source.Amount, receiver.Amount) };
        }
        var edges = new List<SignalConnection>();
        foreach (var edge in parent.Connections)
        {
            if (edge.Destination.NodeId == wrapper.Id) continue;
            if (edge.Source.NodeId != wrapper.Id) { edges.Add(edge); continue; }
            var source = BypassInput(outputRoutes[edge.Source.PortId].Single());
            edges.Add(edge with { Source = source.Source, Amount = Multiply(source.Amount, edge.Amount) });
        }
        var movedEdges = child.Connections.Where(edge => edge.Destination.NodeId != composite.OutputRackId)
            .Select(BypassInput).ToArray();
        var parentEdgeIds = parent.Connections.Select(edge => edge.Id).ToHashSet();
        if (movedEdges.Any(edge => parentEdgeIds.Contains(edge.Id)))
            throw new GraphValidationException("An internal cable identity conflicts with a cable in the parent graph.");
        edges.AddRange(movedEdges);
        if (edges.Select(edge => edge.Id).Distinct().Count() != edges.Count)
            throw new GraphValidationException("An internal cable identity conflicts with a cable in the parent graph.");
        if (edges.Select(edge => (edge.Source, edge.Destination)).Distinct().Count() != edges.Count)
            throw new GraphValidationException("Expanding would create duplicate cables between the same sockets. Keep the composite or change the routing first.");
        return new Plan(wrapper, Array.AsReadOnly(moved), edges.AsReadOnly(), Array.AsReadOnly(movedEdges.Select(edge => edge.Id).ToArray()));
    }

    private static double Multiply(double first, double second)
    {
        var result = first * second;
        if (!double.IsFinite(result) || !float.IsFinite((float)result) ||
            ((float)result == 0 && first != 0 && second != 0))
            throw new GraphValidationException("Boundary cable amounts cannot be composed into a finite representable coefficient.");
        return result;
    }

    internal static (SignalGraph Root, Plan Plan) Build(GraphSnapshot root, UngroupCompositeCommand command, NodeRegistry registry)
    {
        if (root.Revision != command.ExpectedRevision)
            throw new GraphValidationException("The patch changed. Review the composite before expanding it.");
        var plan = Preview(root.GetGraph(command.GraphId), command.CompositeId);
        SavedGraph Rewrite(SavedGraph saved)
        {
            if (saved.Id != command.GraphId)
                return saved with { Nodes = saved.Nodes.Select(node => node.Composite is { } nested
                    ? node with { Composite = nested with { Graph = Rewrite(nested.Graph) } } : node).ToArray() };
            var wrapper = saved.Nodes.Single(node => node.Id == command.CompositeId);
            return saved with
            {
                Nodes = saved.Nodes.SelectMany(node => node.Id == wrapper.Id ? wrapper.Composite!.Graph.Nodes : [node]).ToArray(),
                Connections = plan.Connections.ToArray()
            };
        }
        try
        {
            return (PatchDocumentSerializer.RestoreGraph(Rewrite(PatchDocumentSerializer.CaptureGraph(root)), registry), plan);
        }
        catch (GraphValidationException exception)
        {
            throw new GraphValidationException($"Cannot expand this composite: {exception.Message}");
        }
    }
}
