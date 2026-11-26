using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation;

/// <summary>One normalized, smoothed host base plus the existing weighted
/// boundary signal. Each reachable target consumes a mapped base expression,
/// never an authored parameter writer or a second additive modulation edge.</summary>
internal sealed class FaustMacroExpressions(
    IReadOnlyDictionary<ParameterAddress, GraphFlattener.MacroTarget> targets,
    IReadOnlyDictionary<PortAddress, string> signals,
    List<string> definitions)
{
    private readonly Dictionary<FaustMacroAddress, FaustMacroControl> controls = [];
    private readonly HashSet<ParameterAddress> consumed = [];
    private readonly ILookup<Guid, ParameterAddress> byNode = targets.Keys.ToLookup(address => address.NodeId);

    public IReadOnlyList<FaustMacroControl> Manifest => controls.Values.ToArray();

    public string? Resolve(GraphNodeSnapshot node, ParameterSnapshot parameter)
    {
        var address = new ParameterAddress(node.Id, parameter.Id);
        if (!targets.TryGetValue(address, out var target)) return null;
        // These node families also read authored parameters in host action
        // scheduling. Do not silently diverge between host and DSP semantics.
        if (node.TypeKey is NoteSequencerGenerator.NodeType or NoteSourceGenerator.NodeType)
            throw new FaustCompilationException("Audio-rate macro mapping of host-managed note controls is not supported.");
        if (parameter.Definition.Choices is not null)
            throw new FaustCompilationException("Audio-rate macro targets must be continuous.");

        var source = target.Source;
        var identity = new FaustMacroAddress(source.GraphId, source.CompositeId, source.MacroId);
        if (!controls.TryGetValue(identity, out var control))
        {
            if (!signals.TryGetValue(source.Signal, out var signal))
                throw new FaustCompilationException("Macro input dependency was not compiled.");
            var name = $"macro_{identity.GraphId:N}_{identity.CompositeId:N}_{identity.MacroId:N}";
            var meter = $"effective_{name}";
            control = new FaustMacroControl(identity, name, source.BaseValue, meter);
            controls.Add(identity, control);
            var slider = $"hslider(\"{name}\", {FaustNumber.Format(source.DefaultValue)}, 0.0, 1.0, 0.001)";
            var smooth = FaustControlSmoothing.Filter(ParameterSmoothing.Amplitude);
            definitions.Add($"{name}_u = max(0.0, min(1.0, ({slider} : {smooth}) + {signal})) : hbargraph(\"{meter}\", 0.0, 1.0);");
        }

        var u = control.Name + "_u";
        var binding = target.Binding;
        var nameBase = $"mapped_{node.Id:N}_{parameter.Id:N}";
        if (consumed.Add(address))
        {
            var zero = FaustNumber.Format(binding.AtZero);
            var one = FaustNumber.Format(binding.AtOne);
            var exponent = FaustNumber.Format(binding.Exponent);
            var curved = binding.Exponent == 1 ? u : $"pow({u}, {exponent})";
            // Retain the observable normalized control even for a constant
            // mapping. No second smoothing filter is applied to mapped samples.
            definitions.Add($"{nameBase} = attach((1.0 - {curved}) * {zero} + {curved} * {one}, {u});");
        }
        return nameBase;
    }

    public void VerifyNode(GraphNodeSnapshot node)
    {
        if (byNode[node.Id].Any(address => !consumed.Contains(address)))
            throw new FaustCompilationException($"Node {node.TypeKey} has an audio-rate macro target not consumed by its emitter.");
    }
}
