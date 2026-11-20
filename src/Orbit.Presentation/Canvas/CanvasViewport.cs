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
}
