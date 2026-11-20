namespace Orbit.Presentation.Canvas;

/// <summary>World-space bounding rectangle of measured node frames.</summary>
public readonly record struct CanvasBounds(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
    public CanvasPoint Center => new(Left + Width / 2, Top + Height / 2);
    public bool IsValid => double.IsFinite(Left) && double.IsFinite(Top) &&
        double.IsFinite(Right) && double.IsFinite(Bottom) &&
        double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
}
