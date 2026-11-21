namespace Orbit.Presentation.Canvas;

/// <summary>Cubic Bézier geometry in world space. Both ports and paths use the same viewport.</summary>
public readonly record struct BezierConnection(CanvasPoint Start, CanvasPoint Control1, CanvasPoint Control2, CanvasPoint End)
{
    /// <summary>World-space cubic position used by badges and other cable annotations.</summary>
    public CanvasPoint At(double t)
    {
        if (!double.IsFinite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
        var u = 1 - t;
        return new CanvasPoint(
            u*u*u*Start.X + 3*u*u*t*Control1.X + 3*u*t*t*Control2.X + t*t*t*End.X,
            u*u*u*Start.Y + 3*u*u*t*Control1.Y + 3*u*t*t*Control2.Y + t*t*t*End.Y);
    }

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
