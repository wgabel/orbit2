using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;
using Orbit.Domain.Parameters;

namespace Orbit.Application.Graphs.Snapshots;

/// <summary>A read-only description of a node, detached from mutable graph state.</summary>
public sealed record GraphNodeSnapshot(
    Guid Id, string TypeKey, string Name,
    IReadOnlyList<ParameterSnapshot> Parameters,
    IReadOnlyList<PortDefinition> Inputs,
    IReadOnlyList<PortDefinition> Outputs)
{
    /// <summary>Present only on composite nodes; the snapshot contains no live graph reference.</summary>
    public CompositeSnapshot? Composite { get; init; }
    public bool SupportsMute { get; init; }
    public bool IsMuted { get; init; }
    public string? BypassInputKey { get; init; }
    public bool IsBypassed { get; init; }

    /// <summary>Evaluate conditional parameter activity from the committed node snapshot.</summary>
    public bool IsParameterActive(ParameterSnapshot parameter) =>
        IsActive(parameter.Definition.ActiveWhen);

    private bool IsActive(ParameterActivation? condition) => condition is null || condition.Matches(key =>
        Parameters.SingleOrDefault(selector => selector.Definition.Key == key)?.BaseValue);

    /// <summary>Inputs follow their explicit activation and their associated parameter, if any.</summary>
    public bool IsInputActive(Guid portId) =>
        IsActive(Inputs.SingleOrDefault(port => port.Id == portId)?.ActiveWhen) &&
        Parameters.All(parameter => parameter.ModulationPortId != portId || IsParameterActive(parameter));

    internal static GraphNodeSnapshot Capture(ISignalNode node, long revision) => new(
        node.Id, node.TypeKey, node.Name,
        Array.AsReadOnly(node.Parameters.Select(ParameterSnapshot.Capture).ToArray()),
        Array.AsReadOnly(node.Inputs.ToArray()),
        Array.AsReadOnly(node.Outputs.ToArray()))
        {
            SupportsMute = node.SupportsMute,
            IsMuted = node.IsMuted,
            BypassInputKey = node.BypassInputKey,
            IsBypassed = node.IsBypassed,
            Composite = node is ICompositeNode composite
                ? new CompositeSnapshot(GraphSnapshot.Capture(composite.InternalGraph, revision),
                    composite.InputRack.Id, composite.OutputRack.Id)
                    { Macros = node is CompositeNode concrete ? concrete.Macros : Array.Empty<CompositeMacroDefinition>() }
                : null
        };
}
