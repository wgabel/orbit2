namespace Orbit.Presentation.Canvas;

using Orbit.Application.Persistence;

/// <summary>One graph's presentation state and local editing operations.</summary>
public interface ICanvasStateService : IDisposable
{
    CanvasStateSnapshot Snapshot { get; }
    IDisposable Subscribe(Action<CanvasStateChange> handler);
    IDisposable ReserveVisibleNodePlacement(CanvasBounds visibleBounds, IEnumerable<CanvasBounds> occupiedBounds);
    CanvasChangeResult SetConnectionLabels(ConnectionLabelMode mode);
    CanvasChangeResult SetAnnotationVisibility(AnnotationVisibility visibility);
    CanvasChangeResult SetVisualization(Guid nodeId, NodeVisualizationSettings settings);
    CanvasChangeResult SetExpandedVisualization(Guid? nodeId);
    CanvasChangeResult SetNodePinned(Guid nodeId, bool pinned);
    CanvasChangeResult MoveNode(Guid nodeId, CanvasPoint worldPosition);
    CanvasChangeResult MoveNodeByScreenDelta(Guid nodeId, CanvasPoint screenDelta);
    CanvasChangeResult SelectNode(Guid nodeId, bool additive = false);
    CanvasChangeResult SelectModuleNodes(IEnumerable<Guid> nodeIds, bool additive = false);
    CanvasChangeResult SelectConnection(Guid connectionId, bool additive = false);
    CanvasChangeResult ClearSelection();
    CanvasChangeResult AddNote(CanvasBounds visible, IEnumerable<CanvasBounds> occupied);
    CanvasChangeResult UpdateNote(SavedNote note);
    CanvasChangeResult SelectNote(Guid id);
    CanvasChangeResult DeleteNote(Guid id);
    CanvasChangeResult DuplicateNote(Guid id);
    CanvasChangeResult UndoAnnotation();
    CanvasChangeResult RedoAnnotation();
    CanvasChangeResult SetNodeDescription(SavedNodeDescription description);
    CanvasChangeResult SetConnectionDescription(SavedConnectionDescription description);
    CanvasChangeResult AddGroup(CanvasBounds bounds);
    CanvasChangeResult SelectGroup(Guid id);
    CanvasChangeResult UpdateGroup(SavedVisualGroup group);
    CanvasChangeResult MoveGroup(Guid id, CanvasPoint delta);
    CanvasChangeResult DeleteGroup(Guid id);
    CanvasChangeResult PanBy(CanvasPoint screenDelta);
    CanvasChangeResult ZoomAt(double zoom, CanvasPoint screenAnchor);
    CanvasChangeResult SetViewport(CanvasViewport viewport);
    CanvasChangeResult ResetViewport();
    CanvasChangeResult FitToBounds(CanvasBounds bounds, double width, double height, double padding);
}
