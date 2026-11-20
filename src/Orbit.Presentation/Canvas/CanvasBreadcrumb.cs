namespace Orbit.Presentation.Canvas;

/// <summary>One level in the current canvas path. Graph IDs, not display names, identify levels.</summary>
public sealed record CanvasBreadcrumb(Guid GraphId, string Name);
