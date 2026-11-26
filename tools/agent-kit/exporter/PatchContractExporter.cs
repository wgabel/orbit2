using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Orbit.Application.Graphs;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;
using Orbit.Presentation.Canvas;

namespace Orbit.AgentKit.Exporter;

/// <summary>Exports the native persistence DTO shape and deterministic, native-checked
/// leaf patch fixtures. Schema alone cannot express native graph invariants.</summary>
public static class PatchContractExporter
{
    public const int FormatVersion = 1;

    public static IReadOnlyDictionary<string, string> Export()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        var serializer = new PatchDocumentSerializer();
        var document = CreateExample(registry);
        var accepted = serializer.Serialize(document);
        // Check the same serialized payload consumers will receive, not just the DTO.
        var imported = serializer.Deserialize(accepted);
        using var session = serializer.Restore(imported, registry);
        using var navigation = new CanvasNavigationService(session, layouts: imported.CanvasLayouts,
            activeGraphId: imported.ActiveGraphId);

        var fixtures = new (string File, PatchDocument Document, string? Rejection)[]
        {
            ("valid-leaf.json", document, null),
            ("invalid-version.json", document with { SchemaVersion = PatchDocument.CurrentVersion + 1 }, "schema-version"),
            ("invalid-tempo.json", document with { BeatsPerMinute = MusicalTransport.MinimumTempo - 1 }, "tempo"),
            ("invalid-zoom.json", document with { CanvasLayouts = [document.CanvasLayouts[0] with
                { Zoom = CanvasViewport.MaximumZoom + 1 }] }, "viewport"),
            ("invalid-active-graph.json", document with { ActiveGraphId = Guid.Empty }, "active-graph"),
            ("invalid-endpoint.json", document with { RootGraph = document.RootGraph with
                { Connections = [document.RootGraph.Connections[0] with
                    { Destination = new PortAddress(Guid.Empty, Guid.Empty) }] } }, "endpoint")
        };

        // Native-accepted structural fixtures are deliberately separate from the
        // root-leaf parity manifest: offline support must not be inferred from schema.
        var deferred = new[]
        {
            (File: "deferred-static-macro.json", Document: CreateMacroExample(document, false)),
            (File: "deferred-macro-input.json", Document: CreateMacroExample(document, true))
        };
        foreach (var fixture in deferred)
        {
            var native = serializer.Deserialize(serializer.Serialize(fixture.Document));
            using var restored = serializer.Restore(native, registry);
            using var canvas = new CanvasNavigationService(restored, layouts: native.CanvasLayouts,
                activeGraphId: native.ActiveGraphId);
        }

        var files = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositePresetExporter.FileName] = CompositePresetExporter.Export(registry),
            ["patch-schema.json"] = ExportSchema(),
            ["patch-contract.json"] = JsonSerializer.Serialize(new
            {
                patchContractFormatVersion = FormatVersion,
                patchSchemaVersion = PatchDocument.CurrentVersion,
                structuralSchema = "patch-schema.json",
                compositePresets = new
                {
                    file = CompositePresetExporter.FileName,
                    formatVersion = CompositePresetExporter.FormatVersion,
                    offlineAuthoring = false,
                    offlineEditing = false
                },
                tempo = new { minimum = MusicalTransport.MinimumTempo, maximum = MusicalTransport.MaximumTempo,
                    defaultValue = MusicalTransport.DefaultTempo },
                zoom = new { minimum = CanvasViewport.MinimumZoom, maximum = CanvasViewport.MaximumZoom,
                    defaultValue = CanvasViewport.Initial.Zoom },
                overview = new
                {
                    maxTitleLength = SavedPatchOverview.MaxTitleLength,
                    maxTextLength = SavedPatchOverview.MaxTextLength,
                    maxTags = SavedPatchOverview.MaxTags,
                    maxTagLength = SavedPatchOverview.MaxTagLength
                },
                canvasPreferences = new
                {
                    annotationVisibility = Enum.GetValues<AnnotationVisibility>().Select(value => (int)value),
                    connectionLabels = Enum.GetValues<ConnectionLabelMode>().Select(value => (int)value)
                },
                deferredFeatures = new
                {
                    compositeMacroModulation = new
                    {
                        field = "modulationInputId",
                        nativePersistence = true,
                        runtimeModulation = false,
                        offlineAuthoring = false,
                        offlineEditing = false,
                        rejection = "unsupported-composite",
                        roadmap = "7.4.6-S4d / 7.7-S9"
                    }
                },
                deferredFixtures = deferred.Select(fixture => new
                {
                    file = fixture.File,
                    nativeAccepted = true,
                    offlineAccepted = false,
                    offlineRejection = "unsupported-composite"
                }).ToArray(),
                fixtures = fixtures.Select(fixture => new
                {
                    file = fixture.File,
                    accepted = fixture.Rejection is null,
                    rejection = fixture.Rejection
                }).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true })
        };

        foreach (var fixture in fixtures)
            files.Add(fixture.File, serializer.Serialize(fixture.Document));
        foreach (var fixture in deferred)
            files.Add(fixture.File, serializer.Serialize(fixture.Document));
        return files;
    }

    private static PatchDocument CreateMacroExample(PatchDocument leaf, bool associated)
    {
        // IDs in this range are reserved for the structural fixture; leaf IDs
        // are assigned below from 1. Both variants use identical topology.
        Guid Id(int value) => new(value, 0, 0, new byte[8]);
        var oscillator = leaf.RootGraph.Nodes.Single(n => n.TypeKey == OscillatorGenerator.NodeType);
        var master = leaf.RootGraph.Nodes.Single(n => n.TypeKey == MasterOutput.NodeType);
        var input = new ExposedPort(Id(1001), "Modulation", SignalType.Modulation);
        var output = new ExposedPort(Id(1002), "Audio", SignalType.Audio);
        var wrapperId = Id(1003);
        var childId = Id(1004);
        var inputRack = Id(1005);
        var outputRack = Id(1006);
        var child = new SavedGraph(childId, [oscillator], [
            new SignalConnection(Id(1007),
                new PortAddress(oscillator.Id, oscillator.Ports.Single(p => p.Key == "signal-out").Id),
                new PortAddress(outputRack, output.Id), 1)
        ]);
        var frequency = oscillator.Parameters.Single(p => p.Key == "frequency");
        var macro = new SavedCompositeMacro(Id(1008), "Size", 0.5, 0.5,
            [new(new(oscillator.Id, frequency.Id), 220, 440, 2)])
        { ModulationInputId = associated ? input.Id : null };
        var wrapper = new SavedNode(wrapperId, CompositeNode.NodeType, [], [],
            new SavedComposite("Macro reference", inputRack, outputRack, [input], [output], child)
            { Macros = [macro] });
        return leaf with
        {
            RootGraph = leaf.RootGraph with
            {
                Nodes = [wrapper, master],
                Connections = [leaf.RootGraph.Connections[0] with { Source = new PortAddress(wrapperId, output.Id) }]
            },
            CanvasLayouts = [leaf.CanvasLayouts[0] with
            {
                Positions = [new SavedNodePosition(wrapperId, 0, 0), new SavedNodePosition(master.Id, 320, 0)]
            }]
        };
    }

    private static string ExportSchema()
    {
        // The production serializer uses the same camelCase DTO contract and
        // default numeric enums. Reflect DTOs in this tooling process only.
        // Native Restore/navigation enforce semantic rules beyond this shape.
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        var schema = options.GetJsonSchemaAsNode(typeof(PatchDocument)).AsObject();
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        schema["title"] = "Orbit 2 native patch structural schema";
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static PatchDocument CreateExample(NodeRegistry registry)
    {
        // Factories allocate fresh identities; the fixture substitutes stable,
        // distinct GUIDs without making assumptions about parameter/port counts.
        var next = 0;
        Guid Id() => new(++next, 0, 0, new byte[8]);
        SavedNode Leaf(string key)
        {
            var node = registry.Create(key);
            return new SavedNode(Id(), node.TypeKey,
                node.Parameters.Select(p => new SavedParameter(p.Definition.Key, Id(), p.BaseValue)).ToArray(),
                node.Inputs.Concat(node.Outputs).Select(p => new SavedPort(p.Key, Id())).ToArray(), null)
            { IsMuted = node.IsMuted, IsBypassed = node.IsBypassed };
        }

        var graphId = Id();
        var patchId = Id();
        var oscillator = Leaf(OscillatorGenerator.NodeType);
        var output = Leaf(MasterOutput.NodeType);
        var connection = new SignalConnection(Id(),
            new PortAddress(oscillator.Id, oscillator.Ports.Single(p => p.Key == "signal-out").Id),
            new PortAddress(output.Id, output.Ports.Single(p => p.Key == "audio-in").Id), 0.2);
        var graph = new SavedGraph(graphId, [oscillator, output], [connection]);
        var layout = new SavedCanvas(graphId, 0, 0, CanvasViewport.Initial.Zoom,
            [new SavedNodePosition(oscillator.Id, 0, 0), new SavedNodePosition(output.Id, 320, 0)]);
        return new PatchDocument(PatchDocument.CurrentVersion, patchId, graph, MusicalTransport.DefaultTempo,
            graphId, [layout]);
    }
}
