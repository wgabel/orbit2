namespace Orbit.Presentation.Canvas;

/// <summary>Recognize only completed, nearby taps on the empty canvas.</summary>
public sealed class CanvasFitTaps
{
    private const long SequenceMilliseconds = 600;
    private const double Radius = 24;
    private long firstTime;
    private CanvasPoint firstPoint;
    private string? pointerType;
    private int count;

    public bool Register(long time, CanvasPoint point, string type)
    {
        var delta = point.Subtract(firstPoint);
        if (count == 0 || time - firstTime > SequenceMilliseconds ||
            delta.X * delta.X + delta.Y * delta.Y > Radius * Radius || type != pointerType)
        {
            firstTime = time;
            firstPoint = point;
            pointerType = type;
            count = 0;
        }
        if (++count != 3) return false;
        Reset();
        return true;
    }

    public void Reset() => count = 0;
}
