namespace Orbit.Presentation.Canvas;

/// <summary>Initial placement policy used when a committed graph contains a new node.</summary>
public interface INodePlacementPolicy
{
    CanvasPoint FindPosition(IEnumerable<CanvasPoint> occupiedPositions);
    CanvasPoint FindOutputRackPosition(IEnumerable<CanvasPoint> occupiedPositions);
    CanvasPoint FindVisiblePosition(CanvasBounds visibleBounds, IEnumerable<CanvasBounds> occupiedBounds);
    CanvasPoint FindVisiblePosition(CanvasBounds visibleBounds, IEnumerable<CanvasBounds> occupiedBounds, double width, double height) =>
        FindVisiblePosition(visibleBounds, occupiedBounds);
}
