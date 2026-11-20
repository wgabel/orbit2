namespace Orbit.UI.Components.Canvas;

/// <summary>DOM measurements in world pixels, obtained from the reusable canvas adapter.</summary>
public sealed record CanvasMeasurement(double Width, double Height,
    PortAnchorMeasurement[] Ports, NodeBoundsMeasurement[] Nodes);
