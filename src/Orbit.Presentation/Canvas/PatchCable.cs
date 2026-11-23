namespace Orbit.Presentation.Canvas;

/// <summary>
/// World-space patch cable with short, straight same-color sleeves at each port.
/// A single cubic Bézier between the sleeves provides a continuous, softly sagging cable
/// without a constrained central join or an extra midpoint anchor.
/// </summary>
public readonly record struct PatchCable(
    CanvasPoint Source,
    CanvasPoint Exit,
    BezierConnection Flexible,
    CanvasPoint Entry,
    CanvasPoint Target)
{
    public const double MaximumRigidLength = 20;
    private const double MaximumSag = 24;

    /// <summary>Sample the flexible cable for badges; use the actual port centers at the ends.</summary>
    public CanvasPoint At(double t)
    {
        if (!double.IsFinite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
        if (t == 0) return Source;
        if (t == 1) return Target;
        return Flexible.At(t);
    }

    public static PatchCable Between(CanvasPoint source, CanvasPoint target)
    {
        source.RequireFinite(nameof(source));
        target.RequireFinite(nameof(target));

        var dx = target.X - source.X;
        var dy = target.Y - source.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        // Keep a short horizontal sleeve at each port without overlapping close
        // forward-facing connections. Backward connections can loop around naturally.
        var rigidLength = Math.Min(MaximumRigidLength, distance * 0.25);
        if (dx > 0)
            rigidLength = Math.Min(rigidLength, dx / 3);

        var exit = new CanvasPoint(source.X + rigidLength, source.Y);
        var entry = new CanvasPoint(target.X - rigidLength, target.Y);
        var span = Math.Abs(entry.X - exit.X);

        // Only the two endpoint handles shape the soft part. The small downward
        // offset produces a natural sag without a forced point in the middle.
        // Exactly horizontal tangents at BOTH ends of a level cubic would preclude
        // any sag, so the handle slopes are deliberately shallow instead.
        var handle = Math.Min(120, Math.Min(Math.Max(20, span * 0.35), distance * 0.5));
        var sag = Math.Min(MaximumSag, span * 0.10);
        var sagHandleOffset = sag * (4d / 3d);
        var flexible = new BezierConnection(
            exit,
            new CanvasPoint(exit.X + handle, exit.Y + sagHandleOffset),
            new CanvasPoint(entry.X - handle, entry.Y + sagHandleOffset),
            entry);

        return new PatchCable(source, exit, flexible, entry, target);
    }
}
