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

    // An output boundary must not wrap underneath a potentially very tall node.
    public CanvasPoint FindOutputRackPosition(IEnumerable<CanvasPoint> occupiedPositions)
    {
        ArgumentNullException.ThrowIfNull(occupiedPositions);
        var occupied = occupiedPositions.Select(point => point.RequireFinite(nameof(occupiedPositions))).ToArray();
        return occupied.Length == 0 ? new CanvasPoint(0, 0) :
            new CanvasPoint(occupied.Max(point => point.X) + columnSpacing, occupied.Min(point => point.Y));
    }

    /// <summary>Use measured shared width and grid height as the footprint for a new frame.
    /// Search the visible rectangle for space, preferring its center. Even if the view is
    /// crowded or smaller than one slot, the new frame's upper-left corner stays visible.</summary>
    public CanvasPoint FindVisiblePosition(CanvasBounds visibleBounds, IEnumerable<CanvasBounds> occupiedBounds)
        => FindVisiblePositionCore(visibleBounds, occupiedBounds, null, rowSpacing);

    public CanvasPoint FindVisiblePosition(CanvasBounds visibleBounds, IEnumerable<CanvasBounds> occupiedBounds, double width, double height)
        => FindVisiblePositionCore(visibleBounds, occupiedBounds, width, height);

    private CanvasPoint FindVisiblePositionCore(CanvasBounds visibleBounds, IEnumerable<CanvasBounds> occupiedBounds, double? footprintWidth, double footprintHeight)
    {
        if (!visibleBounds.IsValid) throw new ArgumentOutOfRangeException(nameof(visibleBounds));
        ArgumentNullException.ThrowIfNull(occupiedBounds);
        var measured = occupiedBounds.Where(bounds => bounds.IsValid).ToArray();
        // Node frames share one tokenized width; use the measured width when
        // available instead of assuming the whole grid column is occupied.
        var frameWidth = footprintWidth ?? (measured.Length == 0 ? columnSpacing : measured.Max(bounds => bounds.Width));
        if (!double.IsFinite(frameWidth) || frameWidth <= 0 || !double.IsFinite(footprintHeight) || footprintHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(footprintWidth));
        var searchBounds = new CanvasBounds(visibleBounds.Left, visibleBounds.Top,
            visibleBounds.Right + frameWidth, visibleBounds.Bottom + footprintHeight);
        var occupied = measured.Where(bounds => OverlapArea(bounds, searchBounds) > 0).ToArray();
        var width = Math.Min(frameWidth, visibleBounds.Width);
        var height = Math.Min(footprintHeight, visibleBounds.Height);
        var availableX = visibleBounds.Width - width;
        var availableY = visibleBounds.Height - height;
        var center = new CanvasPoint(visibleBounds.Left + availableX / 2, visibleBounds.Top + availableY / 2);
        var best = center;
        var bestOverlap = double.PositiveInfinity;
        var bestClipping = double.PositiveInfinity;
        var bestDistance = double.PositiveInfinity;

        void Search(double verticalRange)
        {
            // A bounded search makes placement predictable even in large patches.
            // Include nearby frame edges so a narrow empty slot isn't missed
            // merely because it lies between the regular candidate samples.
            const int subdivisions = 8;
            var xs = Enumerable.Range(0, subdivisions + 1)
                .Select(column => visibleBounds.Left + availableX * column / subdivisions).ToHashSet();
            var ys = Enumerable.Range(0, subdivisions + 1)
                .Select(row => visibleBounds.Top + verticalRange * row / subdivisions).ToHashSet();
            foreach (var obstacle in occupied.OrderBy(node => Math.Pow(node.Center.X - center.X, 2) +
                         Math.Pow(node.Center.Y - center.Y, 2)).Take(12))
            {
                xs.Add(Math.Clamp(obstacle.Right, visibleBounds.Left, visibleBounds.Left + availableX));
                xs.Add(Math.Clamp(obstacle.Left - frameWidth, visibleBounds.Left, visibleBounds.Left + availableX));
                ys.Add(Math.Clamp(obstacle.Bottom, visibleBounds.Top, visibleBounds.Top + verticalRange));
                ys.Add(Math.Clamp(obstacle.Top - footprintHeight, visibleBounds.Top, visibleBounds.Top + verticalRange));
            }
            foreach (var y in ys.Order())
            foreach (var x in xs.Order())
            {
                var candidate = new CanvasPoint(x, y);
                var bounds = new CanvasBounds(candidate.X, candidate.Y, candidate.X + frameWidth, candidate.Y + footprintHeight);
                var overlap = occupied.Sum(node => OverlapArea(node, bounds));
                var clipping = Math.Max(0, frameWidth * footprintHeight - OverlapArea(bounds, visibleBounds));
                var distance = Math.Pow(candidate.X - center.X, 2) + Math.Pow(candidate.Y - center.Y, 2);
                if (overlap < bestOverlap || overlap == bestOverlap &&
                    (clipping < bestClipping || clipping == bestClipping && distance < bestDistance))
                {
                    best = candidate;
                    bestOverlap = overlap;
                    bestClipping = clipping;
                    bestDistance = distance;
                }
            }
        }

        Search(availableY);
        // If whole slots collide, allow the lower part of the new frame to
        // extend below the viewport, keeping its draggable header on screen.
        // Do not hide existing controls just to fit the new frame completely.
        if (bestOverlap > 0)
            Search(Math.Max(availableY, visibleBounds.Height - Math.Min(footprintHeight / 4, visibleBounds.Height)));
        return best;
    }

    private static double OverlapArea(CanvasBounds first, CanvasBounds second) =>
        Math.Max(0, Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left)) *
        Math.Max(0, Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top));
}
