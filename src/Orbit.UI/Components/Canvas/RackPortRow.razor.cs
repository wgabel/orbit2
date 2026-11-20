using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Commands;
using Orbit.Domain.Ports;

namespace Orbit.UI.Components.Canvas;

/// <summary>Reusable per-port draft and command controls. Snapshot changes
/// update the committed name without discarding drafts on telemetry renders.</summary>
public partial class RackPortRow
{
    [Parameter, EditorRequired] public Guid GraphId { get; set; }
    [Parameter, EditorRequired] public PortDirection Direction { get; set; }
    [Parameter, EditorRequired] public PortDefinition Port { get; set; } = default!;
    [Parameter] public int Index { get; set; }
    [Parameter] public int Count { get; set; }
    [Parameter, EditorRequired] public Func<IGraphCommand, string?> Dispatch { get; set; } = default!;

    private string? committedName;
    private string DraftName { get; set; } = "";
    private string? Error { get; set; }

    protected override void OnParametersSet()
    {
        if (committedName == Port.Name) return;
        committedName = Port.Name;
        DraftName = Port.Name;
        Error = null;
    }

    private void Rename() => Error = Dispatch(new RenameBoundaryPortCommand(GraphId, Direction, Port.Id, DraftName.Trim()));
    private void MoveUp() => Error = Dispatch(new MoveBoundaryPortCommand(GraphId, Direction, Port.Id, Index - 1));
    private void MoveDown() => Error = Dispatch(new MoveBoundaryPortCommand(GraphId, Direction, Port.Id, Index + 1));
    private void Remove() => Error = Dispatch(new RemoveBoundaryPortCommand(GraphId, Direction, Port.Id));
}
