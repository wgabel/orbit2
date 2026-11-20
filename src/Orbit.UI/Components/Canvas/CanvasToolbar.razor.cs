using Microsoft.AspNetCore.Components;

namespace Orbit.UI.Components.Canvas;

/// <summary>Shared toolbar for root and nested viewports.</summary>
public partial class CanvasToolbar
{
    [Parameter] public double ZoomPercentage { get; set; }
    [Parameter] public EventCallback ZoomIn { get; set; }
    [Parameter] public EventCallback ZoomOut { get; set; }
    [Parameter] public EventCallback Reset { get; set; }
    [Parameter] public EventCallback Fit { get; set; }
}
