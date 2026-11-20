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
}
