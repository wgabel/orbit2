using System.Text.Json;
using Orbit.AgentKit.Exporter;
using Orbit.Application.Graphs;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Composites;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class AgentKitCompositePresetTests
{
    [Fact]
    public void ExportsEveryRegisteredCompositeAsDeterministicNativeTemplate()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        var json = CompositePresetExporter.Export(registry);
        Assert.Equal(json, CompositePresetExporter.Export(BuiltInNodes.CreateEditorRegistry()));
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal(CompositePresetExporter.FormatVersion, root.GetProperty("compositePresetFormatVersion").GetInt32());
        Assert.Equal(PatchDocument.CurrentVersion, root.GetProperty("patchSchemaVersion").GetInt32());
        var presets = root.GetProperty("presets");
        var expected = registry.RegisteredTypes.Where(key => registry.Create(key) is ICompositeNode).ToArray();
        Assert.Equal(expected, presets.EnumerateObject().Select(item => item.Name).ToArray());
        Assert.Contains(DroneTemplate.LayeredPresetKey, expected);
        Assert.Contains(ImpactTemplate.PresetKey, expected);

        foreach (var entry in presets.EnumerateObject())
        {
            var serializer = new PatchDocumentSerializer();
            var document = serializer.Deserialize(entry.Value.GetRawText());
            var wrapper = Assert.Single(document.RootGraph.Nodes);
            Assert.Equal(CompositeNode.NodeType, wrapper.TypeKey);
            Assert.NotNull(wrapper.Composite);
            Assert.Equal(registry.Create(entry.Name).Name, wrapper.Composite!.Name);
            Assert.Empty(document.CanvasLayouts);
            using var session = serializer.Restore(document, registry);
            using var navigation = new CanvasNavigationService(session, layouts: document.CanvasLayouts,
                activeGraphId: document.ActiveGraphId);
            navigation.OpenChild(wrapper.Id);
            Assert.Equal(wrapper.Composite.Graph.Id, navigation.ActiveCanvas.Snapshot.GraphId);
            var recaptured = serializer.Capture(session, document.BeatsPerMinute, session.GraphId, []);
            Assert.Equal(serializer.Serialize(document), serializer.Serialize(recaptured));
            foreach (var port in wrapper.Ports)
                Assert.Equal($"boundary-{port.Id:N}", port.Key);
        }
    }

    [Fact]
    public void NormalizesNestedFactoriesAndRemapsMacroBindingsAndInputAssociation()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        const string nestedKey = "test.nested-preset";
        registry.RegisterCompositePreset(nestedKey, "Nested test", id =>
        {
            var outer = new CompositeNode(id, "Nested test", [], []);
            outer.InternalGraph.AddNode(DroneTemplate.Create(Guid.NewGuid()));
            return outer;
        });
        var json = CompositePresetExporter.Export(registry);
        Assert.Equal(json, CompositePresetExporter.Export(registry));
        using var parsed = JsonDocument.Parse(json);
        var serializer = new PatchDocumentSerializer();
        var nested = serializer.Deserialize(parsed.RootElement.GetProperty("presets").GetProperty(nestedKey).GetRawText());
        var outer = Assert.Single(nested.RootGraph.Nodes);
        var inner = Assert.Single(outer.Composite!.Graph.Nodes);
        Assert.NotNull(inner.Composite);
        Assert.NotEqual(outer.Composite.Graph.Id, inner.Composite!.Graph.Id);
        using var nestedSession = serializer.Restore(nested, registry);
        Assert.Equal(serializer.Serialize(nested), serializer.Serialize(serializer.Capture(nestedSession,
            nested.BeatsPerMinute, nestedSession.GraphId, [])));

        // Start from a native-captured version of the existing macro fixture,
        // including actual mirrored port keys, then normalize all references.
        var fixture = serializer.Deserialize(PatchContractExporter.Export()["deferred-macro-input.json"]);
        using var original = serializer.Restore(fixture, registry);
        var captured = serializer.Capture(original, fixture.BeatsPerMinute, original.GraphId, []);
        var normalized = CompositePresetExporter.NormalizeTemplate(captured);
        Assert.Equal(serializer.Serialize(normalized), serializer.Serialize(CompositePresetExporter.NormalizeTemplate(normalized)));
        var wrapper = normalized.RootGraph.Nodes.Single(node => node.Composite is not null);
        var composite = wrapper.Composite!;
        var macro = Assert.Single(composite.Macros);
        Assert.Equal(Assert.Single(composite.Inputs).Id, macro.ModulationInputId);
        var target = Assert.Single(macro.Bindings).Target;
        var child = composite.Graph.Nodes.Single(node => node.Id == target.NodeId);
        Assert.Contains(child.Parameters, parameter => parameter.Id == target.ParameterId);
        using var restored = serializer.Restore(normalized, registry);
        Assert.Equal(serializer.Serialize(normalized), serializer.Serialize(serializer.Capture(restored,
            normalized.BeatsPerMinute, restored.GraphId, [])));
    }
}
