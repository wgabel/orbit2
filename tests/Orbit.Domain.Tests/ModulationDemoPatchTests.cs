using Orbit.Application.Graphs;
using Orbit.Application.Models;
using Orbit.Domain.Nodes.Generators;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>The fixed DSP adapter must derive settings from validated graph objects.</summary>
public sealed class ModulationDemoPatchTests
{
    [Fact]
    public void BuildsTwoGeneratorsAndMapsTheirGraphToThePlaybackRequest()
    {
        var patch = ModulationDemoPatch.Create(new ModulationDemoSettings(440, 0.5, 100));
        var graph = patch.Graph;
        var generators = graph.Nodes.Where(node => node.TypeKey == SineGenerator.NodeType).ToArray();
        Assert.Equal(2, generators.Length);
        Assert.Equal(4, graph.Nodes.Count);
        Assert.Equal(3, graph.Connections.Count);

        var lfo = generators[0];
        var oscillator = generators[1];
        var oscillatorFrequency = oscillator.Parameters.Single(p => p.Definition.Key == "frequency");
        var route = Assert.Single(graph.Connections, edge =>
            edge.Destination.PortId == oscillatorFrequency.ModulationPortId);

        Assert.Equal(lfo.Id, route.Source.NodeId);
        Assert.Equal(440d, oscillatorFrequency.BaseValue);
        Assert.Equal(100d, route.Amount);
        Assert.Equal(0.1, patch.Playback.Volume);
        Assert.Equal("ModulatedSine", patch.Playback.Processor);
        Assert.Equal(440d, patch.Playback.Parameters["baseFrequency"]);
        Assert.Equal(0.5, patch.Playback.Parameters["lfoRate"]);
        Assert.Equal(100d, patch.Playback.Parameters["depthHz"]);
    }

    [Fact]
    public void CustomSettingsAndZeroDepthAreSupported()
    {
        var patch = ModulationDemoPatch.Create(new ModulationDemoSettings(220, 2, 0));
        Assert.Equal(220d, patch.Playback.Parameters["baseFrequency"]);
        Assert.Equal(2d, patch.Playback.Parameters["lfoRate"]);
        Assert.Equal(0d, patch.Playback.Parameters["depthHz"]);
    }

    [Theory]
    [InlineData(440, 100, 340, 540)]
    [InlineData(100, 200, 20, 300)]
    [InlineData(1000, 200, 800, 1200)]
    [InlineData(440, 0, 440, 440)]
    public void ReportsModulationBoundsWithoutChangingTheBaseValue(
        double frequency, double depth, double minimum, double maximum)
    {
        var settings = new ModulationDemoSettings(frequency, 0.5, depth);
        var range = settings.FrequencyModulationRange;
        Assert.Equal(minimum, range.Minimum);
        Assert.Equal(maximum, range.Maximum);
        Assert.Equal(frequency, settings.BaseFrequencyHertz);
    }

    [Fact]
    public void EditorDefinitionsShareTheValidationLimits()
    {
        var definition = ModulationDemoSettings.BaseFrequencyDefinition;
        Assert.Equal(ModulationDemoSettings.MinimumBaseFrequency, definition.Minimum);
        Assert.Equal(ModulationDemoSettings.MaximumBaseFrequency, definition.Maximum);
        Assert.True(definition.IsValidBaseValue(440));
        Assert.False(definition.IsValidBaseValue(double.NaN));
        Assert.False(definition.IsValidBaseValue(1001));
    }

    [Theory]
    [InlineData(0, 0.5, 100)]
    [InlineData(440, 0, 100)]
    [InlineData(440, 0.5, -1)]
    [InlineData(440, 0.5, 201)]
    public void RejectsInvalidDemoSettings(double frequency, double lfo, double depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModulationDemoPatch.Create(new ModulationDemoSettings(frequency, lfo, depth)));
    }
}
