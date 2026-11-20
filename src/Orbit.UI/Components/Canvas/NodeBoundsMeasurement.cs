namespace Orbit.UI.Components.Canvas;

/// <summary>Actual node frame dimensions before viewport zoom.</summary>
public sealed record NodeBoundsMeasurement(Guid NodeId, double Width, double Height);
