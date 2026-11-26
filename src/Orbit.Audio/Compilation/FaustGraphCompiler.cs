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

    public FaustGraphProgram Compile(GraphSnapshot graph, int auxiliaryChannels = 0, bool dynamicTaps = false, bool enableMacroModulation = false)
    {
        if (auxiliaryChannels < 0 || auxiliaryChannels > SignalTapManifest.MaxAuxiliaryChannels)
            throw new ArgumentOutOfRangeException(nameof(auxiliaryChannels));
        var flattened = GraphFlattener.Flatten(graph, enableMacroModulation);
        graph = flattened.Graph;
        var outputs = graph.Nodes.Where(node => node.TypeKey == MasterOutput.NodeType).ToArray();
        if (outputs.Length != 1)
            throw new FaustCompilationException("A patch must have exactly one master output.");

        var ordered = GraphDependencyOrder.Get(graph, outputs[0].Id, out var reachableEdges,
            enableMacroModulation ? flattened.MacroDependencies : null);
        var incoming = graph.Connections.ToLookup(edge => edge.Destination);
        var signals = new Dictionary<PortAddress, string>();
        var inputSignals = new Dictionary<PortAddress, string>();
        var tapIdentities = new Dictionary<PortAddress, string>();
        string InputIdentity(PortAddress port, string expression)
        {
            var edges = incoming[port].ToArray();
            return edges.Length == 1 && edges[0].Amount == 1 && tapIdentities.TryGetValue(edges[0].Source, out var identity)
                ? identity : expression;
        }
        var controls = new List<FaustParameterControl>();
        var definitions = new List<string>();
        var macros = enableMacroModulation ? new FaustMacroExpressions(flattened.MacroTargets, signals, definitions) : null;
        var audition = new FaustAuditionControls(definitions);
        var requiresTransportBeat = false;
        var requiresTransportTempo = false;
        var requiresTransportReset = false;
        var actionInputs = new List<(string Name, RhythmicInput Input)>();
        var envelopeMeters = new List<FaustEnvelopeMeter>();
        var pitchMeters = new List<FaustPitchMeter>();
        var outputExpression = "0.0";
        var masterIdentity = "0.0";

        var boundaries = 0;
        foreach (var node in ordered)
        {
            if (node.TypeKey == BoundaryFaustEmitter.NodeType) boundaries++;
            var context = new FaustNodeContext(node, incoming, signals, controls, definitions, macros);
            if (auxiliaryChannels > 0)
                foreach (var port in node.Inputs.Where(port => node.IsInputActive(port.Id)))
                {
                    var address = new PortAddress(node.Id, port.Id);
                    var inputExpression = context.InputSignal(port.Id);
                    inputSignals.Add(address, inputExpression);
                    tapIdentities.Add(address, InputIdentity(address, inputExpression));
                }
            if (node.Id == outputs[0].Id)
            {
                outputExpression = context.Input("audio-in");
                masterIdentity = InputIdentity(new(node.Id, node.Inputs.Single().Id), outputExpression);
                continue;
            }

            if (!emitters.TryGetValue(node.TypeKey, out var emitter))
                throw new FaustCompilationException($"Unsupported connected node type: {node.TypeKey}.");
            if (node.Outputs.Count == 0 || node.Outputs.Any(port => port.ChannelCount != 1) ||
                node.Inputs.Any(port => port.ChannelCount != 1) || (node.Outputs.Count != 1 && emitter is not IFaustMultiOutputEmitter))
                throw new FaustCompilationException("Only mono ports and explicitly supported multi-output emitters are supported.");

            var name = $"n_{node.Id:N}";
            var expression = emitter.Emit(context);
            macros?.VerifyNode(node);
            if (context.EnvelopeMeterName is { } meterName)
            {
                var origin = flattened.Origins[node.Id];
                envelopeMeters.Add(new(origin.GraphId, origin.NodeId, meterName));
            }
            if (context.PitchMeterName is { } pitchMeter)
            {
                var origin = flattened.Origins[node.Id];
                pitchMeters.Add(new(origin.GraphId, origin.NodeId, pitchMeter));
            }
            requiresTransportBeat |= context.RequiresTransportBeat;
            requiresTransportTempo |= context.RequiresTransportTempo;
            requiresTransportReset |= context.RequiresTransportReset;
            if (context.ManualRhythmicInput.HasValue || context.ManualPitchInput || context.ManualNoteInput)
            {
                var origin = flattened.Origins[node.Id];
                actionInputs.Add(($"action_{node.Id:N}", new(origin.GraphId, origin.NodeId, context.ManualRhythmicInput ?? false, context.ManualPitchInput,
                    Pattern: context.ManualPitchInput ? PitchPattern.FromNode(node) : null, IsNote: context.ManualNoteInput)));
                if (actionInputs.Count > 30) throw new FaustCompilationException("A patch supports at most 30 reachable manual action sources.");
            }
            if (node.Outputs.Count > 1) definitions.Add($"{name}_tuple = {expression};");
            for (var index = 0; index < node.Outputs.Count; index++)
            {
                var port = node.Outputs[index];
                var outputName = node.Outputs.Count == 1 ? name : $"{name}_{index}";
                var output = node.Outputs.Count == 1 ? expression :
                    $"({name}_tuple : {string.Join(",", Enumerable.Range(0, node.Outputs.Count).Select(i => i == index ? "_" : "!"))})";
                if (node.SupportsMute || node.BypassInputKey is not null)
                    output = audition.Apply(node, flattened.Origins[node.Id], context, output, port.SignalType);
                definitions.Add($"{outputName} = {output};");
                signals.Add(new(node.Id, port.Id), outputName);
                if (auxiliaryChannels > 0)
                    tapIdentities.Add(new(node.Id, port.Id), node.TypeKey == BoundaryFaustEmitter.NodeType && !node.SupportsMute
                        ? tapIdentities[new(node.Id, node.Inputs.Single().Id)] : outputName);
            }
        }

        var tapChannels = new Dictionary<SignalObservationAddress, int>();
        var outputExpressions = new List<string> { outputExpression };
        if (auxiliaryChannels > 0)
        {
            var catalog = new Dictionary<string, int>(StringComparer.Ordinal) { [masterIdentity] = 0 };
            // Prefer reachable outputs, then weighted inputs; aliases share a channel.
            var candidates = flattened.Endpoints.Select(pair =>
            {
                var isOutput = signals.TryGetValue(pair.Value, out var signal);
                if (!isOutput) inputSignals.TryGetValue(pair.Value, out signal);
                return (Address: new SignalObservationAddress(graph.Id, pair.Key.GraphId,
                    pair.Key.Address.NodeId, pair.Key.Address.PortId,
                    isOutput ? PortDirection.Output : PortDirection.Input), Signal: signal,
                    Identity: tapIdentities.GetValueOrDefault(pair.Value));
            }).Where(candidate => candidate.Signal is not null)
                .OrderByDescending(candidate => candidate.Address.Direction)
                .ThenBy(candidate => candidate.Signal == "0.0")
                .ThenBy(candidate => candidate.Address.GraphId).ThenBy(candidate => candidate.Address.NodeId)
                .ThenBy(candidate => candidate.Address.PortId);
            foreach (var candidate in candidates)
            {
                var channel = dynamicTaps ? catalog.GetValueOrDefault(candidate.Identity!, -1) : outputExpressions.IndexOf(candidate.Signal!);
                if (channel < 0)
                {
                    if (!dynamicTaps && outputExpressions.Count > auxiliaryChannels) continue;
                    channel = outputExpressions.Count;
                    outputExpressions.Add(candidate.Signal!);
                    if (dynamicTaps) catalog.Add(candidate.Identity!, channel);
                }
                tapChannels.Add(candidate.Address, channel);
            }
        }
        var selectorCount = 0;
        if (dynamicTaps && outputExpressions.Count > 1)
        {
            // A single exact integer carries both assignment identity and selector.
            // The passive meter echoes that same value from the rendered DSP.
            selectorCount = outputExpressions.Count - 1;
            var stride = selectorCount + 1;
            var candidates = string.Join(", ", new[] { "0.0" }.Concat(outputExpressions.Skip(1)));
            var slots = Math.Min(auxiliaryChannels, selectorCount);
            outputExpressions = [outputExpression];
            for (var slot = 1; slot <= slots; slot++)
            {
                var control = $"orbit_tap_select_{slot}";
                definitions.Add($"{control} = hslider(\"{control}\", 0, 0, 16777215, 1);");
                outputExpressions.Add($"attach(({candidates}):ba.selectn({stride}, int({control})%{stride}), {control}:hbargraph(\"orbit_tap_ack_{slot}\", 0, 16777215))");
            }
        }
        outputExpression = string.Join(", ", outputExpressions);
        // Canonical order and invariant literals make source stable across base-only edits.
        // The formal parameter is shared by every nested definition; it is
        // exactly one audio input, not a separate input per LFO reference.
        if (requiresTransportTempo)
            definitions.Insert(0, "orbit_tempo = hslider(\"orbit_tempo\", 120, 20, 300, 0.001);");
        audition.AddRampDefinition();
        var inputNames = new List<string>();
        if (requiresTransportBeat) inputNames.Add("orbit_beat");
        if (requiresTransportReset) inputNames.Add("orbit_reset");
        var transportChannels = inputNames.Count;
        inputNames.AddRange(actionInputs.Select(input => input.Name));
        var body = inputNames.Count > 0
            ? $"process({string.Join(",", inputNames)}) = {outputExpression} with {{\n{string.Join("\n", definitions)}\n}};\n"
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
        // Keep static program metadata available for benchmarks and opening the
        // monitor mid-play, without a separate diagnostic graph traversal.
        return new FaustGraphProgram(graph.Id, graph.Revision, source, Array.AsReadOnly(manifest))
            { ReachableLeafNodes = ordered.Count - boundaries - 1, BoundaryRoutes = boundaries,
                ReachableEdges = reachableEdges,
                RequiresTransport = requiresTransportBeat || requiresTransportTempo || actionInputs.Any(input => input.Input.Pattern is not null), TransportInputChannels = transportChannels,
                RhythmicInputs = actionInputs.Select(input => input.Input).ToArray(), AuditionControls = audition.Manifest,
                MacroControls = macros?.Manifest ?? Array.Empty<FaustMacroControl>(),
                SignalTaps = new SignalTapManifest(tapChannels, outputExpressions.Count, selectorCount), EnvelopeMeters = envelopeMeters.AsReadOnly(), PitchMeters = pitchMeters.AsReadOnly() };
    }
}
