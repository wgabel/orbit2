using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;
using Orbit.Presentation.Canvas;

namespace Orbit.AgentKit.Exporter;

/// <summary>Portable native composite factory snapshots. Template identities are
/// local placeholders, never identities for a newly instantiated patch node.</summary>
public static class CompositeTemplateExporter
{
    public const int FormatVersion = 1;

    public static string Export()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        var serializer = new PatchDocumentSerializer();
        var entries = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var key in registry.RegisteredTypes)
        {
            if (registry.Create(key) is not ICompositeNode)
                continue;
            using var session = new PatchSession(registry);
            session.Execute(new AddNodeCommand(session.GraphId, key));
            var captured = serializer.Capture(session, MusicalTransport.DefaultTempo, session.GraphId, []);
            var template = NormalizeTemplate(captured.RootGraph.Nodes.Single());
            var patch = WrapTemplate(template);
            var json = serializer.Serialize(patch);
            var loaded = serializer.Deserialize(json);
            using var restored = serializer.Restore(loaded, registry);
            using var navigation = new CanvasNavigationService(restored, layouts: loaded.CanvasLayouts,
                activeGraphId: loaded.ActiveGraphId);
            // Extract the node using the actual persistence wire format, not a
            // parallel JSON serializer for SavedNode.
            using var parsed = JsonDocument.Parse(json);
            entries.Add(key, new
            {
                creationKey = key,
                persistedTypeKey = template.TypeKey,
                label = registry.Describe(key),
                node = parsed.RootElement.GetProperty("rootGraph").GetProperty("nodes")[0].Clone()
            });
        }
        return JsonSerializer.Serialize(new
        {
            compositeTemplatesFormatVersion = FormatVersion,
            patchSchemaVersion = PatchDocument.CurrentVersion,
            templates = entries
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Remap the complete known native SavedNode contract using one
    /// identity map for the hierarchy, including mirrored boundary sockets.</summary>
    public static SavedNode NormalizeTemplate(SavedNode node)
    {
        if (node.Composite is null)
            throw new ArgumentException("Expected a composite template.", nameof(node));
        var ids = new Dictionary<Guid, Guid>();
        Guid Map(Guid id)
        {
            if (id == Guid.Empty)
                throw new InvalidOperationException("Empty template identity.");
            if (!ids.TryGetValue(id, out var mapped))
            {
                mapped = new Guid(ids.Count + 1, 0, 0, new byte[8]);
                ids.Add(id, mapped);
            }
            return mapped;
        }

        SavedGraph Graph(SavedGraph graph) => graph with
        {
            Id = Map(graph.Id),
            Nodes = graph.Nodes.Select(Node).ToArray(),
            Connections = graph.Connections.Select(edge => edge with
            {
                Id = Map(edge.Id),
                Source = new PortAddress(Map(edge.Source.NodeId), Map(edge.Source.PortId)),
                Destination = new PortAddress(Map(edge.Destination.NodeId), Map(edge.Destination.PortId))
            }).ToArray()
        };

        SavedNode Node(SavedNode value) => value with
        {
            Id = Map(value.Id),
            Parameters = value.Parameters.Select(parameter => parameter with { Id = Map(parameter.Id) }).ToArray(),
            Ports = value.Ports.Select(port => port with
            {
                Id = Map(port.Id),
                Key = value.Composite is null ? port.Key : $"boundary-{Map(port.Id):N}"
            }).ToArray(),
            Composite = value.Composite is { } composite ? composite with
            {
                InputRackId = Map(composite.InputRackId),
                OutputRackId = Map(composite.OutputRackId),
                Inputs = composite.Inputs.Select(port => port with { Id = Map(port.Id) }).ToArray(),
                Outputs = composite.Outputs.Select(port => port with { Id = Map(port.Id) }).ToArray(),
                Graph = Graph(composite.Graph),
                Macros = composite.Macros.Select(macro => macro with
                {
                    Id = Map(macro.Id),
                    ModulationInputId = macro.ModulationInputId is Guid input ? Map(input) : null,
                    Bindings = macro.Bindings.Select(binding => binding with
                    {
                        Target = new ParameterAddress(Map(binding.Target.NodeId), Map(binding.Target.ParameterId))
                    }).ToArray()
                }).ToArray()
            } : null
        };
        return Node(node);
    }

    public static PatchDocument WrapTemplate(SavedNode template)
    {
        // Reserved outside the positive sequential identity namespace.
        var graphId = new Guid(-1, 0, 0, new byte[8]);
        var patchId = new Guid(-2, 0, 0, new byte[8]);
        return new PatchDocument(PatchDocument.CurrentVersion, patchId,
            new SavedGraph(graphId, [template], []), MusicalTransport.DefaultTempo, graphId, []);
    }
}
