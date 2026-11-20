using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Verifies independent parameter objects and validated base-value editing.</summary>
public sealed class ParameterTests
{
    [Fact]
    public void OscillatorContinuousParametersExposeTheirOwnModulationSockets()
    {
        var node = new OscillatorGenerator(Guid.NewGuid());

        Assert.Equal(4, node.Parameters.Count);
        Assert.Equal(3, node.Inputs.Count);
        foreach (var parameter in node.Parameters.Where(parameter => parameter.Definition.CanModulate))
        {
            Assert.True(parameter.Definition.CanModulate);
            Assert.NotNull(parameter.ModulationPortId);
            var port = Assert.Single(node.Inputs, port => port.Id == parameter.ModulationPortId);
            Assert.Equal(SignalType.Modulation, port.SignalType);
            Assert.Equal(FanInPolicy.Sum, port.FanIn);
        }

        Assert.NotEqual(node.Parameters[0].ModulationPortId, node.Parameters[1].ModulationPortId);
        var waveform = node.Parameters.Single(parameter => parameter.Definition.Key == "waveform");
        Assert.False(waveform.Definition.CanModulate);
        Assert.True(waveform.Definition.RequiresRecompile);
        Assert.Equal(4, waveform.Definition.Choices!.Count);
        Assert.Null(waveform.ModulationPortId);
    }

    [Fact]
    public void WaveformEditsKeepNodePortsAndOtherParametersUnchanged()
    {
        var (graph, node, frequency) = CreateSine();
        var waveform = node.Parameters.Single(parameter => parameter.Definition.Key == "waveform");
        var address = new ParameterAddress(node.Id, waveform.Id);
        var output = node.Outputs.Single().Id;
        graph.SetParameterValue(address, (int)OscillatorWaveform.Square);
        Assert.Equal((double)OscillatorWaveform.Square, waveform.BaseValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.SetParameterValue(address, 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.SetParameterValue(address, 4));
        Assert.Equal((double)OscillatorWaveform.Square, waveform.BaseValue);
        Assert.Equal(output, node.Outputs.Single().Id);
        Assert.Equal(440, frequency.BaseValue);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.5)]
    [InlineData(110)]
    [InlineData(20000)]
    public void AcceptsBaseFrequencyWithinRange(double value)
    {
        var (graph, node, frequency) = CreateSine();
        graph.SetParameterValue(new ParameterAddress(node.Id, frequency.Id), value);

        Assert.Equal(value, frequency.BaseValue);
        Assert.Equal(440d, frequency.Definition.DefaultValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(20001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidBaseValueWithoutChangingParameter(double value)
    {
        var (graph, node, frequency) = CreateSine();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            graph.SetParameterValue(new ParameterAddress(node.Id, frequency.Id), value));
        Assert.Equal(440d, frequency.BaseValue);
    }

    [Fact]
    public void RejectsMissingParameter()
    {
        var (graph, node, _) = CreateSine();

        Assert.Throws<GraphValidationException>(() =>
            graph.SetParameterValue(new ParameterAddress(node.Id, Guid.NewGuid()), 110));
    }

    [Fact]
    public void ParameterDefinitionRejectsInvalidDefaultAndMissingModulationPort()
    {
        var definition = new ParameterDefinition("test", "Test", 2, 0, 1,
            ParameterUnit.Normalized, ParameterScale.Linear, true);

        Assert.Throws<ArgumentException>(() =>
            new Parameter(Guid.NewGuid(), definition, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() =>
            new Parameter(Guid.NewGuid(), definition with { DefaultValue = 0.5 }));
    }

    private static (SignalGraph Graph, OscillatorGenerator Node, Parameter Frequency) CreateSine()
    {
        var graph = new SignalGraph();
        var node = new OscillatorGenerator(Guid.NewGuid());
        graph.AddNode(node);

        return (graph, node, node.Parameters.Single(parameter => parameter.Definition.Key == "frequency"));
    }
}
