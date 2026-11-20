using Orbit.Domain.Graphs;

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

    internal static GraphSnapshot Capture(SignalGraph graph, long revision) => new(
        graph.Id, revision,
        Array.AsReadOnly(graph.Nodes.Select(node => GraphNodeSnapshot.Capture(node, revision)).ToArray()),
        Array.AsReadOnly(graph.Connections.ToArray()));
}
