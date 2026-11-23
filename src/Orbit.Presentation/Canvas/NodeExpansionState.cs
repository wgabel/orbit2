using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;

namespace Orbit.Presentation.Canvas;

/// <summary>Ephemeral, graph-local expansion of one leaf node; never a graph edit.</summary>
public sealed class NodeExpansionState
{
    public Guid? ExpandedNodeId { get; private set; }

    public static bool IsEligible(GraphNodeSnapshot node) =>
        node.Composite is null &&
        node.TypeKey is not (InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType);

    public bool IsExpanded(GraphNodeSnapshot node) => !IsEligible(node) || ExpandedNodeId == node.Id;

    public bool Expand(Guid id)
    {
        if (ExpandedNodeId == id) return false;
        ExpandedNodeId = id;
        return true;
    }

    public bool Toggle(Guid id)
    {
        ExpandedNodeId = ExpandedNodeId == id ? null : id;
        return true;
    }

    public bool Collapse()
    {
        if (ExpandedNodeId is null) return false;
        ExpandedNodeId = null;
        return true;
    }

    public bool Prune(IReadOnlyList<GraphNodeSnapshot> nodes)
    {
        if (ExpandedNodeId is not Guid id ||
            nodes.Any(node => node.Id == id && IsEligible(node))) return false;
        ExpandedNodeId = null;
        return true;
    }
}
