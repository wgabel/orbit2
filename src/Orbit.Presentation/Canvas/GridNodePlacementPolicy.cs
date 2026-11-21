namespace Orbit.Presentation.Canvas;

/// <summary>Places new nodes in a predictable three-column world-space grid.
/// Spacing is a layout policy, not a graph property or measured node size.</summary>
public sealed class GridNodePlacementPolicy : INodePlacementPolicy
{
    private readonly double columnSpacing;
    private readonly double rowSpacing;
    private readonly int columns;

    public GridNodePlacementPolicy(double columnSpacing = 320, double rowSpacing = 220, int columns = 3)
    {
        if (!double.IsFinite(columnSpacing) || columnSpacing <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnSpacing));
        if (!double.IsFinite(rowSpacing) || rowSpacing <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowSpacing));
        if (columns < 1)
            throw new ArgumentOutOfRangeException(nameof(columns));

        this.columnSpacing = columnSpacing;
        this.rowSpacing = rowSpacing;
        this.columns = columns;
    }

    public CanvasPoint FindPosition(IEnumerable<CanvasPoint> occupiedPositions)
    {
        ArgumentNullException.ThrowIfNull(occupiedPositions);
        var occupied = occupiedPositions.Select(point => point.RequireFinite(nameof(occupiedPositions))).ToArray();

        for (long slot = 0; ; slot++)
        {
            var candidate = new CanvasPoint(slot % columns * columnSpacing, slot / columns * rowSpacing)
                .RequireFinite("placement");
            if (!occupied.Any(point =>
                Math.Abs(point.X - candidate.X) < columnSpacing &&
                Math.Abs(point.Y - candidate.Y) < rowSpacing))
                return candidate;
        }
    }

    /// <summary>Use the grid slot as a conservative footprint for a new, not-yet-rendered frame.
    /// Search the visible rectangle for space, preferring its center. Even if the view is
    /// crowded or smaller than one slot, the new frame's upper-left corner stays visible.</summary>
    public CanvasPoint FindVisiblePosition(CanvasBounds visibleBounds, IEnumerable<CanvasBounds> occupiedBounds)
    {
        if (!visibleBounds.IsValid) throw new ArgumentOutOfRangeException(nameof(visibleBounds));
        ArgumentNullException.ThrowIfNull(occupiedBounds);
        var occupied = occupiedBounds.Where(bounds => bounds.IsValid && OverlapArea(bounds, visibleBounds) > 0).ToArray();
        var width = Math.Min(columnSpacing, visibleBounds.Width);
        var height = Math.Min(rowSpacing, visibleBounds.Height);
        var availableX = visibleBounds.Width - width;
        var availableY = visibleBounds.Height - height;
        var center = new CanvasPoint(visibleBounds.Left + availableX / 2, visibleBounds.Top + availableY / 2);
        var best = center;
        var bestOverlap = double.PositiveInfinity;
        var bestDistance = double.PositiveInfinity;

        // A bounded search makes placement predictable even in large patches.
        const int subdivisions = 8;
        for (var row = 0; row <= subdivisions; row++)
        for (var column = 0; column <= subdivisions; column++)
        {
            var candidate = new CanvasPoint(visibleBounds.Left + availableX * column / subdivisions,
                visibleBounds.Top + availableY * row / subdivisions);
            var bounds = new CanvasBounds(candidate.X, candidate.Y, candidate.X + width, candidate.Y + height);
            var overlap = occupied.Sum(node => OverlapArea(node, bounds));
            var distance = Math.Pow(candidate.X - center.X, 2) + Math.Pow(candidate.Y - center.Y, 2);
            if (overlap < bestOverlap || overlap == bestOverlap && distance < bestDistance)
            {
                best = candidate;
                bestOverlap = overlap;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static double OverlapArea(CanvasBounds first, CanvasBounds second) =>
        Math.Max(0, Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left)) *
        Math.Max(0, Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top));
}
