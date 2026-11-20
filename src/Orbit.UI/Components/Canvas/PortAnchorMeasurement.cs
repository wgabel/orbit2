namespace Orbit.UI.Components.Canvas;

/// <summary>Port center relative to its node frame in unscaled world coordinates.</summary>
public sealed record PortAnchorMeasurement(Guid NodeId, Guid PortId, double X, double Y);
