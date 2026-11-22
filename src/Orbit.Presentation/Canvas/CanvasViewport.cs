namespace Orbit.Presentation.Canvas;

/// <summary>Immutable viewport transform shared by nodes, wires and hit testing.
/// Screen = world * zoom + pan. Pan is measured in viewport/screen pixels.</summary>
public sealed record CanvasViewport
{
    public const double MinimumZoom = 0.25;
    public const double MaximumZoom = 4.0;

    public static CanvasViewport Initial { get; } = new(CanvasPoint.Zero, 1);

    public CanvasViewport(CanvasPoint pan, double zoom)
    {
        Pan = pan.RequireFinite(nameof(pan));
        if (!double.IsFinite(zoom) || zoom < MinimumZoom || zoom > MaximumZoom)
            throw new ArgumentOutOfRangeException(nameof(zoom), "Zoom must be between 25% and 400%.");
        Zoom = zoom;
    }

    public CanvasPoint Pan { get; }
    public double Zoom { get; }
    public double ZoomPercentage => Zoom * 100;

    public CanvasPoint WorldToScreen(CanvasPoint world) =>
        world.RequireFinite(nameof(world)).Scale(Zoom).Add(Pan).RequireFinite(nameof(world));

    public CanvasPoint ScreenToWorld(CanvasPoint screen) =>
        screen.RequireFinite(nameof(screen)).Subtract(Pan).Scale(1 / Zoom).RequireFinite(nameof(screen));

    public CanvasPoint ScreenDeltaToWorld(CanvasPoint delta) =>
        delta.RequireFinite(nameof(delta)).Scale(1 / Zoom).RequireFinite(nameof(delta));

    public CanvasViewport PanBy(CanvasPoint screenDelta) =>
        new(Pan.Add(screenDelta.RequireFinite(nameof(screenDelta))), Zoom);

    /// <summary>Center measured world bounds in the viewport with a screen-space margin.</summary>
    public static CanvasViewport Fit(CanvasBounds bounds, double width, double height, double padding)
    {
        if (!bounds.IsValid || !double.IsFinite(width) || !double.IsFinite(height) ||
            !double.IsFinite(padding) || width <= 0 || height <= 0 || padding < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Fit requires finite, positive bounds and viewport.");

        var availableWidth = Math.Max(1, width - 2 * padding);
        var availableHeight = Math.Max(1, height - 2 * padding);
        var zoom = Math.Clamp(Math.Min(availableWidth / bounds.Width, availableHeight / bounds.Height),
            MinimumZoom, MaximumZoom);
        var pan = new CanvasPoint(width / 2, height / 2).Subtract(bounds.Center.Scale(zoom));
        return new CanvasViewport(pan, zoom);
    }

    /// <summary>Keep the world point under the supplied screen anchor fixed while zooming.</summary>
    public CanvasViewport ZoomAt(double requestedZoom, CanvasPoint screenAnchor)
    {
        if (!double.IsFinite(requestedZoom) || requestedZoom <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedZoom), "Zoom must be positive and finite.");

        var anchor = screenAnchor.RequireFinite(nameof(screenAnchor));
        var nextZoom = Math.Clamp(requestedZoom, MinimumZoom, MaximumZoom);
        if (nextZoom == Zoom) return this;
        var worldAnchor = ScreenToWorld(anchor);
        return new CanvasViewport(anchor.Subtract(worldAnchor.Scale(nextZoom)), nextZoom);
    }

    /// <summary>Map the previous two-finger midpoint to its new location while scaling.</summary>
    public CanvasViewport Pinch(CanvasPoint previousFirst, CanvasPoint previousSecond,
        CanvasPoint first, CanvasPoint second)
    {
        var previous = previousFirst.Add(previousSecond).Scale(0.5);
        var current = first.Add(second).Scale(0.5);
        static double Distance(CanvasPoint a, CanvasPoint b)
        {
            var delta = a.Subtract(b);
            return Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        }
        var oldDistance = Distance(previousFirst, previousSecond);
        var distance = Distance(first, second);
        var ratio = oldDistance < 1 || distance < 1 ? 1 : distance / oldDistance;
        return ZoomAt(Zoom * ratio, previous).PanBy(current.Subtract(previous));
    }
}
