using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;

namespace Orbit.Audio.Playback;

/// <summary>Compares the patch structure independently of editable base values.
/// Connections and structural parameter choices are compiled DSP constants.</summary>
internal static class GraphTopology
{
    public static bool Matches(GraphSnapshot original, GraphSnapshot current)
    {
        if (original.Id != current.Id || original.Nodes.Count != current.Nodes.Count ||
            !original.Connections.OrderBy(edge => edge.Id).SequenceEqual(current.Connections.OrderBy(edge => edge.Id)))
            return false;

        var nodes = current.Nodes.ToDictionary(node => node.Id);
        return original.Nodes.All(node => nodes.TryGetValue(node.Id, out var candidate) &&
            node.TypeKey == candidate.TypeKey && node.Name == candidate.Name && node.SupportsMute == candidate.SupportsMute &&
            node.BypassInputKey == candidate.BypassInputKey &&
            node.Inputs.OrderBy(port => port.Id).SequenceEqual(candidate.Inputs.OrderBy(port => port.Id)) &&
            node.Outputs.OrderBy(port => port.Id).SequenceEqual(candidate.Outputs.OrderBy(port => port.Id)) &&
            node.Parameters.OrderBy(parameter => parameter.Id).Select(Structure)
                .SequenceEqual(candidate.Parameters.OrderBy(parameter => parameter.Id).Select(Structure)) &&
            CompositeMatches(node.Composite, candidate.Composite));
    }

    private static bool CompositeMatches(CompositeSnapshot? original, CompositeSnapshot? current) =>
        original is null ? current is null :
        current is not null && original.InputRackId == current.InputRackId &&
        original.OutputRackId == current.OutputRackId && MacroStructureMatches(original.Macros, current.Macros) &&
        Matches(original.Graph, current.Graph);

    // Macro base values are live Faust controls and names are presentation-only.
    // Associations, defaults and mappings alter the generated dynamic program.
    private static bool MacroStructureMatches(IReadOnlyList<CompositeMacroDefinition> original,
        IReadOnlyList<CompositeMacroDefinition> current)
    {
        if (original.Count != current.Count) return false;
        var byId = current.ToDictionary(macro => macro.Id);
        return original.All(macro => byId.TryGetValue(macro.Id, out var candidate) &&
            macro.DefaultValue == candidate.DefaultValue && macro.ModulationInputId == candidate.ModulationInputId &&
            macro.Mapping.Bindings.OrderBy(binding => binding.Target.NodeId).ThenBy(binding => binding.Target.ParameterId)
                .SequenceEqual(candidate.Mapping.Bindings.OrderBy(binding => binding.Target.NodeId)
                    .ThenBy(binding => binding.Target.ParameterId)));
    }

    private static ParameterSnapshot Structure(ParameterSnapshot parameter) =>
        parameter with
        {
            BaseValue = parameter.Definition.RequiresRecompile ? parameter.BaseValue : 0,
            Definition = parameter.Definition.DisplayScale is null ? parameter.Definition
                : parameter.Definition with { DisplayScale = null }
        };
}
