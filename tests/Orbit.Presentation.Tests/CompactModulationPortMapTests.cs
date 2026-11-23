using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class CompactModulationPortMapTests
{
    [Fact]
    public void AllRegisteredLeafParametersRemainReachableByPortOrFallback()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        foreach (var type in registry.RegisteredTypes)
        {
            var source = registry.Create(type);
            if (source is ICompositeNode || source is InputRack or OutputRack or MasterOutput) continue;
            var node = new GraphNodeSnapshot(source.Id, source.TypeKey, source.Name,
                source.Parameters.Select(parameter => new ParameterSnapshot(parameter.Id, parameter.Definition,
                    parameter.BaseValue, parameter.ModulationPortId)).ToArray(),
                source.Inputs, source.Outputs);

            var mapped = node.Inputs.Select(port => CompactModulationPortMap.ForInput(node, port.Id))
                .OfType<ParameterSnapshot>().ToArray();
            var fallback = CompactModulationPortMap.WithoutInput(node).ToArray();
            var expected = node.Parameters.Where(parameter => parameter.Definition.CanModulate)
                .Select(parameter => parameter.Id).Order().ToArray();
            var actual = mapped.Concat(fallback).Select(parameter => parameter.Id).Order().ToArray();
            Assert.Equal(expected, actual);
            Assert.Equal(actual.Length, actual.Distinct().Count());
        }
    }

    [Fact]
    public void ConditionalInactiveOscillatorControlsKeepTheirPortMapping()
    {
        var oscillator = new OscillatorGenerator(Guid.NewGuid());
        var node = new GraphNodeSnapshot(oscillator.Id, oscillator.TypeKey, oscillator.Name,
            oscillator.Parameters.Select(parameter => new ParameterSnapshot(parameter.Id, parameter.Definition,
                parameter.BaseValue, parameter.ModulationPortId)).ToArray(),
            oscillator.Inputs, oscillator.Outputs);
        var pulseWidth = node.Parameters.Single(parameter => parameter.Definition.Key == OscillatorGenerator.PulseWidthKey);
        Assert.False(node.IsParameterActive(pulseWidth));
        Assert.NotNull(pulseWidth.ModulationPortId);
        Assert.Equal(pulseWidth.Id, CompactModulationPortMap.ForInput(node, pulseWidth.ModulationPortId.Value)?.Id);
    }
}
