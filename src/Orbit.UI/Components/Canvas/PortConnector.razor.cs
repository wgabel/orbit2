using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Domain.Ports;

namespace Orbit.UI.Components.Canvas;

/// <summary>Reusable port presentation; routing rules remain in SignalGraph.</summary>
public partial class PortConnector
{
    [Parameter, EditorRequired] public PortDefinition Port { get; set; } = default!;
    [Parameter] public Guid NodeId { get; set; }
    [Parameter] public bool Active { get; set; }
    [Parameter] public bool Dormant { get; set; }
    [Parameter] public EventCallback<PortPointerStart> OnOutputPointerDown { get; set; }
    [Parameter] public EventCallback<PortAddress> OnActivate { get; set; }

    private PortAddress Address => new(NodeId, Port.Id);
    private string ConnectorClass => $"port-connector port-{Port.Direction.ToString().ToLowerInvariant()}{(Active ? " port-active" : "")}{(Dormant ? " port-dormant" : "")}";
    private string MarkerClass => $"port-marker port-kind-{Port.SignalType.ToString().ToLowerInvariant()}";
    private string Description => $"{Port.Direction}: {Port.Name} ({Port.SignalType}){(Dormant ? " — inactive in current mode" : "")}";

    private Task PointerDown(PointerEventArgs args) =>
        Port.Direction == PortDirection.Output && args.Button == 0
            ? OnOutputPointerDown.InvokeAsync(new PortPointerStart(Address, args))
            : Task.CompletedTask;

    // Mouse output activation is handled by pointer-down; keyboard activation
    // (Detail == 0) uses the same click-to-connect workflow.
    private Task Activate(MouseEventArgs args) =>
        Port.Direction == PortDirection.Input || args.Detail == 0
            ? OnActivate.InvokeAsync(Address) : Task.CompletedTask;
}
