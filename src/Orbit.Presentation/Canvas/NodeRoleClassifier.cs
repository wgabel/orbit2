using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Ports;

namespace Orbit.Presentation.Canvas;

/// <summary>Classifies node accents from type and routing, not oscillator frequency.</summary>
public static class NodeRoleClassifier
{
    public static NodeVisualRole Classify(GraphSnapshot graph, GraphNodeSnapshot node)
    {
        if (node.TypeKey.StartsWith("generator.", StringComparison.Ordinal))
        {
            var outgoing = graph.Connections.Where(edge => edge.Source.NodeId == node.Id).ToArray();
            return outgoing.Length > 0 && outgoing.All(edge =>
                graph.GetNode(edge.Destination.NodeId).Inputs.Single(port => port.Id == edge.Destination.PortId)
                    .SignalType == SignalType.Modulation)
                ? NodeVisualRole.Modulator : NodeVisualRole.Generator;
        }
        if (node.TypeKey.StartsWith("processor.", StringComparison.Ordinal)) return NodeVisualRole.Processor;
        if (node.TypeKey.StartsWith("composite.", StringComparison.Ordinal)) return NodeVisualRole.Composite;
        return NodeVisualRole.Routing;
    }
}
