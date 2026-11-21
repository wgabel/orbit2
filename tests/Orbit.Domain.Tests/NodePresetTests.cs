using System.Text;
using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Presets;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Infrastructure.Presets;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class NodePresetTests
{
    private readonly NodePresetValidator validator = new(BuiltInNodes.CreateEditorRegistry());

    [Fact]
    public void EveryShippedPresetIsValidAndEveryEligibleTypeHasExampleData()
    {
        var catalog = new EmbeddedNodePresetCatalog();
        var service = new NodePresetService(catalog, validator);
        var registry = BuiltInNodes.CreateEditorRegistry();
        foreach (var key in registry.RegisteredTypes.Where(validator.Supports))
        {
            var groups = service.GetGroups(key);
            Assert.True(groups.Count >= 2, key);
            Assert.Equal(NodePreset.CustomGroup, groups[^1].Name);
            Assert.Empty(groups[^1].Presets);
            foreach (var preset in groups.SelectMany(group => group.Presets))
            {
                Assert.Equal(key, preset.NodeType);
                Assert.Same(preset, service.GetPreset(preset.Id));
                Assert.Equal(registry.Create(key).Parameters.Count, preset.Parameters.Count);
                validator.Validate(preset);
            }
        }
        Assert.NotEmpty(catalog.LoadBuiltInPresets());
    }

    [Fact]
    public void GroupsAndEntriesHaveStableOrderingAndRetainInactiveValues()
    {
        var service = new NodePresetService(new EmbeddedNodePresetCatalog(), validator);
        var groups = service.GetGroups(OscillatorGenerator.NodeType);
        Assert.Equal(groups.Take(groups.Count - 1).Select(group => group.Name).Order(StringComparer.OrdinalIgnoreCase),
            groups.Take(groups.Count - 1).Select(group => group.Name));
        Assert.Equal(NodePreset.CustomGroup, groups[^1].Name);
        foreach (var group in groups)
            Assert.Equal(group.Presets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(preset => preset.Id, StringComparer.Ordinal), group.Presets);
        var pulse = service.GetPreset("oscillator-quarter-pulse");
        Assert.Equal(1d, pulse.Parameters["timing"]);
        Assert.Equal(1d, pulse.Parameters["frequency"]); // Dormant in synchronized mode.
        Assert.Equal(2d, pulse.Parameters["division"]);
    }

    [Theory]
    [InlineData(MixerProcessor.NodeType)]
    [InlineData(MasterOutput.NodeType)]
    [InlineData(InputRack.NodeType)]
    [InlineData(OutputRack.NodeType)]
    [InlineData(CompositeNode.NodeType)]
    [InlineData(DroneTemplate.LayeredPresetKey)]
    [InlineData("unknown")]
    public void IneligibleTypesCannotHavePresets(string nodeType)
    {
        Assert.False(validator.Supports(nodeType));
        var service = new NodePresetService(new TestCatalog([]), validator);
        Assert.Empty(service.GetGroups(nodeType));
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(nodeType, new Dictionary<string, double>())));
    }

    [Fact]
    public void EligibleTypeWithoutBuiltInsStillHasCustomGroup()
    {
        var service = new NodePresetService(new TestCatalog([]), validator);
        var custom = Assert.Single(service.GetGroups(GainProcessor.NodeType));
        Assert.Equal(NodePreset.CustomGroup, custom.Name);
        Assert.Empty(custom.Presets);
    }

    [Fact]
    public void ModelDefensivelyCopiesParameterValues()
    {
        var input = new Dictionary<string, double> { ["gain"] = 0.5 };
        var preset = CreatePreset(GainProcessor.NodeType, input);
        input["gain"] = 0.8;
        Assert.Equal(0.5, preset.Parameters["gain"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, double>)preset.Parameters)["gain"] = 1);
    }

    [Fact]
    public void InvalidOrIncompleteConfigurationsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(GainProcessor.NodeType, new Dictionary<string, double>())));
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(GainProcessor.NodeType, new Dictionary<string, double> { ["wrong"] = 0.5 })));
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(GainProcessor.NodeType, new Dictionary<string, double> { ["gain"] = 1.1 })));
        Assert.Throws<ArgumentException>(() => CreatePreset(GainProcessor.NodeType, new Dictionary<string, double> { ["gain"] = double.NaN }));
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(NoiseGenerator.NodeType, new Dictionary<string, double> { ["color"] = 0.5, ["amplitude"] = 0.1 })));
        var oscillator = new EmbeddedNodePresetCatalog().LoadBuiltInPresets().First(preset => preset.Id == "oscillator-quarter-pulse");
        var values = oscillator.Parameters.ToDictionary(pair => pair.Key, pair => pair.Value);
        values.Remove("frequency");
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(oscillator.NodeType, values)));
    }

    [Fact]
    public void BuiltInCatalogRejectsDuplicateIdsAndReservedGroup()
    {
        var preset = CreatePreset(GainProcessor.NodeType, new Dictionary<string, double> { ["gain"] = 0.5 });
        Assert.Throws<ArgumentException>(() => new NodePresetService(new TestCatalog([preset, preset]), validator));
        var custom = new NodePreset(1, "custom-id", GainProcessor.NodeType, "Custom", "custom", null, preset.Parameters);
        Assert.Throws<ArgumentException>(() => new NodePresetService(new TestCatalog([custom]), validator));
    }

    [Fact]
    public void ReaderRejectsUnsupportedSchemaDuplicateKeysAndUnknownProperties()
    {
        const string valid = """
            [{"schemaVersion":1,"id":"gain-test","nodeType":"processor.gain","name":"Test","group":"Level","parameters":{"gain":0.5}}]
            """;
        Assert.Single(Read(valid));
        Assert.Throws<NotSupportedException>(() => Read(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
        Assert.Throws<ArgumentException>(() => Read(valid.Replace("\"gain\":0.5", "\"gain\":0.5,\"gain\":0.2")));
        Assert.Throws<ArgumentException>(() => Read(valid.Replace("\"name\":\"Test\"", "\"name\":\"Test\",\"name\":\"Other\"")));
        Assert.Throws<JsonException>(() => Read(valid.Replace("\"name\":\"Test\"", "\"name\":\"Test\",\"isMuted\":true")));
        Assert.Throws<ArgumentException>(() => Read(valid.Replace("\"group\":\"Level\"", "\"group\":\" \"")));
    }

    private static NodePreset CreatePreset(string nodeType, IReadOnlyDictionary<string, double> values) =>
        new(1, "test", nodeType, "Test", "Usage", null, values);

    private static IReadOnlyList<NodePreset> Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return NodePresetJsonReader.ReadCatalog(stream);
    }

    private sealed class TestCatalog(IReadOnlyList<NodePreset> presets) : INodePresetCatalog
    {
        public IReadOnlyList<NodePreset> LoadBuiltInPresets() => presets;
    }
}
