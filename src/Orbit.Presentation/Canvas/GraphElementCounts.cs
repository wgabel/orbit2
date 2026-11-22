using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Presentation.Canvas;

/// <summary>Counts authored graph elements, including composite wrappers and boundary racks.</summary>
public readonly record struct GraphElementCounts(int Nodes, int Connections)
{
    public static GraphElementCounts CountHierarchy(GraphSnapshot graph)
    {
        var nodes = graph.Nodes.Count;
        var connections = graph.Connections.Count;
        foreach (var node in graph.Nodes)
        {
            if (node.Composite is not { } composite) continue;
            var child = CountHierarchy(composite.Graph);
            nodes += child.Nodes;
            connections += child.Connections;
        }
        return new GraphElementCounts(nodes, connections);
    }
}
