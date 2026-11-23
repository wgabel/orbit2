using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;

namespace Orbit.Presentation.Canvas;

/// <summary>
/// Pure, registry-independent projection for compact leaf node controls.
/// Ports remain authoritative: every modulation input is paired by its ID,
/// not by a localized label. New leaf types receive the safe generic fallback.
/// </summary>
public static class CompactNodePresentation
{
    public static bool IsExpandable(GraphNodeSnapshot node) =>
        node.Composite is null &&
        node.TypeKey is not (InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType);

    public static ParameterSnapshot? ForInput(GraphNodeSnapshot node, Guid portId) =>
        node.Parameters.FirstOrDefault(parameter => parameter.ModulationPortId == portId);

    /// <summary>Unpaired controls that must be reachable before expanding.</summary>
    public static IReadOnlyList<ParameterSnapshot> AdditionalParameters(GraphNodeSnapshot node)
    {
        if (!IsExpandable(node)) return [];

        var parameters = node.Parameters.Where(parameter => !parameter.ModulationPortId.HasValue);
        if (node.TypeKey == NoteSequencerGenerator.NodeType)
        {
            var essential = new HashSet<string>(StringComparer.Ordinal)
            {
                NoteSequencerGenerator.ArticulationKey,
                NoteSequencerGenerator.LoopKey,
                NoteSequencerGenerator.CountKey,
                NoteSequencerGenerator.TimingKey,
                NoteSequencerGenerator.DivisionKey,
                NoteSequencerGenerator.SecondsKey,
                NoteSequencerGenerator.ReferenceKey,
                NoteSequencerGenerator.InitialPitchKey
            };
            parameters = parameters.Where(parameter => essential.Contains(parameter.Definition.Key));
        }
        // Always expose unmatched modulatable parameters, even if inactive.
        return parameters.Where(parameter =>
            parameter.Definition.CanModulate || node.IsParameterActive(parameter)).ToArray();
    }
}
