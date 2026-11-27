using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Services;
using Orbit.Application.Models;
using Orbit.Audio.Compilation;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Timing;

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
        var result = Analyze(root);
        root.GetGraph(graphId);
        return result.Parameters.Where(pair => result.Origins[pair.Key.NodeId].GraphId == graphId)
            .ToDictionary(pair => pair.Key with { NodeId = result.Origins[pair.Key.NodeId].NodeId }, pair => pair.Value);
    }

    public IReadOnlyDictionary<GraphMacroAddress, ParameterRange?> EstimateMacroRanges(GraphSnapshot root, Guid graphId)
    {
        root.GetGraph(graphId);
        return Analyze(root).Macros.Where(pair => pair.Key.GraphId == graphId).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private (IReadOnlyDictionary<ParameterAddress, ParameterRange?> Parameters,
        IReadOnlyDictionary<GraphMacroAddress, ParameterRange?> Macros,
        IReadOnlyDictionary<Guid, GraphFlattener.NodeOrigin> Origins) Analyze(GraphSnapshot root)
    {
        var flattened = GraphFlattener.Flatten(root, includeMacroDependencies: true);
        var graph = flattened.Graph;
        var nodes = graph.Nodes.ToDictionary(node => node.Id);
        var incoming = graph.Connections.ToLookup(edge => edge.Destination);
        var signals = new Dictionary<PortAddress, ParameterRange?>();
        var visiting = new HashSet<PortAddress>();
        var ranges = new Dictionary<ParameterAddress, ParameterRange?>();
        var macroTargets = flattened.MacroTargets;
        var macroRanges = new Dictionary<GraphMacroAddress, ParameterRange?>();
        var macroSources = macroTargets.Values.Select(target => target.Source).DistinctBy(source =>
            new GraphMacroAddress(source.GraphId, source.CompositeId, source.MacroId)).ToArray();

        ParameterRange? Output(PortAddress address)
        {
            if (signals.TryGetValue(address, out var cached)) return cached;
            if (!visiting.Add(address)) return null;
            try
            {
                ParameterRange? range = null;
                if (nodes.TryGetValue(address.NodeId, out var node) &&
                    node.Outputs.SingleOrDefault(port => port.Id == address.PortId) is { } outputPort &&
                    rules.TryGetValue(node.TypeKey, out var rule))
                    range = node.IsMuted ? new ParameterRange(0, 0) :
                        node.IsBypassed && node.BypassInputKey is { } key
                            ? Input(new PortAddress(node.Id, node.Inputs.Single(port => port.Key == key).Id))
                            : rule.EstimateOutputRange(new SignalRangeContext(node,
                                port => Input(new PortAddress(node.Id, port)), parameter => Effective(node, parameter)), outputPort.Key);
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

        GraphMacroAddress MacroAddress(GraphFlattener.MacroSource source) =>
            new(source.GraphId, source.CompositeId, source.MacroId);

        ParameterRange? MacroRange(GraphFlattener.MacroSource source)
        {
            var address = MacroAddress(source);
            if (macroRanges.TryGetValue(address, out var cached)) return cached;
            if (Output(source.Signal) is not { } modulation)
                return macroRanges[address] = null;
            var minimum = Math.Clamp(source.BaseValue + modulation.Minimum, 0, 1);
            var maximum = Math.Clamp(source.BaseValue + modulation.Maximum, 0, 1);
            return macroRanges[address] = new ParameterRange(Math.Min(minimum, maximum), Math.Max(minimum, maximum));
        }

        ParameterRange? BaseRange(GraphNodeSnapshot node, ParameterSnapshot parameter)
        {
            var address = new ParameterAddress(node.Id, parameter.Id);
            if (!macroTargets.TryGetValue(address, out var target))
                return new ParameterRange(parameter.BaseValue, parameter.BaseValue);
            if (MacroRange(target.Source) is not { } normalized) return null;
            var a = Map(target.Binding, normalized.Minimum);
            var b = Map(target.Binding, normalized.Maximum);
            return new ParameterRange(Math.Min(a, b), Math.Max(a, b));
        }

        ParameterRange? Effective(GraphNodeSnapshot node, ParameterSnapshot parameter)
        {
            var definition = parameter.Definition;
            if (!definition.IsValidBaseValue(parameter.BaseValue)) return null;
            var address = new ParameterAddress(node.Id, parameter.Id);
            var baseRange = BaseRange(node, parameter);
            if (baseRange is null) return ranges[address] = null;
            // The frequency control is a base frequency, scaled by semitones before additive Hz modulation.
            if (ActivePitchForFrequency(node, parameter) is { } pitch)
            {
                if (ranges.TryGetValue(address, out var cachedPitchRange)) return cachedPitchRange;
                if (Effective(node, pitch) is not { } pitchRange ||
                    Input(new PortAddress(node.Id, parameter.ModulationPortId!.Value)) is not { } hzRange)
                    return ranges[address] = null;
                return ranges[address] = new ParameterRange(
                    MusicalPitch.Frequency(baseRange.Value.Minimum, pitchRange.Minimum, hzRange.Minimum, definition.Minimum, definition.Maximum),
                    MusicalPitch.Frequency(baseRange.Value.Maximum, pitchRange.Maximum, hzRange.Maximum, definition.Minimum, definition.Maximum));
            }
            if (parameter.ModulationPortId is not Guid port ||
                !incoming[new PortAddress(node.Id, port)].Any())
            {
                if (macroTargets.ContainsKey(address)) return ranges[address] = baseRange;
                return new ParameterRange(parameter.BaseValue, parameter.BaseValue);
            }
            if (ranges.TryGetValue(address, out var cached)) return cached;
            if (definition.Scale != ParameterScale.Linear ||
                Input(new PortAddress(node.Id, port)) is not { } modulation)
                return ranges[address] = null;
            return ranges[address] = new ParameterRange(
                Math.Clamp(baseRange.Value.Minimum + modulation.Minimum, definition.Minimum, definition.Maximum),
                Math.Clamp(baseRange.Value.Maximum + modulation.Maximum, definition.Minimum, definition.Maximum));
        }

        foreach (var node in graph.Nodes)
            foreach (var parameter in node.Parameters)
                if (parameter.ModulationPortId is Guid port && (incoming[new PortAddress(node.Id, port)].Any() ||
                    ActivePitchForFrequency(node, parameter) is not null || macroTargets.ContainsKey(new ParameterAddress(node.Id, parameter.Id))))
                    Effective(node, parameter);
        foreach (var source in macroSources) MacroRange(source);
        return (ranges, macroRanges, flattened.Origins);
    }

    private static double Map(Orbit.Domain.Nodes.Composites.CompositeMacroBinding binding, double value)
    {
        var position = Math.Pow(Math.Clamp(value, 0, 1), binding.Exponent);
        return (1 - position) * binding.AtZero + position * binding.AtOne;
    }

    private static ParameterSnapshot? ActivePitchForFrequency(GraphNodeSnapshot node, ParameterSnapshot parameter) =>
        parameter.Definition.Key == "frequency" && node.TypeKey is OscillatorGenerator.NodeType or ResonatorProcessor.NodeType
            ? node.Parameters.SingleOrDefault(p => p.Definition.Key == OscillatorGenerator.PitchKey && node.IsParameterActive(p)) : null;
}
