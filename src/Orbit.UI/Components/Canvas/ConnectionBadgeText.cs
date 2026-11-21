using System.Globalization;
using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;
using Orbit.UI.Components.Parameters;
using Orbit.Application.Graphs.Snapshots;

namespace Orbit.UI.Components.Canvas;

/// <summary>Shared committed connection semantics for badges and the inspector.</summary>
public static class ConnectionBadgeText
{
    public static string Format(GraphSnapshot graph, SignalConnection connection)
    {
        var destination = graph.GetNode(connection.Destination.NodeId);
        var input = destination.Inputs.Single(port => port.Id == connection.Destination.PortId);
        var parameter = destination.Parameters.SingleOrDefault(p => p.ModulationPortId == input.Id);
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
        return $"{source.Name}: {sourcePort.Name} to {destination.Name}: {destinationPort.Name}, amount {connection.Amount.ToString("G17", CultureInfo.InvariantCulture)}";
    }
}
