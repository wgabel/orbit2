using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;

namespace Orbit.Audio.Compilation;

/// <summary>Compiles an immutable root patch (including child graphs) into one Faust program with a mono master.
/// It never loads a runtime or processes samples on the application thread.</summary>
public sealed class FaustGraphCompiler
{
    private readonly IReadOnlyDictionary<string, IFaustNodeEmitter> emitters;

    public FaustGraphCompiler(IEnumerable<IFaustNodeEmitter> emitters) =>
        this.emitters = emitters.Append(new BoundaryFaustEmitter())
            .ToDictionary(emitter => emitter.TypeKey, StringComparer.Ordinal);

    public static FaustGraphCompiler CreateBuiltIn() =>
        new(BuiltInFaustEmitters.Create());

    public FaustGraphProgram Compile(GraphSnapshot graph, int auxiliaryChannels = 0)
    {
        if (auxiliaryChannels < 0 || auxiliaryChannels > SignalTapManifest.MaxAuxiliaryChannels)
            throw new ArgumentOutOfRangeException(nameof(auxiliaryChannels));
        var flattened = GraphFlattener.Flatten(graph);
        graph = flattened.Graph;
        var outputs = graph.Nodes.Where(node => node.TypeKey == MasterOutput.NodeType).ToArray();
        if (outputs.Length != 1)
            throw new FaustCompilationException("A patch must have exactly one master output.");

        var ordered = GraphDependencyOrder.Get(graph, outputs[0].Id);
        var incoming = graph.Connections.ToLookup(edge => edge.Destination);
        var signals = new Dictionary<PortAddress, string>();
        var inputSignals = new Dictionary<PortAddress, string>();
        var controls = new List<FaustParameterControl>();
        var definitions = new List<string>();
        var audition = new FaustAuditionControls(definitions);
        var requiresTransportBeat = false;
        var requiresTransportTempo = false;
        var outputExpression = "0.0";

        foreach (var node in ordered)
        {
            var context = new FaustNodeContext(node, incoming, signals, controls, definitions);
            if (auxiliaryChannels > 0)
                foreach (var port in node.Inputs.Where(port => node.IsInputActive(port.Id)))
                    inputSignals.Add(new PortAddress(node.Id, port.Id), context.InputSignal(port.Id));
            if (node.Id == outputs[0].Id)
            {
                outputExpression = context.Input("audio-in");
                continue;
            }

            if (!emitters.TryGetValue(node.TypeKey, out var emitter))
                throw new FaustCompilationException($"Unsupported connected node type: {node.TypeKey}.");
            if (node.Outputs.Count != 1 || node.Outputs[0].ChannelCount != 1 ||
                node.Inputs.Any(port => port.ChannelCount != 1))
                throw new FaustCompilationException("Only mono, single-output nodes are supported.");

            var name = $"n_{node.Id:N}";
            var expression = emitter.Emit(context);
            if (node.SupportsMute || node.BypassInputKey is not null)
                expression = audition.Apply(node, flattened.Origins[node.Id], context, expression);
            requiresTransportBeat |= context.RequiresTransportBeat;
            requiresTransportTempo |= context.RequiresTransportTempo;
            definitions.Add($"{name} = {expression};");
            signals.Add(new PortAddress(node.Id, node.Outputs[0].Id), name);
        }

        var tapChannels = new Dictionary<SignalObservationAddress, int>();
        var outputExpressions = new List<string> { outputExpression };
        if (auxiliaryChannels > 0)
        {
            // Prefer reachable outputs, then weighted inputs; aliases share a channel.
            var candidates = flattened.Endpoints.Select(pair =>
            {
                var isOutput = signals.TryGetValue(pair.Value, out var signal);
                if (!isOutput) inputSignals.TryGetValue(pair.Value, out signal);
                return (Address: new SignalObservationAddress(graph.Id, pair.Key.GraphId,
                    pair.Key.Address.NodeId, pair.Key.Address.PortId,
                    isOutput ? PortDirection.Output : PortDirection.Input), Signal: signal);
            }).Where(candidate => candidate.Signal is not null)
                .OrderByDescending(candidate => candidate.Address.Direction)
                .ThenBy(candidate => candidate.Signal == "0.0")
                .ThenBy(candidate => candidate.Address.GraphId).ThenBy(candidate => candidate.Address.NodeId)
                .ThenBy(candidate => candidate.Address.PortId);
            foreach (var candidate in candidates)
            {
                var channel = outputExpressions.IndexOf(candidate.Signal!);
                if (channel < 0)
                {
                    if (outputExpressions.Count > auxiliaryChannels) continue;
                    channel = outputExpressions.Count;
                    outputExpressions.Add(candidate.Signal!);
                }
                tapChannels.Add(candidate.Address, channel);
            }
        }
        outputExpression = string.Join(", ", outputExpressions);
        // Canonical order and invariant literals make source stable across base-only edits.
        // The formal parameter is shared by every nested definition; it is
        // exactly one audio input, not a separate input per LFO reference.
        if (requiresTransportTempo)
            definitions.Insert(0, "orbit_tempo = hslider(\"orbit_tempo\", 120, 20, 300, 0.001);");
        audition.AddRampDefinition();
        var body = requiresTransportBeat
            ? $"process(orbit_beat) = {outputExpression} with {{\n{string.Join("\n", definitions)}\n}};\n"
            : string.Join("\n", definitions.Append($"process = {outputExpression};")) + "\n";
        var source = "import(\"stdfaust.lib\");\ndeclare name \"OrbitGraph\";\n" + body;
        // Faust uses qualified backend identities; the manifest points back to
        // the original graph and parameter, not to temporary routing nodes.
        var manifest = controls.Select(control =>
        {
            var origin = flattened.Origins[control.Address.NodeId];
            return control with { GraphId = origin.GraphId,
                Address = control.Address with { NodeId = origin.NodeId } };
        }).ToArray();
        return new FaustGraphProgram(graph.Id, graph.Revision, source, Array.AsReadOnly(manifest))
            { RequiresTransport = requiresTransportBeat || requiresTransportTempo, AuditionControls = audition.Manifest,
                SignalTaps = new SignalTapManifest(tapChannels, outputExpressions.Count) };
    }
}
