using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Notifications;
using Orbit.Application.Persistence;
using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Presentation.Canvas;

/// <summary>Owns one cached presentation state per visited graph and the current
/// navigation path. No browser, audio or domain mutation is performed here.
/// Like CanvasStateService, use on the presentation dispatcher.</summary>
public sealed class CanvasNavigationService : IDisposable
{
    private readonly IPatchSession session;
    private readonly INodePlacementPolicy? placement;
    private readonly Dictionary<Guid, ICanvasStateService> canvases = [];
    private readonly Dictionary<Guid, IDisposable> outlineSubscriptions = [];
    private PatchOutline? outline;
    private string? outlineTitle;
    private readonly Dictionary<Guid, SavedCanvas> removedLayouts = [];
    private readonly HashSet<Guid> unvisitedGroupedLayouts = [];
    private readonly List<CanvasBreadcrumb> path = [];
    private readonly NotificationChannel<CanvasStateSnapshot> changes = new();
    private readonly IDisposable patchSubscription;
    private IDisposable? activeSubscription;
    private bool disposed;

    public CanvasNavigationService(IPatchSession session, INodePlacementPolicy? placement = null,
        SavedCanvas[]? layouts = null, Guid? activeGraphId = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.placement = placement;
        PatchCounts = GraphElementCounts.CountHierarchy(session.Snapshot);
        // Register before creating projections. Removed canvases can then be
        // released outside their own layout notifications.
        patchSubscription = session.Subscribe(OnPatchCommitted);
        path.Add(new CanvasBreadcrumb(session.GraphId, "Patch"));
        if (layouts is not null)
            foreach (var layout in layouts.Where(layout => session.Snapshot.FindGraph(layout.GraphId) is not null))
                AddCanvas(layout.GraphId, new CanvasStateService(session, layout.GraphId, placement, layout));
        Activate(session.GraphId);
        if (activeGraphId is Guid target && target != session.GraphId)
        {
            if (!TryFindPath(session.Snapshot, target, out var children))
                throw new InvalidOperationException($"Saved active canvas {target} does not exist in patch {session.GraphId}.");
            path.AddRange(children);
            Activate(target);
        }
    }

    public SavedCanvas[] CaptureLayouts() => canvases.Values.Select(canvas => canvas.Snapshot)
        .Where(snapshot => snapshot.IsAvailable).Select(CaptureLayout).ToArray();

    private static SavedCanvas CaptureLayout(CanvasStateSnapshot snapshot) => new(snapshot.GraphId,
            snapshot.Viewport.Pan.X, snapshot.Viewport.Pan.Y, snapshot.Viewport.Zoom,
            snapshot.NodePositions.Select(pair => new SavedNodePosition(pair.Key, pair.Value.X, pair.Value.Y)).ToArray())
        {
            ConnectionLabels = snapshot.ConnectionLabels,
            AnnotationVisibility = snapshot.AnnotationVisibility,
            Visualizations = snapshot.Visualizations.Select(pair => new SavedNodeVisualization(pair.Key, pair.Value)).ToArray(),
            ExpandedVisualizationNodeId = snapshot.ExpandedVisualizationNodeId,
            Notes = snapshot.Notes.ToArray(),
            NodeDescriptions = snapshot.NodeDescriptions.Values.ToArray(),
            ConnectionDescriptions = snapshot.ConnectionDescriptions.Values.ToArray(),
            VisualGroups = snapshot.VisualGroups.ToArray()
        };

    private bool TryFindPath(GraphSnapshot graph, Guid target, out List<CanvasBreadcrumb> path)
    {
        path = [];
        if (graph.Id == target) return true;
        foreach (var node in graph.Nodes)
            if (node.Composite is { } composite && TryFindPath(composite.Graph, target, out var children))
            {
                path.Add(new CanvasBreadcrumb(composite.Graph.Id, canvases.TryGetValue(graph.Id, out var canvas) ? canvas.Snapshot.DisplayName(node.Id) : node.Name));
                path.AddRange(children);
                return true;
            }
        return false;
    }

    public ICanvasStateService ActiveCanvas { get; private set; } = default!;
    /// <summary>Root and all descendant graph elements, refreshed once per patch commit.</summary>
    public GraphElementCounts PatchCounts { get; private set; }
    public PatchOutline GetOutline(string title)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (outline is null || outlineTitle != title)
        {
            outline = new PatchOutline(session.Snapshot, canvases.Values.Select(canvas => canvas.Snapshot), title);
            outlineTitle = title;
        }
        return outline;
    }
    public IReadOnlyList<CanvasBreadcrumb> Path => path.AsReadOnly();
    /// <summary>Fit a newly visited canvas once. Returning must not reset its viewport.</summary>
    public bool IsFirstVisit { get; private set; }

    public IDisposable Subscribe(Action<CanvasStateSnapshot> handler) => changes.Subscribe(handler);

    public bool OpenChild(Guid compositeNodeId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var graph = session.Snapshot.GetGraph(ActiveCanvas.Snapshot.GraphId);
        var node = graph.Nodes.SingleOrDefault(candidate => candidate.Id == compositeNodeId);
        if (node?.Composite is not { } composite) return false;
        path.Add(new CanvasBreadcrumb(composite.Graph.Id, ActiveCanvas.Snapshot.DisplayName(node.Id)));
        Activate(composite.Graph.Id);
        Publish();
        return true;
    }

    public bool NavigateTo(Guid graphId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var index = path.FindIndex(crumb => crumb.GraphId == graphId);
        if (index < 0 || index == path.Count - 1) return false;
        path.RemoveRange(index + 1, path.Count - index - 1);
        Activate(graphId);
        Publish();
        return true;
    }

    /// <summary>Jump to any live graph, including unvisited descendants and siblings.</summary>
    public bool JumpToGraph(Guid graphId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (graphId == ActiveCanvas.Snapshot.GraphId) return true;
        if (!TryFindPath(session.Snapshot, graphId, out var children)) return false;
        path.Clear();
        path.Add(new CanvasBreadcrumb(session.GraphId, "Patch"));
        path.AddRange(children);
        Activate(graphId);
        Publish();
        return true;
    }

    private void AddCanvas(Guid graphId, ICanvasStateService canvas)
    {
        canvases.Add(graphId, canvas);
        outlineSubscriptions.Add(graphId, canvas.Subscribe(change =>
        {
            if (change.Kind is CanvasChangeKind.NotesChanged or CanvasChangeKind.GroupsChanged or CanvasChangeKind.DescriptionsChanged or CanvasChangeKind.GraphCommitted)
                outline = null;
        }));
    }

    private void Activate(Guid graphId)
    {
        activeSubscription?.Dispose();
        IsFirstVisit = !canvases.TryGetValue(graphId, out var canvas) || unvisitedGroupedLayouts.Remove(graphId);
        if (canvas is null)
        {
            removedLayouts.Remove(graphId, out var restored);
            if (restored is not null) IsFirstVisit = false;
            canvas = new CanvasStateService(session, graphId, placement, restored);
            AddCanvas(graphId, canvas);
            outline = null;
        }
        ActiveCanvas = canvas;
        activeSubscription = canvas.Subscribe(_ => Publish());
    }

    private void OnPatchCommitted(GraphChangeSet change)
    {
        outline = null;
        PatchCounts = GraphElementCounts.CountHierarchy(change.Snapshot);
        foreach (var grouped in change.Events.OfType<NodesGrouped>())
            if (canvases.TryGetValue(grouped.GraphId, out var parent))
            {
                var child = grouped.Composite.Composite!;
                var selected = grouped.NodeIds.ToHashSet();
                var source = parent.Snapshot;
                var anchor = GroupAnchor(source, selected);
                var spacing = (placement ?? new GridNodePlacementPolicy()).FindOutputRackPosition([CanvasPoint.Zero]);
                var positions = source.NodePositions.Where(pair => selected.Contains(pair.Key))
                    .Select(pair => new SavedNodePosition(pair.Key, pair.Value.X - anchor.X + spacing.X, pair.Value.Y - anchor.Y)).ToList();
                positions.Add(new SavedNodePosition(child.InputRackId, 0, 0));
                var output = (placement ?? new GridNodePlacementPolicy()).FindOutputRackPosition(positions.Select(p => new CanvasPoint(p.X, p.Y)));
                positions.Add(new SavedNodePosition(child.OutputRackId, output.X, output.Y));
                var edgeIds = child.Graph.Connections.Select(edge => edge.Id).ToHashSet();
                var layout = new SavedCanvas(child.Graph.Id, 0, 0, 1, positions.ToArray())
                {
                    ConnectionLabels = source.ConnectionLabels,
                    AnnotationVisibility = source.AnnotationVisibility,
                    NodeDescriptions = source.NodeDescriptions.Values.Where(value => selected.Contains(value.NodeId)).ToArray(),
                    ConnectionDescriptions = source.ConnectionDescriptions.Values.Where(value => edgeIds.Contains(value.ConnectionId)).ToArray(),
                    Visualizations = source.Visualizations.Where(pair => selected.Contains(pair.Key)).Select(pair => new SavedNodeVisualization(pair.Key, pair.Value)).ToArray()
                };
                AddCanvas(child.Graph.Id, new CanvasStateService(session, child.Graph.Id, placement, layout));
                unvisitedGroupedLayouts.Add(child.Graph.Id);
            }
        // Restored subtrees must be searchable before they are visited again.
        foreach (var pair in removedLayouts.Where(pair => change.Snapshot.FindGraph(pair.Key) is not null).ToArray())
        {
            AddCanvas(pair.Key, new CanvasStateService(session, pair.Key, placement, pair.Value));
            removedLayouts.Remove(pair.Key);
        }
        // If an ancestor was deleted, navigate back to the closest surviving
        // canvas and dispose cached projections for the removed subtree.
        var removed = canvases.Keys.Where(id => change.Snapshot.FindGraph(id) is null).ToArray();
        var activeRemoved = removed.Contains(ActiveCanvas.Snapshot.GraphId);
        if (activeRemoved)
        {
            var firstMissing = path.FindIndex(crumb => removed.Contains(crumb.GraphId));
            path.RemoveRange(firstMissing, path.Count - firstMissing);
            Activate(path[^1].GraphId);
        }
        foreach (var id in removed)
        {
            removedLayouts[id] = CaptureLayout(canvases[id].Snapshot);
            outlineSubscriptions[id].Dispose();
            outlineSubscriptions.Remove(id);
            canvases[id].Dispose();
            canvases.Remove(id);
            unvisitedGroupedLayouts.Remove(id);
        }
        // Grouping history can move a still-live descendant to a different depth.
        // Rebuild its breadcrumbs from actual ownership rather than the old path.
        if (change.Events.Any(fact => fact is NodesGrouped or GraphHistoryRestored) &&
            TryFindPath(change.Snapshot, ActiveCanvas.Snapshot.GraphId, out var actualPath) &&
            !path.Skip(1).Select(crumb => crumb.GraphId).SequenceEqual(actualPath.Select(crumb => crumb.GraphId)))
        {
            path.RemoveRange(1, path.Count - 1);
            path.AddRange(actualPath);
        }
        while (removedLayouts.Count > 100) removedLayouts.Remove(removedLayouts.Keys.First());
        // The surviving active projection receives this same patch commit next
        // and publishes the reconciled state (including the truncated path).
    }

    internal static CanvasPoint GroupAnchor(CanvasStateSnapshot source, IReadOnlySet<Guid> selected) =>
        new(source.NodePositions.Where(pair => selected.Contains(pair.Key)).Min(pair => pair.Value.X),
            source.NodePositions.Where(pair => selected.Contains(pair.Key)).Min(pair => pair.Value.Y));

    private void Publish()
    {
        var errors = changes.Publish(ActiveCanvas.Snapshot);
        if (errors.Count > 0)
            throw new AggregateException("Canvas navigation subscriber failures.", errors);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        patchSubscription.Dispose();
        activeSubscription?.Dispose();
        foreach (var listener in outlineSubscriptions.Values) listener.Dispose();
        outlineSubscriptions.Clear();
        foreach (var canvas in canvases.Values) canvas.Dispose();
        canvases.Clear();
        removedLayouts.Clear();
        changes.Clear();
    }
}
