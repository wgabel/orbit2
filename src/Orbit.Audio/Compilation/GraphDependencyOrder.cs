using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Audio.Compilation;

/// <summary>Orders only the dependencies of the master output, including modulation.
/// Unconnected components do not consume DSP resources. The domain normally guarantees a DAG.</summary>
internal static class GraphDependencyOrder
{
    public static IReadOnlyList<GraphNodeSnapshot> Get(GraphSnapshot graph, Guid outputId, out int reachableEdges,
        IEnumerable<(Guid Source, Guid Destination)>? implicitDependencies = null)
    {
        var links = graph.Connections.Select(edge => (Source: edge.Source.NodeId, Destination: edge.Destination.NodeId));
        if (implicitDependencies is not null) links = links.Concat(implicitDependencies);
        var incoming = links.ToLookup(edge => edge.Destination);
        var nodes = graph.Nodes.ToDictionary(node => node.Id);
        var reachable = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        reachableEdges = 0;
        pending.Push(outputId);
        while (pending.TryPop(out var id))
        {
            if (!reachable.Add(id)) continue;
            if (!nodes.ContainsKey(id))
                throw new FaustCompilationException($"Graph references missing node {id}.");
            foreach (var edge in incoming[id])
            {
                reachableEdges++;
                pending.Push(edge.Source);
            }
        }

        var dependencies = reachable.ToDictionary(id => id, id => incoming[id].Count());
        var outgoing = incoming.SelectMany(group => group).Where(edge => reachable.Contains(edge.Destination))
            .ToLookup(edge => edge.Source);
        var ready = new SortedSet<Guid>(dependencies.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var result = new List<GraphNodeSnapshot>();
        while (ready.Count > 0)
        {
            var id = ready.Min;
            ready.Remove(id);
            result.Add(nodes[id]);
            foreach (var edge in outgoing[id])
            {
                if (--dependencies[edge.Destination] == 0)
                    ready.Add(edge.Destination);
            }
        }

        if (result.Count != reachable.Count)
            throw new FaustCompilationException("Graph dependencies contain a cycle.");
        return result;
    }
}
