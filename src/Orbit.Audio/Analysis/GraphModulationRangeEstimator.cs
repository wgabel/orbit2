using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Services;
using Orbit.Audio.Compilation;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Audio.Analysis;

/// <summary>Interval analysis of the editable graph (including disconnected nodes).
/// Unknown backend bounds propagate as unknown; the DSP is never simulated here.</summary>
public sealed class GraphModulationRangeEstimator(IEnumerable<IFaustNodeEmitter> emitters) : IGraphModulationRangeEstimator
{
    private readonly IReadOnlyDictionary<string, IFaustNodeEmitter> rules =
        emitters.Append(new BoundaryFaustEmitter()).ToDictionary(emitter => emitter.TypeKey, StringComparer.Ordinal);

    public IReadOnlyDictionary<ParameterAddress, ParameterRange?> Estimate(GraphSnapshot graph) => Estimate(graph, graph.Id);

    public IReadOnlyDictionary<ParameterAddress, ParameterRange?> Estimate(GraphSnapshot root, Guid graphId)
    {
        root.GetGraph(graphId);
        var flattened = GraphFlattener.Flatten(root);
        var graph = flattened.Graph;
        var nodes = graph.Nodes.ToDictionary(node => node.Id);
        var incoming = graph.Connections.ToLookup(edge => edge.Destination);
        var signals = new Dictionary<PortAddress, ParameterRange?>();
        var visiting = new HashSet<PortAddress>();
        var ranges = new Dictionary<ParameterAddress, ParameterRange?>();

        ParameterRange? Output(PortAddress address)
        {
            if (signals.TryGetValue(address, out var cached)) return cached;
            if (!visiting.Add(address)) return null;
            try
            {
                ParameterRange? range = null;
                if (nodes.TryGetValue(address.NodeId, out var node) &&
                    node.Outputs.Count == 1 && node.Outputs[0].Id == address.PortId &&
                    rules.TryGetValue(node.TypeKey, out var rule))
                    range = node.IsMuted ? new ParameterRange(0, 0) :
                        node.IsBypassed && node.BypassInputKey is { } key
                            ? Input(new PortAddress(node.Id, node.Inputs.Single(port => port.Key == key).Id))
                            : rule.EstimateOutputRange(new SignalRangeContext(node,
                                port => Input(new PortAddress(node.Id, port)), parameter => Effective(node, parameter)));
                return signals[address] = range;
            }
            finally { visiting.Remove(address); }
        }

        ParameterRange? Input(PortAddress address)
        {
            double minimum = 0, maximum = 0;
            foreach (var edge in incoming[address])
            {
                if (edge.Amount == 0) continue;
                if (Output(edge.Source) is not { } source) return null;
                var a = source.Minimum * edge.Amount;
                var b = source.Maximum * edge.Amount;
                minimum += Math.Min(a, b);
                maximum += Math.Max(a, b);
            }
            return double.IsFinite(minimum) && double.IsFinite(maximum) ? new ParameterRange(minimum, maximum) : null;
        }

        ParameterRange? Effective(GraphNodeSnapshot node, ParameterSnapshot parameter)
        {
            var definition = parameter.Definition;
            if (!definition.IsValidBaseValue(parameter.BaseValue)) return null;
            var address = new ParameterAddress(node.Id, parameter.Id);
            if (parameter.ModulationPortId is not Guid port ||
                !incoming[new PortAddress(node.Id, port)].Any())
                return new ParameterRange(parameter.BaseValue, parameter.BaseValue);
            if (ranges.TryGetValue(address, out var cached)) return cached;
            if (definition.Scale != ParameterScale.Linear ||
                Input(new PortAddress(node.Id, port)) is not { } modulation)
                return ranges[address] = null;
            return ranges[address] = new ParameterRange(
                Math.Clamp(parameter.BaseValue + modulation.Minimum, definition.Minimum, definition.Maximum),
                Math.Clamp(parameter.BaseValue + modulation.Maximum, definition.Minimum, definition.Maximum));
        }

        foreach (var node in graph.Nodes)
            foreach (var parameter in node.Parameters)
                if (parameter.ModulationPortId is Guid port && incoming[new PortAddress(node.Id, port)].Any())
                    Effective(node, parameter);
        return ranges.Where(pair => flattened.Origins[pair.Key.NodeId].GraphId == graphId)
            .ToDictionary(pair => pair.Key with { NodeId = flattened.Origins[pair.Key.NodeId].NodeId },
                pair => pair.Value);
    }
}
