namespace Orbit.Presentation.Canvas;

/// <summary>Cubic Bézier geometry in world space. Both ports and paths use the same viewport.</summary>
public readonly record struct BezierConnection(CanvasPoint Start, CanvasPoint Control1, CanvasPoint Control2, CanvasPoint End)
{
    public static BezierConnection Between(CanvasPoint start, CanvasPoint end)
    {
        start.RequireFinite(nameof(start));
        end.RequireFinite(nameof(end));
        // Keep the exit/entry tangent horizontal, including backward-facing edges.
        var distance = Math.Max(40, Math.Abs(end.X - start.X) * 0.5);
        return new(start, new CanvasPoint(start.X + distance, start.Y),
            new CanvasPoint(end.X - distance, end.Y), end);
    }
}
