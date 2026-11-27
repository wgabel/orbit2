using System.Globalization;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;

namespace Orbit.Presentation.Canvas;

/// <summary>Shared, renderer-independent destination semantics for an existing edge.
/// Non-event coefficients are finite but unbounded; there is no authored slider range.</summary>
public sealed record ConnectionAmountSemantics(
    SignalType DestinationType, ParameterSnapshot? TargetParameter)
{
    public bool IsEvent => DestinationType.IsEvent();
    public bool IsModulation => DestinationType == SignalType.Modulation;
    public bool CanEdit => !IsEvent;

    public static string FormatExactAmount(double amount) => amount.ToString("R", CultureInfo.InvariantCulture);

    public static bool TryParseAmount(string text, out double amount)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount)
           && double.IsFinite(amount);

    public static ConnectionAmountSemantics Resolve(GraphSnapshot graph, SignalConnection connection)
    {
        var destination = graph.GetNode(connection.Destination.NodeId);
        var input = destination.Inputs.Single(port => port.Id == connection.Destination.PortId);
        var parameter = destination.Parameters.SingleOrDefault(p => p.ModulationPortId == input.Id);
        return new(input.SignalType, parameter);
    }
}
