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

    internal static GraphSnapshot Capture(SignalGraph graph, long revision) => new(
        graph.Id, revision,
        Array.AsReadOnly(graph.Nodes.Select(GraphNodeSnapshot.Capture).ToArray()),
        Array.AsReadOnly(graph.Connections.ToArray()));
}
