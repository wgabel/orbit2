using System.Text.Json;
using System.Text.Json.Serialization;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Application.Persistence;

/// <summary>Versioned data-only format. Current registered definitions and domain validation are authoritative.</summary>
public sealed class PatchDocumentSerializer
{
    public PatchDocument Capture(IPatchSession session, double tempo, Guid activeGraphId, SavedCanvas[] layouts) =>
        new(PatchDocument.CurrentVersion, session.PatchId, CaptureGraph(session.Snapshot), tempo, activeGraphId, layouts);

    public string Serialize(PatchDocument document) => JsonSerializer.Serialize(document, PatchJsonContext.Default.PatchDocument);

    public PatchDocument Deserialize(string json) => JsonSerializer.Deserialize(json, PatchJsonContext.Default.PatchDocument)
        ?? throw new JsonException("Missing patch document.");

    public IPatchSession Restore(PatchDocument document, NodeRegistry registry)
    {
        if (document.SchemaVersion != PatchDocument.CurrentVersion)
            throw new NotSupportedException($"Unsupported patch schema {document.SchemaVersion}.");
        _ = new MusicalTransport(document.BeatsPerMinute);
        var root = RestoreGraph(document.RootGraph, registry);
        return new PatchSession(registry, root, document.PatchId);
    }

    // Session history uses the same validated reconstruction path without JSON or storage.
    internal static SignalGraph RestoreSnapshot(GraphSnapshot snapshot, NodeRegistry registry) =>
        RestoreGraph(CaptureGraph(snapshot), registry);

    private static SignalGraph RestoreGraph(SavedGraph saved, NodeRegistry registry)
    {
        var root = new SignalGraph(saved.Id);
        RestoreGraph(saved, root, registry, new HashSet<Guid>());
        return root;
    }

    private static SavedGraph CaptureGraph(GraphSnapshot graph) => new(graph.Id,
        graph.Nodes.Where(node => node.TypeKey is not (InputRack.NodeType or OutputRack.NodeType)).Select(node =>
            new SavedNode(node.Id, node.TypeKey,
                node.Parameters.Select(parameter => new SavedParameter(parameter.Definition.Key, parameter.Id, parameter.BaseValue)).ToArray(),
                node.Inputs.Concat(node.Outputs).Select(port => new SavedPort(port.Key, port.Id)).ToArray(),
                node.Composite is { } composite ? new SavedComposite(node.Name, composite.InputRackId, composite.OutputRackId,
                    node.Inputs.Select(ToExposed).ToArray(),
                    composite.Graph.GetNode(composite.OutputRackId).Inputs.Select(ToExposed).ToArray(),
                    CaptureGraph(composite.Graph)) : null)).ToArray(), graph.Connections.ToArray());

    private static ExposedPort ToExposed(PortDefinition port) => new(port.Id, port.Name, port.SignalType, port.ChannelCount, port.FanIn);

    private static void RestoreGraph(SavedGraph saved, SignalGraph graph, NodeRegistry registry, HashSet<Guid> graphIds)
    {
        if (!graphIds.Add(saved.Id)) throw new GraphValidationException("Duplicate graph identity.");
        foreach (var node in saved.Nodes)
        {
            if (node.Composite is { } composite)
            {
                if (node.TypeKey != CompositeNode.NodeType) throw new GraphValidationException("Invalid composite type.");
                var restored = new CompositeNode(node.Id, composite.Name, composite.Inputs, composite.Outputs,
                    composite.Graph.Id, composite.InputRackId, composite.OutputRackId);
                RestoreGraph(composite.Graph, restored.InternalGraph, registry, graphIds);
                graph.AddNode(restored);
            }
            else
            {
                if (node.TypeKey is CompositeNode.NodeType or InputRack.NodeType or OutputRack.NodeType ||
                    registry.Create(node.TypeKey, node.Id) is not SignalNode leaf)
                    throw new GraphValidationException("Invalid leaf node type.");
                leaf.RestoreIdentity(node.Ports.ToDictionary(port => port.Key, port => port.Id),
                    node.Parameters.ToDictionary(parameter => parameter.Key, parameter => (parameter.Id, parameter.BaseValue)));
                graph.AddNode(leaf);
            }
        }
        foreach (var connection in saved.Connections) graph.AddConnection(connection);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PatchDocument))]
internal partial class PatchJsonContext : JsonSerializerContext { }
