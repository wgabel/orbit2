using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Ports;
using Orbit.Domain.Parameters;
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
    [Parameter] public EventCallback<Guid> OnParameterGestureEnded { get; set; }
    [Parameter] public EventCallback<Guid> OnOpenComposite { get; set; }
    [Parameter] public EventCallback<Guid> OnEditRack { get; set; }
    [Parameter] public IReadOnlyDictionary<ParameterAddress, ParameterRange?> ModulationRanges { get; set; } = new Dictionary<ParameterAddress, ParameterRange?>();
    [Parameter] public IReadOnlyDictionary<ParameterAddress, double> EffectiveValues { get; set; } = new Dictionary<ParameterAddress, double>();

    private bool IsModulated(Guid id) => ModulationRanges.ContainsKey(new ParameterAddress(Node.Id, id));
    private ParameterRange? GetRange(Guid id) => ModulationRanges.GetValueOrDefault(new ParameterAddress(Node.Id, id));
    private double? GetEffective(Guid id) => EffectiveValues.TryGetValue(new ParameterAddress(Node.Id, id), out var value) ? value : null;

    private string FrameClass => $"node-frame node-role-{Role.ToString().ToLowerInvariant()}{(Selected ? " node-selected" : "")}";
    private string PositionStyle => FormattableString.Invariant($"left:{Position.X}px;top:{Position.Y}px;");
}
