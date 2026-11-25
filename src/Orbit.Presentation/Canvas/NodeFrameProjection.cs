using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;

namespace Orbit.Presentation.Canvas;

/// <summary>Canvas-local presentation cache. Never substitutes snapshots for graph commands or DSP.</summary>
public sealed class NodeFrameProjection
{
    public sealed record Entry(GraphNodeSnapshot Node, NodeVisualRole Role,
        IReadOnlyDictionary<ParameterAddress, ParameterRange?> Ranges);

    private static readonly IReadOnlyDictionary<ParameterAddress, ParameterRange?> EmptyRanges =
        new System.Collections.ObjectModel.ReadOnlyDictionary<ParameterAddress, ParameterRange?>(
            new Dictionary<ParameterAddress, ParameterRange?>());
    private GraphSnapshot? previousGraph;
    private IReadOnlyDictionary<ParameterAddress, ParameterRange?>? previousRanges;
    private IReadOnlyList<Entry> entries = [];

    public IReadOnlyList<Entry> Update(GraphSnapshot graph,
        IReadOnlyDictionary<ParameterAddress, ParameterRange?> ranges)
    {
        var graphChanged = !ReferenceEquals(previousGraph, graph);
        var rangesChanged = !ReferenceEquals(previousRanges, ranges);
        if (!graphChanged && !rangesChanged) return entries;

        var prior = previousGraph?.Id == graph.Id
            ? entries.ToDictionary(entry => entry.Node.Id) : new Dictionary<Guid, Entry>();
        // The estimator publishes new dictionaries on committed graph changes.
        // Group once, not once per node on every viewport or telemetry render.
        var grouped = ranges.ToLookup(pair => pair.Key.NodeId);
        var next = new List<Entry>(graph.Nodes.Count);
        foreach (var node in graph.Nodes.OrderBy(node => node.TypeKey is InputRack.NodeType or OutputRack.NodeType ? 1 : 0))
        {
            prior.TryGetValue(node.Id, out var old);
            var stable = old is not null && SameLeaf(old.Node, node) ? old.Node : node;
            var role = !graphChanged && old is not null ? old.Role : NodeRoleClassifier.Classify(graph, node);
            var items = grouped[node.Id];
            IReadOnlyDictionary<ParameterAddress, ParameterRange?> local =
                old is not null && SameRanges(old.Ranges, items) ? old.Ranges :
                !items.Any() ? EmptyRanges :
                new System.Collections.ObjectModel.ReadOnlyDictionary<ParameterAddress, ParameterRange?>(
                    items.ToDictionary(pair => pair.Key, pair => pair.Value));
            next.Add(old is not null && ReferenceEquals(old.Node, stable) && old.Role == role &&
                ReferenceEquals(old.Ranges, local) ? old : new Entry(stable, role, local));
        }
        previousGraph = graph;
        previousRanges = ranges;
        entries = next.AsReadOnly();
        return entries;
    }

    public void Reset()
    {
        previousGraph = null;
        previousRanges = null;
        entries = [];
    }

    private static bool SameLeaf(GraphNodeSnapshot left, GraphNodeSnapshot right) =>
        ReferenceEquals(left, right) ||
        // Preserve all scalar/record properties via record equality, normalize
        // only the newly allocated immutable lists after comparing their contents.
        left.Composite is null && right.Composite is null &&
        left.Parameters.SequenceEqual(right.Parameters) &&
        left.Inputs.SequenceEqual(right.Inputs) && left.Outputs.SequenceEqual(right.Outputs) &&
        (left with { Parameters = right.Parameters, Inputs = right.Inputs, Outputs = right.Outputs }) == right;

    private static bool SameRanges(IReadOnlyDictionary<ParameterAddress, ParameterRange?> old,
        IEnumerable<KeyValuePair<ParameterAddress, ParameterRange?>> current)
    {
        var count = 0;
        foreach (var pair in current)
        {
            if (!old.TryGetValue(pair.Key, out var value) || value != pair.Value) return false;
            count++;
        }
        return count == old.Count;
    }
}
