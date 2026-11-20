using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Graphs;

/// <summary>
/// Enforces graph invariants before mutation. Both audio and parameter modulation
/// edges participate in cycle detection; explicit feedback is not supported yet.
/// </summary>
public sealed class SignalGraphValidator
{
    public void ValidateNode(SignalGraph graph, ISignalNode node)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(node);

        if (node.Id == Guid.Empty || string.IsNullOrWhiteSpace(node.TypeKey) ||
            string.IsNullOrWhiteSpace(node.Name))
            throw new GraphValidationException("A node needs a valid ID, type key and name.");

        if (graph.Nodes.Any(existing => existing.Id == node.Id))
            throw new GraphValidationException($"Node {node.Id} already exists.");

        if (node is SignalNode { OwningGraph: not null })
            throw new GraphValidationException("This node instance already belongs to a graph.");
        if (node is InputRack inputRack && !ReferenceEquals(graph.Owner?.InputRack, inputRack) ||
            node is OutputRack outputRack && !ReferenceEquals(graph.Owner?.OutputRack, outputRack))
            throw new GraphValidationException("A boundary rack belongs only to its own composite graph.");
        if (graph.Owner is not null && node is MasterOutput)
            throw new GraphValidationException("A child graph uses its Output Rack, not a master output.");
        if (node is ICompositeNode composite)
        {
            if (!ReferenceEquals(composite.InternalGraph.Owner, composite))
                throw new GraphValidationException("A composite must own its child graph.");
            // Walk ancestors, including the immediate owner. A composite cannot
            // be placed in its own graph, even indirectly through another child.
            for (SignalGraph? ancestor = graph; ancestor is not null; ancestor = ancestor.Owner?.OwningGraph)
                if (ReferenceEquals(ancestor.Owner, composite))
                    throw new GraphValidationException("A composite cannot contain itself or an ancestor.");
        }

        var ports = node.Inputs.Concat(node.Outputs).ToArray();

        if (ports.Any(port => port.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(port.Key) || string.IsNullOrWhiteSpace(port.Name) ||
                port.ChannelCount < 1 || !Enum.IsDefined(port.SignalType) ||
                !Enum.IsDefined(port.Direction) || !Enum.IsDefined(port.FanIn)))
            throw new GraphValidationException("Every port needs a valid definition.");

        if (ports.Select(port => port.Id).Distinct().Count() != ports.Length ||
            ports.Select(port => port.Key).Distinct(StringComparer.Ordinal).Count() != ports.Length)
            throw new GraphValidationException("Port IDs and keys must be unique within a node.");

        if (node.Inputs.Any(port => port.Direction != PortDirection.Input) ||
            node.Outputs.Any(port => port.Direction != PortDirection.Output))
            throw new GraphValidationException("Ports must be listed under the correct direction.");

        if (node.Parameters.Select(parameter => parameter.Id).Distinct().Count() != node.Parameters.Count ||
            node.Parameters.Select(parameter => parameter.Definition.Key)
                .Distinct(StringComparer.Ordinal).Count() != node.Parameters.Count)
            throw new GraphValidationException("Parameter IDs and keys must be unique within a node.");

        var modulationPorts = new HashSet<Guid>();

        foreach (var parameter in node.Parameters)
        {
            if (parameter.ModulationPortId is not Guid portId)
                continue;

            var port = node.Inputs.SingleOrDefault(candidate => candidate.Id == portId);
            if (port is null || port.SignalType != SignalType.Modulation ||
                port.ChannelCount != 1 || !modulationPorts.Add(portId))
                throw new GraphValidationException(
                    "Each modulatable parameter needs its own mono modulation input.");
        }
    }

    public void ValidateConnection(SignalGraph graph, SignalConnection connection)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Id == Guid.Empty || !double.IsFinite(connection.Amount))
            throw new GraphValidationException("Connection ID and amount must be valid.");

        var source = graph.GetNode(connection.Source.NodeId);
        var destination = graph.GetNode(connection.Destination.NodeId);
        var output = source.Outputs.SingleOrDefault(port => port.Id == connection.Source.PortId);
        var input = destination.Inputs.SingleOrDefault(port => port.Id == connection.Destination.PortId);

        if (output is null || input is null)
            throw new GraphValidationException("A connection must link an existing output to an existing input.");

        if (!AreCompatible(output, input))
            throw new GraphValidationException("Port signal types or channel counts are incompatible.");

        if (graph.Connections.Any(existing => existing.Id == connection.Id ||
            (existing.Source == connection.Source && existing.Destination == connection.Destination)))
            throw new GraphValidationException("The connection already exists.");

        if (input.FanIn == FanInPolicy.Single &&
            graph.Connections.Any(existing => existing.Destination == connection.Destination))
            throw new GraphValidationException("This input accepts only one connection.");

        if (CreatesCycle(graph, source.Id, destination.Id))
            throw new GraphValidationException("The connection would create a dependency cycle.");
    }

    // A mono audio oscillator can drive a modulation input at any rate. The
    // reverse conversion or channel-count changes require an explicit adapter.
    private static bool AreCompatible(PortDefinition output, PortDefinition input) =>
        output.ChannelCount == input.ChannelCount &&
        (output.SignalType == input.SignalType ||
            (output.SignalType == SignalType.Audio && input.SignalType == SignalType.Modulation));

    private static bool CreatesCycle(SignalGraph graph, Guid sourceId, Guid destinationId)
    {
        // If destination can already reach source, adding source -> destination
        // closes a cycle. Traversal also rejects self-connections.
        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(destinationId);

        while (pending.TryPop(out var current))
        {
            if (current == sourceId)
                return true;

            if (!visited.Add(current))
                continue;

            foreach (var connection in graph.Connections)
            {
                if (connection.Source.NodeId == current)
                    pending.Push(connection.Destination.NodeId);
            }
        }

        return false;
    }
}
