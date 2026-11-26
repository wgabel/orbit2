using System.Globalization;
using Orbit.Domain.Graphs;
using Orbit.Presentation.Canvas;
using Orbit.UI.Components.Parameters;
using Orbit.Application.Graphs.Snapshots;

namespace Orbit.UI.Components.Canvas;

/// <summary>Shared committed connection semantics for badges and the inspector.</summary>
public static class ConnectionBadgeText
{
    public static string Format(GraphSnapshot graph, SignalConnection connection)
    {
        var semantics = ConnectionAmountSemantics.Resolve(graph, connection);
        if (semantics.IsEvent) return semantics.DestinationType.ToString();
        var parameter = semantics.TargetParameter;
        var amount = connection.Amount;
        var magnitude = Math.Abs(amount).ToString("0.###", CultureInfo.InvariantCulture);
        if (parameter is null)
            return (amount < 0 ? "−" : "") + "×" + magnitude;
        var unit = ParameterUnitLabel.Format(parameter.Definition.Unit);
        return (amount > 0 ? "+" : amount < 0 ? "−" : "") + magnitude +
            (unit == "normalized" || unit == "ratio" ? "" : " " + unit);
    }

    public static string Describe(GraphSnapshot graph, SignalConnection connection)
    {
        var source = graph.GetNode(connection.Source.NodeId);
        var destination = graph.GetNode(connection.Destination.NodeId);
        var sourcePort = source.Outputs.Single(p => p.Id == connection.Source.PortId);
        var destinationPort = destination.Inputs.Single(p => p.Id == connection.Destination.PortId);
        var detail = ConnectionAmountSemantics.Resolve(graph, connection).IsEvent ? "unity rhythmic event" : $"amount {connection.Amount.ToString("G17", CultureInfo.InvariantCulture)}";
        return $"{source.Name}: {sourcePort.Name} to {destination.Name}: {destinationPort.Name}, {detail}";
    }
}
