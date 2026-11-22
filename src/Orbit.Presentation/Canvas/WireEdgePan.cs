namespace Orbit.Presentation.Canvas;

/// <summary>Screen-space edge scrolling while a cable is held. No graph or DOM dependencies.</summary>
public sealed class WireEdgePan
{
    public const double EdgeSize = 50;
    public const double MaximumSpeed = 600;
    public const long DwellMilliseconds = 150;
    private (int X, int Y) direction;
    private long entered;

    public static CanvasPoint Velocity(CanvasBounds bounds, CanvasPoint pointer)
    {
        if (!bounds.IsValid || !pointer.IsFinite || !TouchWireAssist.Contains(bounds, pointer)) return CanvasPoint.Zero;
        static double Axis(double value, double low, double high)
        {
            // Keep a neutral center even on very narrow canvases.
            var edge = Math.Min(EdgeSize, (high - low) / 3);
            var lowPressure = Math.Clamp(1 - (value - low) / edge, 0, 1);
            var highPressure = Math.Clamp(1 - (high - value) / edge, 0, 1);
            return MaximumSpeed * (lowPressure * lowPressure - highPressure * highPressure);
        }
        var velocity = new CanvasPoint(Axis(pointer.X, bounds.Left, bounds.Right), Axis(pointer.Y, bounds.Top, bounds.Bottom));
        var magnitude = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
        return magnitude > MaximumSpeed ? velocity.Scale(MaximumSpeed / magnitude) : velocity;
    }

    public CanvasPoint Update(CanvasBounds bounds, CanvasPoint pointer, long now, bool snapped)
    {
        var velocity = snapped ? CanvasPoint.Zero : Velocity(bounds, pointer);
        var next = (Math.Sign(velocity.X), Math.Sign(velocity.Y));
        if (next == (0, 0)) { Reset(); return CanvasPoint.Zero; }
        if (next != direction) { direction = next; entered = now; }
        return now - entered >= DwellMilliseconds ? velocity : CanvasPoint.Zero;
    }

    public void Reset() { direction = (0, 0); entered = 0; }
}
