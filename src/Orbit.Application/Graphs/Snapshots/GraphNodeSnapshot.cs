using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;

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

    internal static GraphNodeSnapshot Capture(ISignalNode node, long revision) => new(
        node.Id, node.TypeKey, node.Name,
        Array.AsReadOnly(node.Parameters.Select(ParameterSnapshot.Capture).ToArray()),
        Array.AsReadOnly(node.Inputs.ToArray()),
        Array.AsReadOnly(node.Outputs.ToArray()))
        {
            Composite = node is ICompositeNode composite
                ? new CompositeSnapshot(GraphSnapshot.Capture(composite.InternalGraph, revision),
                    composite.InputRack.Id, composite.OutputRack.Id)
                : null
        };
}
