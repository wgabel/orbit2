using Orbit.Domain.Ports;

namespace Orbit.Presentation.Canvas;

/// <summary>Screen-space aiming only. A selected candidate is a preview, never a graph edit.</summary>
public sealed record WireTarget(PortAddress Address, CanvasPoint Point);
public sealed record WireAim(CanvasPoint Point, WireTarget? Target);

public static class TouchWireAssist
{
    public const double Offset = 60;
    public const double SnapRadius = 24;
    public const double ReleaseRadius = 36;
    public const double SwitchAdvantage = 4;
    public const double DragThreshold = 6;

    public static bool HasMoved(CanvasPoint start, CanvasPoint finger) => Distance(start, finger) >= DragThreshold;

    public static bool Contains(CanvasBounds bounds, CanvasPoint point) =>
        point.X >= bounds.Left && point.X <= bounds.Right &&
        point.Y >= bounds.Top && point.Y <= bounds.Bottom;

    public static WireAim Resolve(CanvasPoint finger, CanvasBounds bounds,
        IReadOnlyList<WireTarget> targets, WireTarget? previous)
    {
        // Ease the offset away at the bottom edge so even the lowest port is reachable.
        var offset = Math.Clamp(bounds.Bottom - finger.Y, 0, Offset);
        var aim = new CanvasPoint(Math.Clamp(finger.X, bounds.Left, bounds.Right),
            Math.Clamp(finger.Y - offset, bounds.Top, bounds.Bottom));
        if (!Contains(bounds, finger)) return new(aim, null);

        WireTarget? nearest = null;
        var nearestDistance = SnapRadius;
        foreach (var target in targets)
        {
            var distance = Distance(aim, target.Point);
            if (distance <= nearestDistance && (nearest is null || distance < nearestDistance))
            {
                nearest = target;
                nearestDistance = distance;
            }
        }
        // Hysteresis prevents flicker without locking out closely spaced inputs.
        // A meaningfully closer challenger, or an exact aim, can replace the lock.
        if (previous is not null && targets.Contains(previous))
        {
            var previousDistance = Distance(aim, previous.Point);
            if (previousDistance <= ReleaseRadius &&
                (nearest is null || nearest == previous ||
                 (nearestDistance > 2 && nearestDistance + SwitchAdvantage >= previousDistance)))
                nearest = previous;
        }
        return new(nearest?.Point ?? aim, nearest);
    }

    private static double Distance(CanvasPoint a, CanvasPoint b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
