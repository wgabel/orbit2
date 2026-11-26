using System.Text.Json;
using Orbit.AgentKit.Exporter;
using Orbit.Application.Graphs;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Timing;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class AgentKitPatchContractTests
{
    [Fact]
    public void ContractAndFixturesRegenerateAndMatchNativeShape()
    {
        var files = PatchContractExporter.Export();
        var repeat = PatchContractExporter.Export();
        Assert.Equal(files.Keys.ToArray(), repeat.Keys.ToArray());
        foreach (var name in files.Keys)
            Assert.Equal(files[name], repeat[name]);

        using var contract = JsonDocument.Parse(files["patch-contract.json"]);
        var manifest = contract.RootElement;
        Assert.Equal(PatchDocument.CurrentVersion, manifest.GetProperty("patchSchemaVersion").GetInt32());
        Assert.Equal(MusicalTransport.MinimumTempo, manifest.GetProperty("tempo").GetProperty("minimum").GetDouble());
        Assert.Equal(MusicalTransport.MaximumTempo, manifest.GetProperty("tempo").GetProperty("maximum").GetDouble());
        Assert.Equal(CanvasViewport.MinimumZoom, manifest.GetProperty("zoom").GetProperty("minimum").GetDouble());
        Assert.Equal(CanvasViewport.MaximumZoom, manifest.GetProperty("zoom").GetProperty("maximum").GetDouble());

        using var schema = JsonDocument.Parse(files["patch-schema.json"]);
        using var patch = JsonDocument.Parse(files["valid-leaf.json"]);
        var properties = schema.RootElement.GetProperty("properties");
        Assert.Equal(patch.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            properties.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal("integer", properties.GetProperty("schemaVersion").GetProperty("type").GetString());
        Assert.Equal("number", properties.GetProperty("beatsPerMinute").GetProperty("type").GetString());

        var serializer = new PatchDocumentSerializer();
        var document = serializer.Deserialize(files["valid-leaf.json"]);
        using var session = serializer.Restore(document, BuiltInNodes.CreateEditorRegistry());
        using var navigation = new CanvasNavigationService(session, layouts: document.CanvasLayouts,
            activeGraphId: document.ActiveGraphId);
        Assert.Equal(2, session.Snapshot.Nodes.Count);
        Assert.Single(session.Snapshot.Connections);
        Assert.Equal(document.RootGraph.Id, navigation.ActiveCanvas.Snapshot.GraphId);
        Assert.Equal(document.CanvasLayouts[0].Zoom, navigation.ActiveCanvas.Snapshot.Viewport.Zoom);
        Assert.Equal(files["valid-leaf.json"], serializer.Serialize(serializer.Capture(session,
            document.BeatsPerMinute, document.ActiveGraphId, navigation.CaptureLayouts(), document.Overview)));
    }

    [Fact]
    public void NativeMacroAssociationIsExportedButOfflineSupportIsExplicitlyDeferred()
    {
        var files = PatchContractExporter.Export();
        using var manifest = JsonDocument.Parse(files["patch-contract.json"]);
        var feature = manifest.RootElement.GetProperty("deferredFeatures").GetProperty("compositeMacroModulation");
        Assert.Equal("modulationInputId", feature.GetProperty("field").GetString());
        Assert.True(feature.GetProperty("nativePersistence").GetBoolean());
        Assert.False(feature.GetProperty("runtimeModulation").GetBoolean());
        Assert.False(feature.GetProperty("offlineAuthoring").GetBoolean());
        Assert.False(feature.GetProperty("offlineEditing").GetBoolean());
        Assert.Contains("\"modulationInputId\"", files["patch-schema.json"]);
        var serializer = new PatchDocumentSerializer();
        var registry = BuiltInNodes.CreateEditorRegistry();
        foreach (var fixture in manifest.RootElement.GetProperty("deferredFixtures").EnumerateArray())
        {
            var name = fixture.GetProperty("file").GetString()!;
            Assert.True(fixture.GetProperty("nativeAccepted").GetBoolean());
            Assert.False(fixture.GetProperty("offlineAccepted").GetBoolean());
            Assert.Equal("unsupported-composite", fixture.GetProperty("offlineRejection").GetString());
            var json = files[name];
            var document = serializer.Deserialize(json);
            using var session = serializer.Restore(document, registry);
            using var navigation = new CanvasNavigationService(session, layouts: document.CanvasLayouts,
                activeGraphId: document.ActiveGraphId);
            var wrapper = session.Snapshot.Nodes.Single(n => n.Composite is not null);
            var macro = Assert.Single(wrapper.Composite!.Macros);
            var associated = name == "deferred-macro-input.json";
            if (associated)
                Assert.Equal(wrapper.Inputs.Single().Id, macro.ModulationInputId);
            else
                Assert.Null(macro.ModulationInputId);
            Assert.Equal(associated, json.Contains("\"modulationInputId\"", StringComparison.Ordinal));
            var captured = serializer.Capture(session, document.BeatsPerMinute, document.ActiveGraphId,
                navigation.CaptureLayouts(), document.Overview);
            var saved = Assert.Single(captured.RootGraph.Nodes.Single(n => n.Id == wrapper.Id).Composite!.Macros);
            Assert.Equal(macro.ModulationInputId, saved.ModulationInputId);
            Assert.Equal(macro.Mapping.Bindings.ToArray(), saved.Bindings);
        }
    }

    [Fact]
    public void ManifestRejectionsMatchNativeImport()
    {
        var files = PatchContractExporter.Export();
        using var manifest = JsonDocument.Parse(files["patch-contract.json"]);
        var serializer = new PatchDocumentSerializer();
        var registry = BuiltInNodes.CreateEditorRegistry();

        foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var document = serializer.Deserialize(files[fixture.GetProperty("file").GetString()!]);
            void Import()
            {
                using var session = serializer.Restore(document, registry);
                using var navigation = new CanvasNavigationService(session, layouts: document.CanvasLayouts,
                    activeGraphId: document.ActiveGraphId);
            }

            switch (fixture.GetProperty("rejection").GetString())
            {
                case null:
                    Assert.True(fixture.GetProperty("accepted").GetBoolean());
                    Import();
                    break;
                case "schema-version":
                    Assert.Throws<NotSupportedException>(Import);
                    break;
                case "tempo":
                case "viewport":
                    Assert.Throws<ArgumentOutOfRangeException>(Import);
                    break;
                case "active-graph":
                    Assert.Throws<InvalidOperationException>(Import);
                    break;
                case "endpoint":
                    Assert.Throws<GraphValidationException>(Import);
                    break;
                default:
                    throw new InvalidOperationException("Unknown fixture expectation.");
            }
        }
    }
}
