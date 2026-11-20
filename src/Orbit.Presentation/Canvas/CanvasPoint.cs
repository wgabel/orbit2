namespace Orbit.Presentation.Canvas;

/// <summary>A logical CSS-pixel coordinate. The viewport converts world to screen space.</summary>
public readonly record struct CanvasPoint(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
    public static CanvasPoint Zero => new(0, 0);

    public CanvasPoint Add(CanvasPoint other) => new(X + other.X, Y + other.Y);
    public CanvasPoint Subtract(CanvasPoint other) => new(X - other.X, Y - other.Y);
    public CanvasPoint Scale(double factor) => new(X * factor, Y * factor);

    internal CanvasPoint RequireFinite(string parameterName)
    {
        if (!IsFinite)
            throw new ArgumentOutOfRangeException(parameterName, this, "Canvas coordinates must be finite.");
        return this;
    }
}
