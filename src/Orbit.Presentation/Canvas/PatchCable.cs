namespace Orbit.Presentation.Canvas;

/// <summary>
/// World-space patch cable with straight, outward-facing sleeves at both ports
/// and two smoothly joined cubic curves for the flexible, gently sagging span.
/// The sleeves are drawn thicker, in the same color as the rest of the cable.
/// </summary>
public readonly record struct PatchCable(
    CanvasPoint Source,
    CanvasPoint Exit,
    BezierConnection First,
    BezierConnection Second,
    CanvasPoint Entry,
    CanvasPoint Target)
{
    public const double MaximumRigidLength = 20;
    private const double MaximumSag = 24;

    /// <summary>The cable's flexible midpoint, with exact port coordinates at 0 and 1.</summary>
    public CanvasPoint At(double t)
    {
        if (!double.IsFinite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
        if (t == 0) return Source;
        if (t == 1) return Target;
        return t <= 0.5 ? First.At(t * 2) : Second.At((t - 0.5) * 2);
    }

    public static PatchCable Between(CanvasPoint source, CanvasPoint target)
    {
        source.RequireFinite(nameof(source));
        target.RequireFinite(nameof(target));

        var dx = target.X - source.X;
        var dy = target.Y - source.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        // At short forward gaps the two rigid sleeves must not meet or cross.
        // Backward connections may still loop outward naturally from each port.
        var rigidLength = Math.Min(MaximumRigidLength, distance * 0.25);
        if (dx > 0)
            rigidLength = Math.Min(rigidLength, dx / 3);

        var exit = new CanvasPoint(source.X + rigidLength, source.Y);
        var entry = new CanvasPoint(target.X - rigidLength, target.Y);
        var horizontal = entry.X - exit.X;
        var span = Math.Abs(horizontal);
        var outwardHandle = Math.Min(100, Math.Min(Math.Max(12, span * 0.30), distance * 0.30));
        var middleHandle = Math.Min(70, span * 0.18);
        var direction = horizontal >= 0 ? 1 : -1;
        var middle = new CanvasPoint((exit.X + entry.X) * 0.5,
            (exit.Y + entry.Y) * 0.5 + Math.Min(MaximumSag, span * 0.10));

        // Both endpoints and the midpoint have horizontal tangents. The control
        // points around the midpoint are collinear for a seamless soft bend.
        var first = new BezierConnection(
            exit,
            new CanvasPoint(exit.X + outwardHandle, exit.Y),
            new CanvasPoint(middle.X - direction * middleHandle, middle.Y),
            middle);
        var second = new BezierConnection(
            middle,
            new CanvasPoint(middle.X + direction * middleHandle, middle.Y),
            new CanvasPoint(entry.X - outwardHandle, entry.Y),
            entry);

        return new PatchCable(source, exit, first, second, entry, target);
    }
}
