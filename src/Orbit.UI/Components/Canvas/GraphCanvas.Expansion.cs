using Microsoft.AspNetCore.Components.Web;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>Ephemeral expansion is separate from the persisted graph-scoped pin list.</summary>
public partial class GraphCanvas
{
    private Guid? transientExpandedNodeId;
    private Guid? pendingNodeTapId;
    private long pendingNodeTapPointer;
    private CanvasPoint pendingNodeTapStart;
    private readonly HashSet<Guid> editingCompactParameters = [];

    private bool IsNodeExpanded(Guid id) =>
        transientExpandedNodeId == id || Snapshot.PinnedNodeIds.Contains(id);
    private bool IsNodePinned(Guid id) => Snapshot.PinnedNodeIds.Contains(id);

    private void ToggleNodeExpanded(Guid nodeId)
    {
        if (editingCompactParameters.Count > 0) return;
        if (IsNodeExpanded(nodeId))
        {
            if (IsNodePinned(nodeId)) State.SetNodePinned(nodeId, false);
            if (transientExpandedNodeId == nodeId) transientExpandedNodeId = null;
        }
        else
        {
            transientExpandedNodeId = nodeId;
            State.SelectNode(nodeId);
        }
        visualizationGeometryGeneration++;
        StateHasChanged();
    }

    private void ToggleNodePin(Guid nodeId)
    {
        if (editingCompactParameters.Count > 0) return;
        var pinned = !IsNodePinned(nodeId);
        State.SetNodePinned(nodeId, pinned);
        if (pinned) transientExpandedNodeId = nodeId;
        visualizationGeometryGeneration++;
        StateHasChanged();
    }

    private void SetCompactEdit(Guid parameterId, bool editing)
    {
        if (editing) editingCompactParameters.Add(parameterId);
        else editingCompactParameters.Remove(parameterId);
    }

    private void BeginNodeTap(Guid nodeId, PointerEventArgs args)
    {
        pendingNodeTapId = nodeId;
        pendingNodeTapPointer = args.PointerId;
        pendingNodeTapStart = new CanvasPoint(args.ClientX, args.ClientY);
    }

    private void TrackNodeTap(CanvasPoint current)
    {
        if (pendingNodeTapId is null) return;
        var dx = current.X - pendingNodeTapStart.X;
        var dy = current.Y - pendingNodeTapStart.Y;
        if (dx * dx + dy * dy > 64) pendingNodeTapId = null;
    }

    private void FinishNodeTap(PointerEventArgs args)
    {
        if (pendingNodeTapId is not Guid nodeId || pendingNodeTapPointer != args.PointerId ||
            editingCompactParameters.Count > 0) return;
        TrackNodeTap(new CanvasPoint(args.ClientX, args.ClientY));
        if (pendingNodeTapId is null) return;
        if (!Snapshot.Graph.Nodes.Any(node => node.Id == nodeId && CompactNodePresentation.IsExpandable(node))) return;
        transientExpandedNodeId = nodeId;
        visualizationGeometryGeneration++;
        StateHasChanged();
    }

    private void CollapseUnpinned()
    {
        if (editingCompactParameters.Count > 0 || transientExpandedNodeId is null) return;
        transientExpandedNodeId = null;
        visualizationGeometryGeneration++;
        StateHasChanged();
    }

    private void ReconcileNodeExpansion()
    {
        if (transientExpandedNodeId is Guid nodeId &&
            !Snapshot.Graph.Nodes.Any(node => node.Id == nodeId && CompactNodePresentation.IsExpandable(node)))
            transientExpandedNodeId = null;
        var valid = Snapshot.Graph.Nodes.SelectMany(node => node.Parameters).Select(parameter => parameter.Id).ToHashSet();
        editingCompactParameters.RemoveWhere(id => !valid.Contains(id));
    }
}
