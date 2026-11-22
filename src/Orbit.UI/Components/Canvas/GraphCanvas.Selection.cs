using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Presentation.Canvas;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;

namespace Orbit.UI.Components.Canvas;

public partial class GraphCanvas
{
    private bool selectionPanelOpen;
    private bool marqueeMode;
    private string selectionCandidate = "";
    private string compositeName = "New composite";
    private sealed record MarqueeGesture(long PointerId, CanvasPoint Start, CanvasPoint End,
        CanvasStateSnapshot Snapshot, bool Additive, ViewportMeasurement? Bounds = null);
    private MarqueeGesture? marquee;
    private IEnumerable<Orbit.Application.Graphs.Snapshots.GraphNodeSnapshot> EligibleNodes =>
        Snapshot.Graph.Nodes.Where(ModuleSelection.IsEligible);
    private int SelectedEligible => EligibleNodes.Count(node => Snapshot.SelectedNodeIds.Contains(node.Id));
    private int SelectedProtected => Snapshot.SelectedNodeIds.Count - SelectedEligible;
    private bool SelectedVisualGroupMember => Snapshot.VisualGroups.Any(group => group.NodeIds.Any(Snapshot.SelectedNodeIds.Contains));
    private bool CanGroup => !AnnotationControlsLocked && OnGroupNodes.HasDelegate && SelectedEligible is > 0 and <= 512 &&
        SelectedProtected == 0 && Snapshot.SelectedConnectionIds.Count == 0 && !SelectedVisualGroupMember;
    private IReadOnlyList<CompositeGrouping.Crossing> GroupCrossings => CanGroup
        ? CompositeGrouping.Preview(Snapshot.Graph, Snapshot.SelectedNodeIds) : [];
    private Orbit.Application.Graphs.Snapshots.GraphNodeSnapshot? SelectedComposite => Snapshot.SelectedNodeIds.Count == 1 &&
        Snapshot.SelectedConnectionIds.Count == 0 ? Snapshot.Graph.Nodes.SingleOrDefault(node =>
            node.Id == Snapshot.SelectedNodeIds[0] && node.Composite is not null) : null;

    private async Task UngroupCompositeAsync()
    {
        if (AnnotationControlsLocked || !OnUngroupComposite.HasDelegate || SelectedComposite is not { } composite) return;
        EndGesture();
        pendingSource = null;
        selectionPanelOpen = false;
        await OnUngroupComposite.InvokeAsync(new UngroupCompositeCommand(Snapshot.GraphId, composite.Id, Snapshot.GraphRevision));
        if (MobileSelectionOpen) await OnMobileToolCompleted.InvokeAsync();
    }

    private async Task CreateCompositeAsync()
    {
        if (!CanGroup || string.IsNullOrWhiteSpace(compositeName)) return;
        EndGesture();
        pendingSource = null;
        selectionPanelOpen = false;
        await OnGroupNodes.InvokeAsync(new GroupNodesCommand(Snapshot.GraphId, Snapshot.SelectedNodeIds.ToArray(),
            compositeName, Snapshot.GraphRevision));
        if (MobileSelectionOpen) await OnMobileToolCompleted.InvokeAsync();
    }
    private string SelectionSummary => $"{Snapshot.SelectedNodeIds.Count} nodes selected · {SelectedEligible} eligible" +
        (SelectedProtected > 0 ? $" · {SelectedProtected} protected" : "") +
        (Snapshot.SelectedConnectionIds.Count > 0 ? $" · {Snapshot.SelectedConnectionIds.Count} cables selected" : "");

    private async Task ToggleMarqueeMode()
    {
        if (AnnotationControlsLocked) return;
        EndGesture();
        pendingSource = null;
        marqueeMode = !marqueeMode;
        selectionPanelOpen = false;
        if (MobileSelectionOpen) await OnMobileToolCompleted.InvokeAsync();
    }

    private async Task CloseSelectionAsync()
    {
        selectionPanelOpen = false;
        if (MobileSelectionOpen) await OnCloseMobileTools.InvokeAsync();
    }

    private void SelectAllModuleNodes()
    {
        if (AnnotationControlsLocked) return;
        EndGesture();
        pendingSource = null;
        State.SelectModuleNodes(ModuleSelection.EligibleIds(Snapshot.Graph));
    }

    private void ClearModuleSelection()
    {
        if (AnnotationControlsLocked) return;
        EndGesture();
        pendingSource = null;
        State.ClearSelection();
    }

    private bool CandidateExists => Guid.TryParse(selectionCandidate, out var id) &&
        EligibleNodes.Any(node => node.Id == id);

    private void ToggleCandidate()
    {
        if (AnnotationControlsLocked || !CandidateExists || !Guid.TryParse(selectionCandidate, out var id)) return;
        EndGesture();
        pendingSource = null;
        var selected = Snapshot.SelectedNodeIds.ToHashSet();
        if (!selected.Add(id)) selected.Remove(id);
        State.SelectModuleNodes(selected);
    }

    private async Task BeginMarqueeAsync(PointerEventArgs args)
    {
        if (module is null || AnnotationControlsLocked) return;
        pendingSource = null;
        var point = new CanvasPoint(args.ClientX, args.ClientY);
        var gesture = new MarqueeGesture(args.PointerId, point, point, Snapshot, args.CtrlKey || args.MetaKey);
        marquee = gesture;
        await BeginGesture(args, null, wire: false);
        var bounds = await module.InvokeAsync<ViewportMeasurement>("viewportBounds", viewportElement);
        if (marquee is { } active && active.Snapshot == gesture.Snapshot && active.PointerId == gesture.PointerId)
            marquee = active with { Bounds = bounds };
    }

    private string? MarqueeStyle
    {
        get
        {
            if (marquee is not { Bounds: { } bounds } gesture) return null;
            var rectangle = ModuleSelection.Rectangle(gesture.Start, gesture.End);
            return FormattableString.Invariant($"left:{rectangle.Left - bounds.Left}px;top:{rectangle.Top - bounds.Top}px;width:{rectangle.Width}px;height:{rectangle.Height}px");
        }
    }

    private async Task CompleteMarqueeAsync(MarqueeGesture gesture)
    {
        if (gesture.Bounds is not { } bounds || gesture.Snapshot != Snapshot || AnnotationControlsLocked) return;
        var screen = ModuleSelection.Rectangle(gesture.Start, gesture.End);
        if (screen.Width < 3 || screen.Height < 3) return; // click/line is not a box; preserve selection
        var state = State;
        var measured = await MeasurePlacementAsync();
        if (disposed || !ReferenceEquals(State, state) || gesture.Snapshot != Snapshot ||
            gesturePointer is not null || AnnotationControlsLocked || measured is null) return;
        var viewport = gesture.Snapshot.Viewport;
        var rectangle = ModuleSelection.Rectangle(
            viewport.ScreenToWorld(new CanvasPoint(screen.Left - bounds.Left, screen.Top - bounds.Top)),
            viewport.ScreenToWorld(new CanvasPoint(screen.Right - bounds.Left, screen.Bottom - bounds.Top)));
        State.SelectModuleNodes(ModuleSelection.HitTest(Snapshot.Graph, rectangle, measured.Value.NodeBounds), gesture.Additive);
    }
}
