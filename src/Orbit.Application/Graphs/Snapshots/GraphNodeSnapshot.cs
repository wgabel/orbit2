using Orbit.Domain.Nodes;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs.Snapshots;

/// <summary>A read-only description of a node, detached from mutable graph state.</summary>
public sealed record GraphNodeSnapshot(
    Guid Id, string TypeKey, string Name,
    IReadOnlyList<ParameterSnapshot> Parameters,
    IReadOnlyList<PortDefinition> Inputs,
    IReadOnlyList<PortDefinition> Outputs)
{
    internal static GraphNodeSnapshot Capture(ISignalNode node) => new(
        node.Id, node.TypeKey, node.Name,
        Array.AsReadOnly(node.Parameters.Select(ParameterSnapshot.Capture).ToArray()),
        Array.AsReadOnly(node.Inputs.ToArray()),
        Array.AsReadOnly(node.Outputs.ToArray()));
}
