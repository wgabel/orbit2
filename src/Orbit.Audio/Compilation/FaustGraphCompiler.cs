using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;

namespace Orbit.Audio.Compilation;

/// <summary>Compiles an immutable root patch (including child graphs) into one mono Faust program.
/// It never loads a runtime or processes samples on the application thread.</summary>
public sealed class FaustGraphCompiler
{
    private readonly IReadOnlyDictionary<string, IFaustNodeEmitter> emitters;

    public FaustGraphCompiler(IEnumerable<IFaustNodeEmitter> emitters) =>
        this.emitters = emitters.Append(new BoundaryFaustEmitter())
            .ToDictionary(emitter => emitter.TypeKey, StringComparer.Ordinal);

    public static FaustGraphCompiler CreateBuiltIn() =>
        new(BuiltInFaustEmitters.Create());

    public FaustGraphProgram Compile(GraphSnapshot graph)
    {
        var flattened = GraphFlattener.Flatten(graph);
        graph = flattened.Graph;
        var outputs = graph.Nodes.Where(node => node.TypeKey == MasterOutput.NodeType).ToArray();
        if (outputs.Length != 1)
            throw new FaustCompilationException("A patch must have exactly one master output.");

        var ordered = GraphDependencyOrder.Get(graph, outputs[0].Id);
        var incoming = graph.Connections.ToLookup(edge => edge.Destination);
        var signals = new Dictionary<PortAddress, string>();
        var controls = new List<FaustParameterControl>();
        var muteControls = new Dictionary<(Guid GraphId, Guid NodeId), FaustMuteControl>();
        var definitions = new List<string>();
        var requiresTransportBeat = false;
        var requiresTransportTempo = false;
        var outputExpression = "0.0";

        foreach (var node in ordered)
        {
            var context = new FaustNodeContext(node, incoming, signals, controls, definitions);
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
            if (node.SupportsMute)
            {
                var origin = flattened.Origins[node.Id];
                var address = (origin.GraphId, origin.NodeId);
                if (!muteControls.TryGetValue(address, out var control))
                {
                    control = new FaustMuteControl(origin.GraphId, origin.NodeId,
                        $"mute_{origin.GraphId:N}_{origin.NodeId:N}", node.IsMuted ? 0 : 1);
                    muteControls.Add(address, control);
                    definitions.Add($"{control.Name} = orbit_mute_ramp(hslider(\"{control.Name}\", 1, 0, 1, 1));");
                }
                expression = $"({expression}) * {control.Name}";
            }
            requiresTransportBeat |= context.RequiresTransportBeat;
            requiresTransportTempo |= context.RequiresTransportTempo;
            definitions.Add($"{name} = {expression};");
            signals.Add(new PortAddress(node.Id, node.Outputs[0].Id), name);
        }

        // Canonical order and invariant literals make source stable across base-only edits.
        // The formal parameter is shared by every nested definition; it is
        // exactly one audio input, not a separate input per LFO reference.
        if (requiresTransportTempo)
            definitions.Insert(0, "orbit_tempo = hslider(\"orbit_tempo\", 120, 20, 300, 0.001);");
        // Start at the actual host value (including a restored mute), then
        // linearly slew to later targets in 10 ms without resetting DSP state.
        if (muteControls.Count > 0)
            definitions.Insert(0, "orbit_mute_ramp(target) = step ~ _ with { delta = 1.0 / (0.01 * ma.SR); step(previous) = select2(1 - (1 : mem), previous + max(-delta, min(delta, target - previous)), target); };");
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
            { RequiresTransport = requiresTransportBeat || requiresTransportTempo, MuteControls = Array.AsReadOnly(muteControls.Values.ToArray()) };
    }
}
