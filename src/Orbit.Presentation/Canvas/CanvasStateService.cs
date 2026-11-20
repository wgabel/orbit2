using System.Collections.ObjectModel;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Notifications;

namespace Orbit.Presentation.Canvas;

/// <summary>
/// One canvas projection of a session's graph. Owns only layout, selection and
/// viewport state. Use on the presentation dispatcher/UI thread, including graph
/// commands observed by this projection; immutable snapshots can be read elsewhere.
/// </summary>
public sealed class CanvasStateService : ICanvasStateService
{
    private readonly INodePlacementPolicy placement;
    private readonly NotificationChannel<CanvasStateChange> notifications = new();
    private readonly IDisposable graphSubscription;
    private readonly Queue<CanvasStateChange> pendingNotifications = new();
    private CanvasStateSnapshot snapshot;
    private bool publishing;
    private bool disposed;

    public CanvasStateService(IPatchSession session, INodePlacementPolicy? placement = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.placement = placement ?? new GridNodePlacementPolicy();
        var graph = session.Snapshot;
        var positions = new Dictionary<Guid, CanvasPoint>();
        foreach (var node in graph.Nodes)
            positions.Add(node.Id, PlaceNode(positions));

        snapshot = new CanvasStateSnapshot(graph, 0, CanvasViewport.Initial,
            Freeze(positions), Array.Empty<Guid>(), Array.Empty<Guid>());
        graphSubscription = session.Subscribe(OnGraphChanged, graph.Id);
    }

    public CanvasStateSnapshot Snapshot => snapshot;

    public IDisposable Subscribe(Action<CanvasStateChange> handler)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return notifications.Subscribe(handler);
    }

    public CanvasChangeResult MoveNode(Guid nodeId, CanvasPoint worldPosition)
    {
        EnsureEditable();
        var previous = GetPosition(nodeId);
        var position = worldPosition.RequireFinite(nameof(worldPosition));
        if (position == previous) return NoChange();

        var positions = new Dictionary<Guid, CanvasPoint>(snapshot.NodePositions) { [nodeId] = position };
        return CommitLayout(CanvasChangeKind.NodeMoved, snapshot with { NodePositions = Freeze(positions) });
    }

    public CanvasChangeResult MoveNodeByScreenDelta(Guid nodeId, CanvasPoint screenDelta)
    {
        EnsureEditable();
        return MoveNode(nodeId, GetPosition(nodeId).Add(snapshot.Viewport.ScreenDeltaToWorld(screenDelta)));
    }

    public CanvasChangeResult SelectNode(Guid nodeId, bool additive = false)
    {
        EnsureEditable();
        GetPosition(nodeId);
        return UpdateSelection(nodeId, additive, selectNode: true);
    }

    public CanvasChangeResult SelectConnection(Guid connectionId, bool additive = false)
    {
        EnsureEditable();
        if (!snapshot.Graph.Connections.Any(connection => connection.Id == connectionId))
            throw new KeyNotFoundException($"Connection {connectionId} does not exist in this canvas.");
        return UpdateSelection(connectionId, additive, selectNode: false);
    }

    public CanvasChangeResult ClearSelection()
    {
        EnsureEditable();
        return CommitSelection(Array.Empty<Guid>(), Array.Empty<Guid>());
    }

    public CanvasChangeResult PanBy(CanvasPoint screenDelta)
    {
        EnsureEditable();
        return CommitViewport(snapshot.Viewport.PanBy(screenDelta));
    }

    public CanvasChangeResult ZoomAt(double zoom, CanvasPoint screenAnchor)
    {
        EnsureEditable();
        return CommitViewport(snapshot.Viewport.ZoomAt(zoom, screenAnchor));
    }

    public CanvasChangeResult ResetViewport()
    {
        EnsureEditable();
        return CommitViewport(CanvasViewport.Initial);
    }

    private void OnGraphChanged(GraphChangeSet change)
    {
        if (disposed || change.GraphId != snapshot.GraphId || change.Revision <= snapshot.GraphRevision)
            return;

        // Reconcile from the final graph, not individual edge/node events. Retain
        // every surviving layout and selection and publish just one canvas update.
        var graph = change.Snapshot;
        var nodeIds = graph.Nodes.Select(node => node.Id).ToHashSet();
        var connectionIds = graph.Connections.Select(connection => connection.Id).ToHashSet();
        var positions = snapshot.NodePositions
            .Where(pair => nodeIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var node in graph.Nodes)
            if (!positions.ContainsKey(node.Id))
                positions.Add(node.Id, PlaceNode(positions));

        var selectedNodes = snapshot.SelectedNodeIds.Where(nodeIds.Contains).ToArray();
        var selectedConnections = snapshot.SelectedConnectionIds.Where(connectionIds.Contains).ToArray();
        var layoutChanged = !positions.Keys.ToHashSet().SetEquals(snapshot.NodePositions.Keys) ||
            selectedNodes.Length != snapshot.SelectedNodeIds.Count ||
            selectedConnections.Length != snapshot.SelectedConnectionIds.Count;

        snapshot = snapshot with
        {
            Graph = graph,
            LayoutRevision = layoutChanged ? checked(snapshot.LayoutRevision + 1) : snapshot.LayoutRevision,
            NodePositions = Freeze(positions),
            SelectedNodeIds = Array.AsReadOnly(selectedNodes),
            SelectedConnectionIds = Array.AsReadOnly(selectedConnections)
        };

        var errors = Publish(new CanvasStateChange(CanvasChangeKind.GraphCommitted, snapshot));
        if (errors.Count > 0)
            throw new AggregateException("Canvas subscriber failures after graph commit.", errors);
    }

    private CanvasChangeResult UpdateSelection(Guid id, bool additive, bool selectNode)
    {
        var nodes = additive ? snapshot.SelectedNodeIds.ToHashSet() : new HashSet<Guid>();
        var connections = additive ? snapshot.SelectedConnectionIds.ToHashSet() : new HashSet<Guid>();
        var target = selectNode ? nodes : connections;
        if (!target.Add(id) && additive)
            target.Remove(id);
        return CommitSelection(nodes.Order().ToArray(), connections.Order().ToArray());
    }

    private CanvasChangeResult CommitSelection(Guid[] nodes, Guid[] connections)
    {
        if (snapshot.SelectedNodeIds.SequenceEqual(nodes) &&
            snapshot.SelectedConnectionIds.SequenceEqual(connections))
            return NoChange();
        return CommitLayout(CanvasChangeKind.SelectionChanged, snapshot with
        {
            SelectedNodeIds = Array.AsReadOnly(nodes),
            SelectedConnectionIds = Array.AsReadOnly(connections)
        });
    }

    private CanvasChangeResult CommitViewport(CanvasViewport viewport) =>
        viewport == snapshot.Viewport ? NoChange()
            : CommitLayout(CanvasChangeKind.ViewportChanged, snapshot with { Viewport = viewport });

    private CanvasChangeResult CommitLayout(CanvasChangeKind kind, CanvasStateSnapshot state)
    {
        snapshot = state with { LayoutRevision = checked(snapshot.LayoutRevision + 1) };
        var change = new CanvasStateChange(kind, snapshot);
        return new CanvasChangeResult(change, Publish(change));
    }

    private IReadOnlyList<Exception> Publish(CanvasStateChange change)
    {
        pendingNotifications.Enqueue(change);
        // If a graph commits while a layout observer is running, queue its
        // projection notification after the current one rather than reorder it.
        if (publishing) return Array.Empty<Exception>();

        publishing = true;
        var errors = new List<Exception>();
        try
        {
            while (pendingNotifications.TryDequeue(out var notification))
                errors.AddRange(notifications.Publish(notification));
            return errors.AsReadOnly();
        }
        finally
        {
            publishing = false;
        }
    }

    private CanvasPoint GetPosition(Guid nodeId) =>
        snapshot.NodePositions.TryGetValue(nodeId, out var position)
            ? position : throw new KeyNotFoundException($"Node {nodeId} does not exist in this canvas.");

    private CanvasPoint PlaceNode(Dictionary<Guid, CanvasPoint> positions) =>
        placement.FindPosition(positions.Values).RequireFinite("placement");

    private static IReadOnlyDictionary<Guid, CanvasPoint> Freeze(Dictionary<Guid, CanvasPoint> positions) =>
        new ReadOnlyDictionary<Guid, CanvasPoint>(positions);

    private static CanvasChangeResult NoChange() => new(null, Array.Empty<Exception>());

    private void EnsureEditable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (publishing)
            throw new InvalidOperationException("Canvas layout cannot be edited during a canvas notification.");
    }

    public void Dispose()
    {
        if (disposed) return;
        if (publishing)
            throw new InvalidOperationException("Canvas cannot be disposed during a canvas notification.");
        disposed = true;
        graphSubscription.Dispose();
        notifications.Clear();
    }
}
