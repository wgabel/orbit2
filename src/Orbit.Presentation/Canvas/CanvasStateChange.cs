namespace Orbit.Presentation.Canvas;

/// <summary>A complete canvas update. The renderer reads the supplied stable snapshot.</summary>
public sealed record CanvasStateChange(CanvasChangeKind Kind, CanvasStateSnapshot Snapshot);
