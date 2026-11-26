using System.Text.Json;
using Orbit.AgentKit.Exporter;
using Orbit.Application.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class AgentKitCatalogTests
{
    [Fact]
    public void ExportIsDeterministicAndCoversTheEntireEditorRegistry()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        var json = CatalogExporter.Export(registry);
        Assert.Equal(json, CatalogExporter.Export(BuiltInNodes.CreateEditorRegistry()));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(CatalogExporter.FormatVersion, root.GetProperty("catalogFormatVersion").GetInt32());
        var entries = root.GetProperty("nodes");
        Assert.Equal(registry.RegisteredTypes.ToArray(), entries.EnumerateObject().Select(entry => entry.Name).ToArray());

        foreach (var key in registry.RegisteredTypes)
        {
            var node = registry.Create(key);
            var entry = entries.GetProperty(key);
            Assert.Equal(key, entry.GetProperty("creationKey").GetString());
            Assert.Equal(node.TypeKey, entry.GetProperty("persistedTypeKey").GetString());
            Assert.Equal(registry.Describe(key), entry.GetProperty("label").GetString());

            if (node is ICompositeNode)
            {
                Assert.Equal("deferred-composite", entry.GetProperty("metadataStatus").GetString());
                Assert.False(string.IsNullOrEmpty(entry.GetProperty("unsupportedReason").GetString()));
                Assert.Equal(0, entry.GetProperty("ports").GetArrayLength());
                continue;
            }

            Assert.Equal("leaf-definition", entry.GetProperty("metadataStatus").GetString());
            var parameters = entry.GetProperty("parameters").EnumerateArray()
                .ToDictionary(p => p.GetProperty("key").GetString()!);
            var ports = entry.GetProperty("ports").EnumerateArray()
                .ToDictionary(p => p.GetProperty("key").GetString()!);
            Assert.Equal(node.Parameters.Count, parameters.Count);
            Assert.Equal(node.Inputs.Count + node.Outputs.Count, ports.Count);

            foreach (var parameter in node.Parameters)
            {
                var definition = parameter.Definition;
                var exported = parameters[definition.Key];
                Assert.Equal(parameter.BaseValue, exported.GetProperty("factoryBaseValue").GetDouble());
                Assert.Equal(definition.DefaultValue, exported.GetProperty("defaultValue").GetDouble());
                Assert.Equal(definition.Minimum, exported.GetProperty("minimum").GetDouble());
                Assert.Equal(definition.Maximum, exported.GetProperty("maximum").GetDouble());
                Assert.Equal(definition.CanModulate, exported.GetProperty("canModulate").GetBoolean());
                Assert.Equal(definition.RequiresRecompile, exported.GetProperty("requiresRecompile").GetBoolean());
                Assert.Equal(parameter.ModulationPortId is Guid id ? node.Inputs.Single(p => p.Id == id).Key : null,
                    exported.GetProperty("modulationPortKey").GetString());
                if (definition.Choices is { } choices)
                    Assert.Equal(choices.Select(c => c.Value).ToArray(),
                        exported.GetProperty("choices").EnumerateArray().Select(c => c.GetProperty("value").GetDouble()).ToArray());
            }

            foreach (var port in node.Inputs.Concat(node.Outputs))
            {
                var exported = ports[port.Key];
                Assert.Equal(port.Direction.ToString(), exported.GetProperty("direction").GetString());
                Assert.Equal(port.SignalType.ToString(), exported.GetProperty("signalType").GetString());
                Assert.Equal(port.ChannelCount, exported.GetProperty("channelCount").GetInt32());
                Assert.Equal(port.FanIn.ToString(), exported.GetProperty("fanIn").GetString());
            }
        }
    }

    [Fact]
    public void ExportsConditionalAndDormantControlsWithoutInstanceIdentities()
    {
        using var document = JsonDocument.Parse(CatalogExporter.Export(BuiltInNodes.CreateEditorRegistry()));
        var nodes = document.RootElement.GetProperty("nodes");
        var oscillator = nodes.GetProperty(OscillatorGenerator.NodeType);
        var pitch = oscillator.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("key").GetString() == OscillatorGenerator.PitchKey);
        var clause = Assert.Single(pitch.GetProperty("activeWhen").EnumerateArray().ToArray());
        Assert.Equal(new[] { "timing", OscillatorGenerator.PitchTrackingKey },
            clause.EnumerateArray().Select(c => c.GetProperty("selectorKey").GetString()).ToArray());
        Assert.Equal(OscillatorGenerator.PitchPortKey, pitch.GetProperty("modulationPortKey").GetString());
        Assert.False(pitch.TryGetProperty("id", out _));
        var sequencer = nodes.GetProperty(NoteSequencerGenerator.NodeType).GetProperty("parameters");
        Assert.Contains(sequencer.EnumerateArray(), p =>
            p.GetProperty("key").GetString() == NoteSequencerGenerator.StepTimingOffsetKey(NoteSequencerGenerator.Capacity - 1));
        var preset = nodes.GetProperty(DroneTemplate.LayeredPresetKey);
        Assert.Equal("composite-preset", preset.GetProperty("kind").GetString());
        Assert.Equal(CompositeNode.NodeType, preset.GetProperty("persistedTypeKey").GetString());
    }
}
