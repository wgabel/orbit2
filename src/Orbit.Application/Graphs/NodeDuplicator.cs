using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>
/// Deep-copies a committed node through the validated patch reconstruction path.
/// Shared boundary IDs, racks, inner nodes and inner connections are remapped
/// consistently; connections to other nodes on the parent canvas are excluded.
/// </summary>
internal static class NodeDuplicator
{
    public static ISignalNode Duplicate(GraphNodeSnapshot source, Guid duplicateId, NodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(registry);
        if (duplicateId == Guid.Empty || duplicateId == source.Id)
            throw new GraphValidationException("The duplicate needs a new node identity.");
        if (source.TypeKey is InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType)
            throw new GraphValidationException("Boundary racks cannot be duplicated independently.");

        var identities = new Dictionary<Guid, Guid> { [source.Id] = duplicateId };
        Guid Remap(Guid original)
        {
            if (!identities.TryGetValue(original, out var replacement))
            {
                replacement = Guid.NewGuid();
                identities.Add(original, replacement);
            }
            return replacement;
        }

        SavedGraph CopyGraph(SavedGraph graph) => new(
            Remap(graph.Id),
            graph.Nodes.Select(CopyNode).ToArray(),
            graph.Connections.Select(connection => connection with
            {
                Id = Remap(connection.Id),
                Source = new PortAddress(Remap(connection.Source.NodeId), Remap(connection.Source.PortId)),
                Destination = new PortAddress(Remap(connection.Destination.NodeId), Remap(connection.Destination.PortId))
            }).ToArray());

        SavedNode CopyNode(SavedNode node) => new(
            Remap(node.Id), node.TypeKey,
            node.Parameters.Select(parameter => parameter with { Id = Remap(parameter.Id) }).ToArray(),
            node.Ports.Select(port => port with { Id = Remap(port.Id) }).ToArray(),
            node.Composite is { } composite ? new SavedComposite(
                composite.Name, Remap(composite.InputRackId), Remap(composite.OutputRackId),
                composite.Inputs.Select(port => port with { Id = Remap(port.Id) }).ToArray(),
                composite.Outputs.Select(port => port with { Id = Remap(port.Id) }).ToArray(),
                CopyGraph(composite.Graph))
                {
                    Macros = composite.Macros.Select(macro => macro with
                    {
                        Id = Remap(macro.Id),
                        ModulationInputId = macro.ModulationInputId is Guid input ? Remap(input) : null,
                        Bindings = macro.Bindings.Select(binding => binding with
                        {
                            Target = new(Remap(binding.Target.NodeId), Remap(binding.Target.ParameterId))
                        }).ToArray()
                    }).ToArray()
                } : null)
        {
            IsMuted = node.IsMuted,
            IsBypassed = node.IsBypassed
        };

        // Construct a graph containing only the requested node. This keeps
        // external connections out, including when the source is a composite.
        var captured = PatchDocumentSerializer.CaptureGraph(new GraphSnapshot(Guid.NewGuid(), 0, [source], []));
        var holder = PatchDocumentSerializer.RestoreGraph(CopyGraph(captured), registry);
        var duplicate = holder.GetNode(duplicateId);
        holder.RemoveNode(duplicateId); // Detach ownership before adding to the live graph.
        return duplicate;
    }
}
