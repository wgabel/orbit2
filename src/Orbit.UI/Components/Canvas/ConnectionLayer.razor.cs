using Microsoft.AspNetCore.Components;

namespace Orbit.UI.Components.Canvas;

/// <summary>Reusable SVG layer; paths are already calculated in world coordinates.</summary>
public partial class ConnectionLayer
{
    [Parameter, EditorRequired] public IReadOnlyList<ConnectionVisual> Wires { get; set; } = Array.Empty<ConnectionVisual>();
    [Parameter] public string? DraftPath { get; set; }
    [Parameter] public string? DraftRigidPath { get; set; }
    [Parameter] public EventCallback<Guid> OnSelect { get; set; }
}
