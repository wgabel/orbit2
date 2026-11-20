using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;

namespace Orbit.UI.Components.Canvas;

/// <summary>Transient form state for either boundary rack. The editor never
/// changes graph snapshots and exposes only mono audio/modulation port creation.</summary>
public partial class RackPortEditor
{
    [Parameter, EditorRequired] public Guid GraphId { get; set; }
    [Parameter, EditorRequired] public GraphNodeSnapshot Rack { get; set; } = default!;
    [Parameter, EditorRequired] public Func<IGraphCommand, string?> Dispatch { get; set; } = default!;
    [Parameter] public EventCallback OnClose { get; set; }

    private bool IsInputRack => Rack.TypeKey == InputRack.NodeType;
    private PortDirection Direction => IsInputRack ? PortDirection.Input : PortDirection.Output;
    private IReadOnlyList<PortDefinition> Ports => IsInputRack ? Rack.Outputs : Rack.Inputs;
    private string NewName { get; set; } = "";
    private SignalType NewSignalType { get; set; } = SignalType.Modulation;
    private FanInPolicy NewFanIn { get; set; } = FanInPolicy.Single;
    private string? Error { get; set; }

    private void AddPort()
    {
        Error = Dispatch(new AddBoundaryPortCommand(GraphId, Direction, NewName.Trim(), NewSignalType, NewFanIn));
        if (Error is null) NewName = "";
    }
}
