using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.AgentKit.Exporter;

/// <summary>Portable native templates captured from registered composite factories.
/// Exported IDs are template-local, never suitable for direct instance reuse.</summary>
public static class CompositePresetExporter
{
    public const int FormatVersion = 1;
    public const string FileName = "composite-presets.json";

    public static string Export(NodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var serializer = new PatchDocumentSerializer();
        var presets = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var key in registry.RegisteredTypes)
        {
            if (registry.Create(key) is not ICompositeNode)
                continue;

            using var session = new PatchSession(registry);
            session.Execute(new AddNodeCommand(session.GraphId, key));
            var captured = serializer.Capture(session, MusicalTransport.DefaultTempo, session.GraphId, []);
            var document = NormalizeTemplate(captured);
            var json = serializer.Serialize(document);
            // Reject incomplete/invalid remapping before publishing metadata.
            using var restored = serializer.Restore(serializer.Deserialize(json), registry);
            using var parsed = JsonDocument.Parse(json);
            presets.Add(key, parsed.RootElement.Clone());
        }

        return JsonSerializer.Serialize(new
        {
            compositePresetFormatVersion = FormatVersion,
            patchSchemaVersion = PatchDocument.CurrentVersion,
            presets
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Normalize a captured layout-free root template, preserving references
    /// and mirrored boundary identities. Does not remap arbitrary user layouts.</summary>
    public static PatchDocument NormalizeTemplate(PatchDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.CanvasLayouts.Length != 0 || document.ActiveGraphId != document.RootGraph.Id)
            throw new ArgumentException("Expected a root template without saved layouts.", nameof(document));

        var identities = new Dictionary<Guid, Guid>();
        var next = 0;
        Guid Map(Guid id)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Template identity cannot be empty.", nameof(document));
            if (!identities.TryGetValue(id, out var mapped))
            {
                mapped = new Guid(++next, 0, 0, new byte[8]);
                identities.Add(id, mapped);
            }
            return mapped;
        }

        PortAddress MapPort(PortAddress address) => new(Map(address.NodeId), Map(address.PortId));
        ParameterAddress MapParameter(ParameterAddress address) => new(Map(address.NodeId), Map(address.ParameterId));
        ExposedPort MapBoundary(ExposedPort port) => port with { Id = Map(port.Id) };
        SavedPort MapSavedPort(SavedPort port, bool composite)
        {
            var id = Map(port.Id);
            // The native projected boundary key includes the instance GUID.
            // Leaf keys are stable catalog identifiers and must not be changed.
            if (composite && port.Key != $"boundary-{port.Id:N}")
                throw new InvalidOperationException("Native composite boundary key contract changed.");
            return port with { Id = id, Key = composite ? $"boundary-{id:N}" : port.Key };
        }

        SavedGraph MapGraph(SavedGraph graph) => graph with
        {
            Id = Map(graph.Id),
            Nodes = graph.Nodes.Select(MapNode).ToArray(),
            Connections = graph.Connections.Select(edge => edge with
            {
                Id = Map(edge.Id),
                Source = MapPort(edge.Source),
                Destination = MapPort(edge.Destination)
            }).ToArray()
        };

        SavedNode MapNode(SavedNode node) => node with
        {
            Id = Map(node.Id),
            Parameters = node.Parameters.Select(parameter => parameter with { Id = Map(parameter.Id) }).ToArray(),
            Ports = node.Ports.Select(port => MapSavedPort(port, node.Composite is not null)).ToArray(),
            Composite = node.Composite is { } composite ? composite with
            {
                InputRackId = Map(composite.InputRackId),
                OutputRackId = Map(composite.OutputRackId),
                Inputs = composite.Inputs.Select(MapBoundary).ToArray(),
                Outputs = composite.Outputs.Select(MapBoundary).ToArray(),
                Graph = MapGraph(composite.Graph),
                Macros = composite.Macros.Select(macro => macro with
                {
                    Id = Map(macro.Id),
                    ModulationInputId = macro.ModulationInputId is Guid input ? Map(input) : null,
                    Bindings = macro.Bindings.Select(binding => binding with
                    {
                        Target = MapParameter(binding.Target)
                    }).ToArray()
                }).ToArray()
            } : null
        };

        var root = MapGraph(document.RootGraph);
        return document with { PatchId = Map(document.PatchId), RootGraph = root, ActiveGraphId = root.Id };
    }
}
