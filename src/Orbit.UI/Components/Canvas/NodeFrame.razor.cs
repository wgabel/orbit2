using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>Reusable node shell; the owning canvas supplies graph data and coordinates.</summary>
public partial class NodeFrame
{
    [Parameter, EditorRequired] public GraphNodeSnapshot Node { get; set; } = default!;
    [Parameter] public CanvasPoint Position { get; set; }
    [Parameter] public NodeVisualRole Role { get; set; }
    [Parameter] public bool Selected { get; set; }
    [Parameter] public PortAddress? PendingSource { get; set; }
    [Parameter] public EventCallback<PointerEventArgs> OnHeaderPointerDown { get; set; }
    [Parameter] public EventCallback<PortAddress> OnPortActivate { get; set; }
    [Parameter] public EventCallback<PortPointerStart> OnOutputPointerDown { get; set; }
    [Parameter] public EventCallback<ParameterEditRequest> OnParameterEdit { get; set; }

    private string FrameClass => $"node-frame node-role-{Role.ToString().ToLowerInvariant()}{(Selected ? " node-selected" : "")}";
    private string PositionStyle => FormattableString.Invariant($"left:{Position.X}px;top:{Position.Y}px;");
}
