using System.Collections.ObjectModel;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Notifications;
using Orbit.Domain.Nodes.Composites;
using Orbit.Application.Persistence;

namespace Orbit.Presentation.Canvas;

/// <summary>
/// One canvas projection of a session's graph. Owns only layout, selection and
/// viewport state. Use on the presentation dispatcher/UI thread, including graph
/// commands observed by this projection; immutable snapshots can be read elsewhere.
/// </summary>
public sealed partial class CanvasStateService : ICanvasStateService
{
    private readonly INodePlacementPolicy placement;
    private readonly NotificationChannel<CanvasStateChange> notifications = new();
    private readonly IDisposable graphSubscription;
    private readonly Queue<CanvasStateChange> pendingNotifications = new();
    private readonly Dictionary<Guid, CanvasPoint> removedPositions = [];
    private readonly Dictionary<Guid, NodeVisualizationSettings> removedVisualizations = [];
    private NodePlacementReservation? nextPlacement;
    private CanvasStateSnapshot snapshot;
    private bool publishing;
    private bool disposed;

    public CanvasStateService(IPatchSession session, INodePlacementPolicy? placement = null)
        : this(session, session?.GraphId ?? throw new ArgumentNullException(nameof(session)), placement)
    {
    }

    public CanvasStateService(IPatchSession session, Guid graphId, INodePlacementPolicy? placement = null, SavedCanvas? saved = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.placement = placement ?? new GridNodePlacementPolicy();
        var graph = session.Snapshot.GetGraph(graphId);
        var positions = new Dictionary<Guid, CanvasPoint>();
        var viewport = saved is null ? CanvasViewport.Initial : new CanvasViewport(new CanvasPoint(saved.PanX, saved.PanY), saved.Zoom);
        if (saved is not null)
            foreach (var position in saved.Positions.Where(position => graph.Nodes.Any(node => node.Id == position.NodeId)))
                positions.Add(position.NodeId, new CanvasPoint(position.X, position.Y).RequireFinite(nameof(saved)));
        // Place the receiving rack at the end of a new child layout, so the
        // default signal flow reads left to right. Existing positions never move.
        foreach (var node in graph.Nodes.OrderBy(node => node.TypeKey == OutputRack.NodeType ? 1 : 0))
            if (!positions.ContainsKey(node.Id)) positions.Add(node.Id, node.TypeKey == OutputRack.NodeType
                ? this.placement.FindOutputRackPosition(positions.Values).RequireFinite("placement") : PlaceNode(positions));

        var visualizations = new Dictionary<Guid, NodeVisualizationSettings>();
        foreach (var preference in saved?.Visualizations ?? [])
            if (preference is not null && graph.Nodes.FirstOrDefault(node => node.Id == preference.NodeId) is { } node &&
                NodeVisualizationCapabilities.EligiblePorts(node).Any())
                visualizations[node.Id] = NodeVisualizationCapabilities.Normalize(node, preference.Settings);
        var notes = saved is null ? [] : saved.Notes;
        SavedNote.Validate(notes);
        if (notes.Length > 0 && saved!.GraphId != graphId)
            throw new ArgumentException("Canvas notes belong to a different graph.", nameof(saved));
        if (saved is not null)
        {
            if (!Enum.IsDefined(saved.AnnotationVisibility))
                throw new ArgumentException("Invalid annotation visibility preference.", nameof(saved));
            DescriptionLimits.Validate(saved, graph.Nodes.Select(node => node.Id), graph.Connections.Select(edge => edge.Id));
            SavedVisualGroup.Validate(saved.VisualGroups, graph.Nodes.Select(node => node.Id), notes.Select(note => note.Id));
            if (saved.GraphId != graphId && saved.VisualGroups.Length > 0)
                throw new ArgumentException("Visual groups belong to a different graph.", nameof(saved));
            if (saved.GraphId != graphId && (saved.NodeDescriptions.Length > 0 || saved.ConnectionDescriptions.Length > 0))
                throw new ArgumentException("Descriptions belong to a different graph.", nameof(saved));
        }
        foreach (var group in saved?.VisualGroups ?? []) groupDefinitions.Add(group.Id, FreezeGroup(group));
        snapshot = new CanvasStateSnapshot(graph, 0, viewport,
            Freeze(positions), Array.Empty<Guid>(), Array.Empty<Guid>())
        {
            ConnectionLabels = saved?.ConnectionLabels ?? ConnectionLabelMode.All,
            AnnotationVisibility = saved?.AnnotationVisibility ?? AnnotationVisibility.Show,
            ZoomAnnotationDetail = AnnotationDetailPolicy.Initial(viewport.Zoom),
            Visualizations = FreezeVisualizations(visualizations),
            ExpandedVisualizationNodeId = ValidExpandedNode(graph, saved?.ExpandedVisualizationNodeId),
            Notes = Array.AsReadOnly(notes.ToArray()),
            NodeDescriptions = FreezeDescriptions((saved?.NodeDescriptions ?? []).Where(value => !value.IsEmpty).ToDictionary(value => value.NodeId)),
            ConnectionDescriptions = FreezeDescriptions((saved?.ConnectionDescriptions ?? []).Where(value => !value.IsEmpty).ToDictionary(value => value.ConnectionId)),
            VisualGroups = Array.AsReadOnly(groupDefinitions.Values.ToArray())
        };
        // Observe patch commits, including edits of descendants and deletion of
        // the composite that owns this canvas. Each local projection has its own layout.
        graphSubscription = session.Subscribe(OnGraphChanged);
    }

    public CanvasStateSnapshot Snapshot => snapshot;

    public IDisposable Subscribe(Action<CanvasStateChange> handler)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return notifications.Subscribe(handler);
    }

    /// <summary>Reserve one interactive addition's layout without coupling the graph command
    /// to viewport geometry. Dispose the reservation even if the command is rejected.</summary>
    public IDisposable ReserveVisibleNodePlacement(CanvasBounds visibleBounds, IEnumerable<CanvasBounds> occupiedBounds)
    {
        EnsureEditable();
        if (nextPlacement is not null) throw new InvalidOperationException("A node placement is already reserved.");
        return nextPlacement = new NodePlacementReservation(this,
            placement.FindVisiblePosition(visibleBounds, occupiedBounds.Concat(snapshot.Notes.Select(NoteBounds))).RequireFinite("placement"));
    }

    public CanvasChangeResult SetConnectionLabels(ConnectionLabelMode mode)
    {
        EnsureEditable();
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        return snapshot.ConnectionLabels == mode ? NoChange() :
            CommitLayout(CanvasChangeKind.ConnectionLabelsChanged, snapshot with { ConnectionLabels = mode });
    }

    public CanvasChangeResult SetAnnotationVisibility(AnnotationVisibility visibility)
    {
        EnsureEditable();
        if (!Enum.IsDefined(visibility)) throw new ArgumentOutOfRangeException(nameof(visibility));
        if (visibility == snapshot.AnnotationVisibility) return NoChange();
        return CommitLayout(CanvasChangeKind.AnnotationVisibilityChanged, snapshot with
        {
            AnnotationVisibility = visibility,
            SelectedNoteId = null,
            SelectedGroupId = null
        });
    }

    public CanvasChangeResult SetVisualization(Guid nodeId, NodeVisualizationSettings settings)
    {
        EnsureEditable();
        var node = snapshot.Graph.GetNode(nodeId);
        if (!NodeVisualizationCapabilities.EligiblePorts(node).Any()) return NoChange();
        var normalized = NodeVisualizationCapabilities.Normalize(node, settings);
        if (normalized == snapshot.VisualizationFor(nodeId)) return NoChange();
        var preferences = new Dictionary<Guid, NodeVisualizationSettings>(snapshot.Visualizations) { [nodeId] = normalized };
        return CommitLayout(CanvasChangeKind.VisualizationChanged, snapshot with { Visualizations = FreezeVisualizations(preferences) });
    }

    public CanvasChangeResult SetExpandedVisualization(Guid? nodeId)
    {
        EnsureEditable();
        var valid = ValidExpandedNode(snapshot.Graph, nodeId);
        if (valid == snapshot.ExpandedVisualizationNodeId) return NoChange();
        return CommitLayout(CanvasChangeKind.VisualizationChanged, snapshot with { ExpandedVisualizationNodeId = valid });
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

    /// <summary>Replace or union a module selection in one notification. Clears non-node selection.</summary>
    public CanvasChangeResult SelectModuleNodes(IEnumerable<Guid> nodeIds, bool additive = false)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(nodeIds);
        var requested = nodeIds.ToHashSet();
        foreach (var id in requested) GetPosition(id); // reject stale/foreign IDs before any state change
        var eligible = ModuleSelection.EligibleIds(snapshot.Graph).ToHashSet();
        if (additive) requested.UnionWith(snapshot.SelectedNodeIds);
        requested.IntersectWith(eligible);
        return CommitSelection(requested.Order().ToArray(), []);
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

    public CanvasChangeResult FitToBounds(CanvasBounds bounds, double width, double height, double padding)
    {
        EnsureEditable();
        return CommitViewport(CanvasViewport.Fit(bounds, width, height, padding));
    }

    private void OnGraphChanged(GraphChangeSet change)
    {
        if (disposed || !snapshot.IsAvailable || change.Revision <= snapshot.GraphRevision)
            return;

        var graph = change.Snapshot.FindGraph(snapshot.GraphId);
        if (graph is null)
        {
            // A removed child canvas is no longer editable. Keep its final
            // projection for observers to navigate away without stale commands.
            snapshot = snapshot with { IsAvailable = false };
            ReportSubscriberErrors(Publish(new CanvasStateChange(CanvasChangeKind.GraphRemoved, snapshot)));
            return;
        }

        // Reconcile from the final graph, not individual edge/node events. Retain
        // every surviving layout and selection and publish just one canvas update.
        var nodeIds = graph.Nodes.Select(node => node.Id).ToHashSet();
        var connectionIds = graph.Connections.Select(connection => connection.Id).ToHashSet();
        var positions = snapshot.NodePositions
            .Where(pair => nodeIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var pair in snapshot.NodePositions.Where(pair => !nodeIds.Contains(pair.Key)))
            removedPositions[pair.Key] = pair.Value;
        while (removedPositions.Count > 512) removedPositions.Remove(removedPositions.Keys.First());
        foreach (var node in graph.Nodes)
            if (!positions.ContainsKey(node.Id))
            {
                if (removedPositions.Remove(node.Id, out var restored))
                    positions.Add(node.Id, restored);
                else if (nextPlacement is { } reserved && change.Events.OfType<NodeAdded>()
                    .Any(added => added.GraphId == graph.Id && added.Node.Id == node.Id))
                {
                    positions.Add(node.Id, reserved.Position);
                    nextPlacement = null;
                }
                else positions.Add(node.Id, PlaceNode(positions));
            }

        var selectedNodes = snapshot.SelectedNodeIds.Where(nodeIds.Contains).ToArray();
        var selectedConnections = snapshot.SelectedConnectionIds.Where(connectionIds.Contains).ToArray();
        foreach (var pair in snapshot.Visualizations.Where(pair => !nodeIds.Contains(pair.Key)))
            removedVisualizations[pair.Key] = pair.Value;
        while (removedVisualizations.Count > 512) removedVisualizations.Remove(removedVisualizations.Keys.First());
        var visualizations = new Dictionary<Guid, NodeVisualizationSettings>();
        foreach (var node in graph.Nodes)
            if (snapshot.Visualizations.TryGetValue(node.Id, out var settings) || removedVisualizations.Remove(node.Id, out settings))
                visualizations[node.Id] = NodeVisualizationCapabilities.Normalize(node, settings);
        var expanded = ValidExpandedNode(graph, snapshot.ExpandedVisualizationNodeId);
        var nodeDescriptions = ReconcileDescriptions(snapshot.NodeDescriptions, removedNodeDescriptions, nodeIds);
        var connectionDescriptions = ReconcileDescriptions(snapshot.ConnectionDescriptions, removedConnectionDescriptions, connectionIds);
        var layoutChanged = !positions.Keys.ToHashSet().SetEquals(snapshot.NodePositions.Keys) ||
            selectedNodes.Length != snapshot.SelectedNodeIds.Count ||
            selectedConnections.Length != snapshot.SelectedConnectionIds.Count ||
            expanded != snapshot.ExpandedVisualizationNodeId ||
            !visualizations.OrderBy(pair => pair.Key).SequenceEqual(snapshot.Visualizations.OrderBy(pair => pair.Key)) ||
            !ReferenceEquals(nodeDescriptions, snapshot.NodeDescriptions) || !ReferenceEquals(connectionDescriptions, snapshot.ConnectionDescriptions);

        snapshot = ProjectGroups(snapshot with
        {
            Graph = graph,
            LayoutRevision = layoutChanged ? checked(snapshot.LayoutRevision + 1) : snapshot.LayoutRevision,
            NodePositions = Freeze(positions),
            SelectedNodeIds = Array.AsReadOnly(selectedNodes),
            SelectedConnectionIds = Array.AsReadOnly(selectedConnections),
            Visualizations = FreezeVisualizations(visualizations),
            ExpandedVisualizationNodeId = expanded,
            NodeDescriptions = nodeDescriptions,
            ConnectionDescriptions = connectionDescriptions
        });

        ReportSubscriberErrors(Publish(new CanvasStateChange(CanvasChangeKind.GraphCommitted, snapshot)));
    }

    private static void ReportSubscriberErrors(IReadOnlyList<Exception> errors)
    {
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
            snapshot.SelectedConnectionIds.SequenceEqual(connections) && snapshot.SelectedNoteId is null && snapshot.SelectedGroupId is null)
            return NoChange();
        return CommitLayout(CanvasChangeKind.SelectionChanged, snapshot with
        {
            SelectedNodeIds = Array.AsReadOnly(nodes),
            SelectedConnectionIds = Array.AsReadOnly(connections),
            SelectedNoteId = null,
            SelectedGroupId = null
        });
    }

    private CanvasChangeResult CommitViewport(CanvasViewport viewport) =>
        viewport == snapshot.Viewport ? NoChange()
            : CommitLayout(CanvasChangeKind.ViewportChanged, snapshot with
            {
                Viewport = viewport,
                ZoomAnnotationDetail = AnnotationDetailPolicy.Update(viewport.Zoom, snapshot.ZoomAnnotationDetail)
            });

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

    private sealed class NodePlacementReservation(CanvasStateService owner, CanvasPoint position) : IDisposable
    {
        public CanvasPoint Position { get; } = position;
        public void Dispose()
        {
            if (ReferenceEquals(owner.nextPlacement, this)) owner.nextPlacement = null;
        }
    }

    private static IReadOnlyDictionary<Guid, CanvasPoint> Freeze(Dictionary<Guid, CanvasPoint> positions) =>
        new ReadOnlyDictionary<Guid, CanvasPoint>(positions);

    private static IReadOnlyDictionary<Guid, NodeVisualizationSettings> FreezeVisualizations(Dictionary<Guid, NodeVisualizationSettings> settings) =>
        new ReadOnlyDictionary<Guid, NodeVisualizationSettings>(settings);

    private static Guid? ValidExpandedNode(GraphSnapshot graph, Guid? id) =>
        graph.Nodes.Any(node => node.Id == id && NodeVisualizationCapabilities.EligiblePorts(node).Any()) ? id : null;

    private static CanvasChangeResult NoChange() => new(null, Array.Empty<Exception>());

    private void EnsureEditable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!snapshot.IsAvailable)
            throw new InvalidOperationException("The canvas graph no longer exists in the patch.");
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
