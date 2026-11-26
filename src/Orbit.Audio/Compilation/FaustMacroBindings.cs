using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation;

/// <summary>One normalized DSP control per reachable macro; target bindings
/// replace only the base expression, never a native additive modulation input.</summary>
internal sealed class FaustMacroBindings(
    IReadOnlyDictionary<ParameterAddress, GraphFlattener.MacroTarget> targets,
    IReadOnlyDictionary<PortAddress, string> signals,
    List<string> definitions)
{
    private readonly Dictionary<MacroControlAddress, FaustMacroControl> controls = [];
    private readonly Dictionary<ParameterAddress, string> mappedBases = [];
    private readonly ILookup<Guid, ParameterAddress> byNode = targets.Keys.ToLookup(address => address.NodeId);

    public IReadOnlyList<FaustMacroControl> Manifest => Array.AsReadOnly(controls.Values.ToArray());

    public string? ResolveBase(GraphNodeSnapshot node, ParameterSnapshot parameter)
    {
        var address = new ParameterAddress(node.Id, parameter.Id);
        if (!targets.TryGetValue(address, out var target)) return null;
        if (mappedBases.TryGetValue(address, out var mapped)) return mapped;
        // The note sequencer also consumes authored scalar values in the host
        // pattern scheduler. Until a sample-coherent contract exists, reject
        // its dynamic targets rather than allowing split DSP/host semantics.
        if (node.TypeKey == NoteSequencerGenerator.NodeType || parameter.Definition.Choices is not null)
            throw new FaustCompilationException($"Audio-rate macro target '{parameter.Definition.Key}' is not supported on {node.TypeKey}.");

        var source = target.Source;
        var identity = new MacroControlAddress(source.GraphId, source.CompositeId, source.MacroId);
        if (!controls.TryGetValue(identity, out var control))
        {
            if (!signals.TryGetValue(source.Signal, out var signal))
                throw new FaustCompilationException("Missing compiled macro boundary signal.");
            var name = $"macro_{identity.GraphId:N}_{identity.CompositeId:N}_{identity.MacroId:N}";
            var meter = name + "_effective";
            control = new FaustMacroControl(identity, name, source.BaseValue, meter);
            controls.Add(identity, control);
            var smoothing = FaustControlSmoothing.Filter(ParameterSmoothing.Amplitude);
            definitions.Add($"{name} = hslider(\"{name}\", {FaustNumber.Format(source.DefaultValue)}, 0.0, 1.0, 0.001) : {smoothing};");
            definitions.Add($"{name}_u = max(0.0, min(1.0, {name} + {signal}));");
            definitions.Add($"{name}_observed = {name}_u : hbargraph(\"{meter}\", 0.0, 1.0);");
        }

        var binding = target.Binding;
        var curve = $"pow({control.Name}_u, {FaustNumber.Format(binding.Exponent)})";
        var value = $"((1.0 - {curve}) * ({FaustNumber.Format(binding.AtZero)}) + {curve} * ({FaustNumber.Format(binding.AtOne)}))";
        mapped = $"macro_base_{address.NodeId:N}_{address.ParameterId:N}";
        // Keep the shared effective meter alive even for constant curves.
        definitions.Add($"{mapped} = attach({value}, {control.Name}_observed);");
        mappedBases.Add(address, mapped);
        return mapped;
    }

    public void VerifyConsumed(GraphNodeSnapshot node)
    {
        foreach (var address in byNode[node.Id])
            if (!mappedBases.ContainsKey(address))
                throw new FaustCompilationException($"A reachable macro target on {node.TypeKey} has no supported DSP base expression.");
    }
}
