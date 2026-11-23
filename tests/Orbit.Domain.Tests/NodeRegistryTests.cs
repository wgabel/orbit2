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
    public void CreatesRegisteredBuiltInNodeTypes()
    {
        var registry = BuiltInNodes.CreateRegistry();

        Assert.Equal(26, registry.RegisteredTypes.Count);
        foreach (var kind in Enum.GetValues<PhysicalSustainedKind>())
            Assert.IsType<PhysicalSustainedGenerator>(registry.Create(PhysicalSustainedGenerator.TypeFor(kind)));
        Assert.IsType<PhysicalPluckGenerator>(registry.Create(PhysicalPluckGenerator.NodeType));
        Assert.IsType<PhysicalMarimbaGenerator>(registry.Create(PhysicalMarimbaGenerator.NodeType));
        Assert.IsType<DrawbarOrganGenerator>(registry.Create(DrawbarOrganGenerator.NodeType));
        Assert.IsType<RotarySpeakerProcessor>(registry.Create(RotarySpeakerProcessor.NodeType));
        Assert.IsType<NoteSourceGenerator>(registry.Create(NoteSourceGenerator.NodeType));
        Assert.IsType<ResonatorProcessor>(registry.Create(ResonatorProcessor.NodeType));
        Assert.IsType<NoteSequencerGenerator>(registry.Create(NoteSequencerGenerator.NodeType));
        Assert.IsType<SignalToTriggerGenerator>(registry.Create(SignalToTriggerGenerator.NodeType));
        Assert.IsType<SignalToGateGenerator>(registry.Create(SignalToGateGenerator.NodeType));
        Assert.IsType<EnvelopeGenerator>(registry.Create(EnvelopeGenerator.NodeType));
        Assert.IsType<ChorusProcessor>(registry.Create(ChorusProcessor.NodeType));
        Assert.IsType<ReverbProcessor>(registry.Create(ReverbProcessor.NodeType));
        Assert.IsType<DelayProcessor>(registry.Create(DelayProcessor.NodeType));
        Assert.IsType<ParametricEqProcessor>(registry.Create(ParametricEqProcessor.NodeType));
        Assert.IsType<FilterProcessor>(registry.Create(FilterProcessor.NodeType));
        Assert.IsType<MixerProcessor>(registry.Create(MixerProcessor.NodeType));
        Assert.IsType<NoiseGenerator>(registry.Create(NoiseGenerator.NodeType));
        Assert.IsAssignableFrom<IGenerator>(registry.Create(OscillatorGenerator.NodeType));
        Assert.IsAssignableFrom<IProcessor>(registry.Create(GainProcessor.NodeType));
        var output = Assert.IsType<MasterOutput>(registry.Create(MasterOutput.NodeType));
        Assert.Single(output.Inputs);
        Assert.Empty(output.Outputs);
    }

    [Fact]
    public void LayeredPresetCreatesIndependentOrdinaryCompositeGraphs()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        var first = Assert.IsType<Orbit.Domain.Nodes.Composites.CompositeNode>(registry.Create(DroneTemplate.LayeredPresetKey));
        var second = Assert.IsType<Orbit.Domain.Nodes.Composites.CompositeNode>(registry.Create(DroneTemplate.LayeredPresetKey));
        Assert.Equal(Orbit.Domain.Nodes.Composites.CompositeNode.NodeType, first.TypeKey);
        Assert.Equal("Layered fifth", first.Name);
        Assert.Equal(3, first.InternalGraph.Nodes.OfType<OscillatorGenerator>().Count());
        Assert.Single(first.InternalGraph.Nodes.OfType<NoiseGenerator>());
        var mixer = Assert.Single(first.InternalGraph.Nodes.OfType<MixerProcessor>());
        Assert.Equal(4, first.InternalGraph.Connections.Count(edge => edge.Destination.NodeId == mixer.Id));
        Assert.Empty(first.InternalGraph.Nodes.Select(node => node.Id).Intersect(second.InternalGraph.Nodes.Select(node => node.Id)));
        var oscillator = first.InternalGraph.Nodes.OfType<OscillatorGenerator>().First();
        var frequency = oscillator.Parameters.Single(p => p.Definition.Key == "frequency");
        first.InternalGraph.SetParameterValue(new ParameterAddress(oscillator.Id, frequency.Id), 90);
        Assert.Equal(110, second.InternalGraph.Nodes.OfType<OscillatorGenerator>().First().Parameters.Single(p => p.Definition.Key == "frequency").BaseValue);
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
    public void OscillatorHasDiscreteDivisionAndTimingMode()
    {
        var graph = new SignalGraph();
        var node = new OscillatorGenerator(Guid.NewGuid());
        graph.AddNode(node);
        var division = node.Parameters.Single(p => p.Definition.Key == "division");
        Assert.Equal((double)MusicalDivision.QuarterNote, division.BaseValue);
        Assert.Equal(7, division.Definition.Choices!.Count);
        Assert.Null(division.ModulationPortId);
        Assert.Equal(5, node.Inputs.Count);
        var timing = node.Parameters.Single(p => p.Definition.Key == "timing");
        Assert.Equal((double)OscillatorTimingMode.Free, timing.BaseValue);
        Assert.Null(timing.ModulationPortId);
        Assert.True(timing.Definition.RequiresRecompile);
        graph.SetParameterValue(new ParameterAddress(node.Id, timing.Id), (int)OscillatorTimingMode.Synchronized);
        Assert.Equal((double)OscillatorTimingMode.Synchronized, timing.BaseValue);
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
