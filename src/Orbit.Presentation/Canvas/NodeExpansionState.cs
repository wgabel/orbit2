using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;

namespace Orbit.Presentation.Canvas;

/// <summary>Ephemeral, graph-local manual compact-view preferences; never graph edits.</summary>
public sealed class NodeExpansionState
{
    private readonly HashSet<Guid> collapsedNodeIds = [];

    public static bool IsEligible(GraphNodeSnapshot node) =>
        node.Composite is null &&
        node.TypeKey is not (InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType);

    public bool IsExpanded(GraphNodeSnapshot node) => !IsEligible(node) || !collapsedNodeIds.Contains(node.Id);

    public bool Expand(Guid id) => collapsedNodeIds.Remove(id);

    public bool Toggle(Guid id)
    {
        if (!collapsedNodeIds.Add(id)) collapsedNodeIds.Remove(id);
        return true;
    }

    public bool Reset()
    {
        if (collapsedNodeIds.Count == 0) return false;
        collapsedNodeIds.Clear();
        return true;
    }

    public bool Prune(IReadOnlyList<GraphNodeSnapshot> nodes) =>
        collapsedNodeIds.RemoveWhere(id => !nodes.Any(node => node.Id == id && IsEligible(node))) > 0;
}
