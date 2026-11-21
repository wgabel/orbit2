using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Orbit.UI.Components.Canvas;

/// <summary>Draws one routed edge, without owning the graph or its endpoints.</summary>
public partial class ConnectionPath
{
    [Parameter, EditorRequired] public ConnectionVisual Wire { get; set; } = default!;
    [Parameter] public EventCallback<Guid> OnSelect { get; set; }

    private string WireClass => $"connection-path connection-{Wire.Kind.ToString().ToLowerInvariant()}{(Wire.Selected ? " connection-selected" : "")}{(Wire.Dormant ? " connection-dormant" : "") + (Wire.Zero ? " connection-zero" : "")}";
    private Task Select(PointerEventArgs args) => args.Button == 0
        ? OnSelect.InvokeAsync(Wire.Id) : Task.CompletedTask;
}
