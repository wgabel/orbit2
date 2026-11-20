using Orbit.Application.Graphs;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Timing;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Tests stable type keys, registration, and independent node instances.</summary>
public sealed class NodeRegistryTests
{
    [Fact]
    public void CreatesFourBuiltInNodeTypes()
    {
        var registry = BuiltInNodes.CreateRegistry();

        Assert.Equal(4, registry.RegisteredTypes.Count);
        Assert.IsAssignableFrom<IGenerator>(registry.Create(OscillatorGenerator.NodeType));
        Assert.IsAssignableFrom<IGenerator>(registry.Create(TempoSineGenerator.NodeType));
        Assert.IsAssignableFrom<IProcessor>(registry.Create(GainProcessor.NodeType));
        var output = Assert.IsType<MasterOutput>(registry.Create(MasterOutput.NodeType));
        Assert.Single(output.Inputs);
        Assert.Empty(output.Outputs);
    }

    [Fact]
    public void InstancesHaveIndependentIdentitiesAndParameterValues()
    {
        var registry = BuiltInNodes.CreateRegistry();
        var first = registry.Create(OscillatorGenerator.NodeType);
        var second = registry.Create(OscillatorGenerator.NodeType);
        var firstFrequency = first.Parameters.Single(parameter => parameter.Definition.Key == "frequency");
        var secondFrequency = second.Parameters.Single(parameter => parameter.Definition.Key == "frequency");

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(firstFrequency.Id, secondFrequency.Id);
        Assert.NotEqual(firstFrequency.ModulationPortId, secondFrequency.ModulationPortId);
        Assert.NotEqual(first.Outputs.Single().Id, second.Outputs.Single().Id);

        var graph = new SignalGraph();
        graph.AddNode(first);
        graph.AddNode(second);
        graph.SetParameterValue(new ParameterAddress(first.Id, firstFrequency.Id), 110);

        Assert.Equal(110d, firstFrequency.BaseValue);
        Assert.Equal(440d, secondFrequency.BaseValue);
    }

    [Fact]
    public void TempoGeneratorHasDiscreteDivisionAndModulatablePhaseAndAmplitude()
    {
        var graph = new SignalGraph();
        var node = new TempoSineGenerator(Guid.NewGuid());
        graph.AddNode(node);
        var division = node.Parameters.Single(p => p.Definition.Key == "division");
        Assert.Equal((double)MusicalDivision.QuarterNote, division.BaseValue);
        Assert.Equal(7, division.Definition.Choices!.Count);
        Assert.Null(division.ModulationPortId);
        Assert.Equal(2, node.Inputs.Count);
        var address = new ParameterAddress(node.Id, division.Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.SetParameterValue(address, 2.5));
        Assert.Equal((double)MusicalDivision.QuarterNote, division.BaseValue);
        graph.SetParameterValue(address, (int)MusicalDivision.HalfNote);
        Assert.Equal((double)MusicalDivision.HalfNote, division.BaseValue);
    }

    [Fact]
    public void RejectsDuplicateAndUnknownTypeKeys()
    {
        var registry = BuiltInNodes.CreateRegistry();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(OscillatorGenerator.NodeType, id => new OscillatorGenerator(id)));
        Assert.Throws<KeyNotFoundException>(() => registry.Create("generator.unknown"));
    }

    [Fact]
    public void RejectsFactoryReturningWrongTypeOrIdentity()
    {
        var registry = new NodeRegistry();
        registry.Register("incorrect", id => new OscillatorGenerator(id));
        registry.Register(OscillatorGenerator.NodeType, _ => new OscillatorGenerator(Guid.NewGuid()));

        Assert.Throws<InvalidOperationException>(() => registry.Create("incorrect"));
        Assert.Throws<InvalidOperationException>(() => registry.Create(OscillatorGenerator.NodeType));
    }
}
