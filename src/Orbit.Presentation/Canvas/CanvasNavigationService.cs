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
    private readonly Dictionary<Guid, SavedCanvas> removedLayouts = [];
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
        // Register before creating projections. Removed canvases can then be
        // released outside their own layout notifications.
        patchSubscription = session.Subscribe(OnPatchCommitted);
        path.Add(new CanvasBreadcrumb(session.GraphId, "Patch"));
        if (layouts is not null)
            foreach (var layout in layouts.Where(layout => session.Snapshot.FindGraph(layout.GraphId) is not null))
                canvases.Add(layout.GraphId, new CanvasStateService(session, layout.GraphId, placement, layout));
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
            Visualizations = snapshot.Visualizations.Select(pair => new SavedNodeVisualization(pair.Key, pair.Value)).ToArray(),
            ExpandedVisualizationNodeId = snapshot.ExpandedVisualizationNodeId,
            Notes = snapshot.Notes.ToArray()
        };

    private static bool TryFindPath(GraphSnapshot graph, Guid target, out List<CanvasBreadcrumb> path)
    {
        path = [];
        if (graph.Id == target) return true;
        foreach (var node in graph.Nodes)
            if (node.Composite is { } composite && TryFindPath(composite.Graph, target, out var children))
            {
                path.Add(new CanvasBreadcrumb(composite.Graph.Id, node.Name));
                path.AddRange(children);
                return true;
            }
        return false;
    }

    public ICanvasStateService ActiveCanvas { get; private set; } = default!;
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
        path.Add(new CanvasBreadcrumb(composite.Graph.Id, node.Name));
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

    private void Activate(Guid graphId)
    {
        activeSubscription?.Dispose();
        IsFirstVisit = !canvases.TryGetValue(graphId, out var canvas);
        if (canvas is null)
        {
            removedLayouts.Remove(graphId, out var restored);
            if (restored is not null) IsFirstVisit = false;
            canvas = new CanvasStateService(session, graphId, placement, restored);
            canvases.Add(graphId, canvas);
        }
        ActiveCanvas = canvas;
        activeSubscription = canvas.Subscribe(_ => Publish());
    }

    private void OnPatchCommitted(GraphChangeSet change)
    {
        // If an ancestor was deleted, navigate back to the closest surviving
        // canvas and dispose cached projections for the removed subtree.
        var removed = canvases.Keys.Where(id => change.Snapshot.FindGraph(id) is null).ToArray();
        if (removed.Length == 0) return;
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
            canvases[id].Dispose();
            canvases.Remove(id);
        }
        while (removedLayouts.Count > 100) removedLayouts.Remove(removedLayouts.Keys.First());
        // The surviving active projection receives this same patch commit next
        // and publishes the reconciled state (including the truncated path).
    }

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
        foreach (var canvas in canvases.Values) canvas.Dispose();
        canvases.Clear();
        removedLayouts.Clear();
        changes.Clear();
    }
}
