using System.Text.Json;
using Orbit.AgentKit.Exporter;
using Orbit.Application.Graphs;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Composites;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class AgentKitCompositeTemplateTests
{
    [Fact]
    public void EveryCompositeFactoryExportsDeterministicNativeImportableTemplate()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        var serializer = new PatchDocumentSerializer();
        var json = CompositeTemplateExporter.Export();
        Assert.Equal(json, CompositeTemplateExporter.Export());
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal(CompositeTemplateExporter.FormatVersion, root.GetProperty("compositeTemplatesFormatVersion").GetInt32());
        Assert.Equal(PatchDocument.CurrentVersion, root.GetProperty("patchSchemaVersion").GetInt32());
        var templates = root.GetProperty("templates");
        var expected = registry.RegisteredTypes.Where(key => registry.Create(key) is ICompositeNode).ToArray();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, templates.EnumerateObject().Select(entry => entry.Name).ToArray());
        var example = serializer.Deserialize(PatchContractExporter.Export()["deferred-static-macro.json"])
            .RootGraph.Nodes.Single(node => node.Composite is not null);
        var wrapper = CompositeTemplateExporter.WrapTemplate(example);
        foreach (var key in expected)
        {
            var entry = templates.GetProperty(key);
            Assert.Equal(key, entry.GetProperty("creationKey").GetString());
            Assert.Equal(CompositeNode.NodeType, entry.GetProperty("persistedTypeKey").GetString());
            // Embed the exported wire node in a native patch without using
            // reflection deserialization of SavedNode.
            var wire = System.Text.Json.Nodes.JsonNode.Parse(serializer.Serialize(wrapper))!.AsObject();
            wire["rootGraph"]!["nodes"] = new System.Text.Json.Nodes.JsonArray(
                System.Text.Json.Nodes.JsonNode.Parse(entry.GetProperty("node").GetRawText()));
            var document = serializer.Deserialize(wire.ToJsonString());
            using var session = serializer.Restore(document, registry);
            using var navigation = new CanvasNavigationService(session, layouts: document.CanvasLayouts,
                activeGraphId: document.ActiveGraphId);
            var recaptured = serializer.Capture(session, document.BeatsPerMinute, document.ActiveGraphId, []);
            Assert.Equal(serializer.Serialize(document), serializer.Serialize(recaptured));
        }
    }

    [Fact]
    public void NormalizationPreservesMacroTargetAndMirroredBoundaryAssociation()
    {
        var serializer = new PatchDocumentSerializer();
        var original = serializer.Deserialize(PatchContractExporter.Export()["deferred-macro-input.json"])
            .RootGraph.Nodes.Single(node => node.Composite is not null);
        var normalized = CompositeTemplateExporter.NormalizeTemplate(original);
        var composite = normalized.Composite!;
        var macro = Assert.Single(composite.Macros);
        Assert.Equal(composite.Inputs.Single().Id, macro.ModulationInputId);
        var target = Assert.Single(macro.Bindings).Target;
        var child = composite.Graph.Nodes.Single(node => node.Id == target.NodeId);
        Assert.Contains(child.Parameters, parameter => parameter.Id == target.ParameterId);
        Assert.NotEqual(original.Id, normalized.Id);
        Assert.NotEqual(original.Composite!.Graph.Id, composite.Graph.Id);
        var patch = CompositeTemplateExporter.WrapTemplate(normalized);
        using var session = serializer.Restore(serializer.Deserialize(serializer.Serialize(patch)),
            BuiltInNodes.CreateEditorRegistry());
        using var navigation = new CanvasNavigationService(session);
        Assert.Single(session.Snapshot.Nodes);
    }
}
