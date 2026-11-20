namespace Orbit.Presentation.Canvas;

/// <summary>One graph's presentation state and local editing operations.</summary>
public interface ICanvasStateService : IDisposable
{
    CanvasStateSnapshot Snapshot { get; }
    IDisposable Subscribe(Action<CanvasStateChange> handler);
    CanvasChangeResult MoveNode(Guid nodeId, CanvasPoint worldPosition);
    CanvasChangeResult MoveNodeByScreenDelta(Guid nodeId, CanvasPoint screenDelta);
    CanvasChangeResult SelectNode(Guid nodeId, bool additive = false);
    CanvasChangeResult SelectConnection(Guid connectionId, bool additive = false);
    CanvasChangeResult ClearSelection();
    CanvasChangeResult PanBy(CanvasPoint screenDelta);
    CanvasChangeResult ZoomAt(double zoom, CanvasPoint screenAnchor);
    CanvasChangeResult ResetViewport();
    CanvasChangeResult FitToBounds(CanvasBounds bounds, double width, double height, double padding);
}
