using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;

namespace Orbit.Application.Graphs.Snapshots;

/// <summary>Stable read model for canvas projections and asynchronous DSP compilation.
/// Subsequent edits do not modify previously published snapshots.</summary>
public sealed record GraphSnapshot(
    Guid Id, long Revision,
    IReadOnlyList<GraphNodeSnapshot> Nodes,
    IReadOnlyList<SignalConnection> Connections)
{
    public GraphNodeSnapshot GetNode(Guid nodeId) =>
        Nodes.SingleOrDefault(node => node.Id == nodeId)
        ?? throw new GraphValidationException($"Node {nodeId} does not exist in this snapshot.");

    /// <summary>Resolve a canvas by graph ID without exposing a mutable domain graph.</summary>
    public GraphSnapshot GetGraph(Guid graphId) =>
        FindGraph(graphId) ?? throw new GraphValidationException($"Graph {graphId} does not exist in this snapshot.");

    /// <summary>Returns null for a removed or foreign graph without throwing.</summary>
    public GraphSnapshot? FindGraph(Guid graphId)
    {
        if (Id == graphId) return this;
        foreach (var node in Nodes)
            if (node.Composite?.Graph.FindGraph(graphId) is { } found)
                return found;
        return null;
    }

    /// <summary>Return a derived snapshot for DSP/control consumers. The authored
    /// snapshot, including every manual base value, remains untouched.
    /// Inactive mappings keep their ownership but do not override dormant parameters.</summary>
    public GraphSnapshot WithEffectiveMacroBaseValues()
    {
        var visited = new HashSet<Guid>();
        return Resolve(this, Array.Empty<CompositeMacroDefinition>());

        GraphSnapshot Resolve(GraphSnapshot graph, IReadOnlyList<CompositeMacroDefinition> macros)
        {
            // Leave invalid repeated graphs to the existing flattener validation.
            if (!visited.Add(graph.Id)) return graph;
            var mapped = macros.Count == 0 ? null : macros.SelectMany(macro => macro.ResolveBaseValues())
                .ToDictionary(entry => entry.Target, entry => entry.Value);
            GraphNodeSnapshot[]? changedNodes = null;
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var original = graph.Nodes[i];
                var node = original;
                if (mapped is not null)
                {
                    ParameterSnapshot[]? changedParameters = null;
                    for (var j = 0; j < node.Parameters.Count; j++)
                    {
                        var parameter = node.Parameters[j];
                        if (mapped.TryGetValue(new ParameterAddress(node.Id, parameter.Id), out var value) &&
                            node.IsParameterActive(parameter) && parameter.BaseValue != value)
                        {
                            changedParameters ??= node.Parameters.ToArray();
                            changedParameters[j] = parameter with { BaseValue = value };
                        }
                    }
                    if (changedParameters is not null)
                        node = node with { Parameters = Array.AsReadOnly(changedParameters) };
                }

                if (node.Composite is { } composite)
                {
                    var child = Resolve(composite.Graph, composite.Macros);
                    if (!ReferenceEquals(child, composite.Graph))
                        node = node with { Composite = composite with { Graph = child } };
                }

                if (!ReferenceEquals(node, original))
                {
                    changedNodes ??= graph.Nodes.ToArray();
                    changedNodes[i] = node;
                }
            }

            return changedNodes is null ? graph : graph with { Nodes = Array.AsReadOnly(changedNodes) };
        }
    }

    internal static GraphSnapshot Capture(SignalGraph graph, long revision) => new(
        graph.Id, revision,
        Array.AsReadOnly(graph.Nodes.Select(node => GraphNodeSnapshot.Capture(node, revision)).ToArray()),
        Array.AsReadOnly(graph.Connections.ToArray()));
}
