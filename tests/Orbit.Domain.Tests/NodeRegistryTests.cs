using Orbit.Application.Graphs;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Tests stable type keys, registration, and independent node instances.</summary>
public sealed class NodeRegistryTests
{
    [Fact]
    public void CreatesThreeBuiltInNodeTypes()
    {
        var registry = BuiltInNodes.CreateRegistry();

        Assert.Equal(3, registry.RegisteredTypes.Count);
        Assert.IsAssignableFrom<IGenerator>(registry.Create(SineGenerator.NodeType));
        Assert.IsAssignableFrom<IProcessor>(registry.Create(GainProcessor.NodeType));
        var output = Assert.IsType<MasterOutput>(registry.Create(MasterOutput.NodeType));
        Assert.Single(output.Inputs);
        Assert.Empty(output.Outputs);
    }

    [Fact]
    public void InstancesHaveIndependentIdentitiesAndParameterValues()
    {
        var registry = BuiltInNodes.CreateRegistry();
        var first = registry.Create(SineGenerator.NodeType);
        var second = registry.Create(SineGenerator.NodeType);
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
    public void RejectsDuplicateAndUnknownTypeKeys()
    {
        var registry = BuiltInNodes.CreateRegistry();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(SineGenerator.NodeType, id => new SineGenerator(id)));
        Assert.Throws<KeyNotFoundException>(() => registry.Create("generator.unknown"));
    }

    [Fact]
    public void RejectsFactoryReturningWrongTypeOrIdentity()
    {
        var registry = new NodeRegistry();
        registry.Register("incorrect", id => new SineGenerator(id));
        registry.Register(SineGenerator.NodeType, _ => new SineGenerator(Guid.NewGuid()));

        Assert.Throws<InvalidOperationException>(() => registry.Create("incorrect"));
        Assert.Throws<InvalidOperationException>(() => registry.Create(SineGenerator.NodeType));
    }
}
