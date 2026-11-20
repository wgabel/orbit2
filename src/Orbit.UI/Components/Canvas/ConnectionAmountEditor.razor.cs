using System.Globalization;
using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;
using Orbit.UI.Components.Parameters;

namespace Orbit.UI.Components.Canvas;

/// <summary>Presentation-only inspector for a single selected connection.</summary>
public partial class ConnectionAmountEditor
{
    [Parameter, EditorRequired] public GraphSnapshot Graph { get; set; } = default!;
    [Parameter, EditorRequired] public SignalConnection Connection { get; set; } = default!;
    [Parameter, EditorRequired] public Func<SetConnectionAmountCommand, string?> Dispatch { get; set; } = default!;

    private readonly string inputId = $"connection-amount-{Guid.NewGuid():N}";
    private string InputId => inputId;
    private string ErrorId => $"{inputId}-error";
    private string Draft { get; set; } = "";
    private string? Error { get; set; }
    private double? committedAmount;
    private GraphNodeSnapshot Source => Graph.GetNode(Connection.Source.NodeId);
    private GraphNodeSnapshot Destination => Graph.GetNode(Connection.Destination.NodeId);
    private PortDefinition SourcePort => Source.Outputs.Single(port => port.Id == Connection.Source.PortId);
    private PortDefinition DestinationPort => Destination.Inputs.Single(port => port.Id == Connection.Destination.PortId);
    private ParameterSnapshot? TargetParameter => Destination.Parameters.SingleOrDefault(parameter =>
        parameter.ModulationPortId == Connection.Destination.PortId);
    private bool IsModulation => DestinationPort.SignalType == SignalType.Modulation;
    private string RouteName => $"{Source.Name}: {SourcePort.Name} → {Destination.Name}: {DestinationPort.Name}";
    private string AmountLabel => IsModulation ? "Modulation depth" : "Audio gain";
    private string UnitLabel => TargetParameter is { } target
        ? ParameterUnitLabel.Format(target.Definition.Unit) : "multiplier";
    private string Explanation => IsModulation && TargetParameter is not null
        ? "The incoming signal is multiplied by this depth and added to the target base value. Parameter limits still apply."
        : "The incoming signal is multiplied by this amount. Negative values invert it; zero silences this route.";

    protected override void OnParametersSet()
    {
        // Other graph updates and live telemetry must not erase an unfinished edit.
        if (committedAmount == Connection.Amount) return;
        committedAmount = Connection.Amount;
        Draft = Connection.Amount.ToString("G", CultureInfo.InvariantCulture);
        Error = null;
    }

    private void EditDraft(ChangeEventArgs args)
    {
        Draft = args.Value?.ToString() ?? "";
        Error = null;
    }

    private void Apply()
    {
        if (!double.TryParse(Draft, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
            || !double.IsFinite(amount))
        {
            Error = "Enter a finite number.";
            return;
        }
        Error = Dispatch(new SetConnectionAmountCommand(Graph.Id, Connection.Id, amount));
    }
}
