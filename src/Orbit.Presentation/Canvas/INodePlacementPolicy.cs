namespace Orbit.Presentation.Canvas;

/// <summary>Initial placement policy used when a committed graph contains a new node.</summary>
public interface INodePlacementPolicy
{
    CanvasPoint FindPosition(IEnumerable<CanvasPoint> occupiedPositions);
}
