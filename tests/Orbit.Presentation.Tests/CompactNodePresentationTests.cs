using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Generators;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class CompactNodePresentationTests
{
    [Fact]
    public void EveryRegisteredLeafHasAllPortBoundModulatableValuesAvailable()
    {
        var registry = BuiltInNodes.CreateRegistry();
        foreach (var key in registry.RegisteredTypes)
        {
            var node = Snapshot(registry.Create(key));
            if (!CompactNodePresentation.IsExpandable(node)) continue;
            foreach (var parameter in node.Parameters.Where(p => p.ModulationPortId.HasValue))
            {
                var input = Assert.Single(node.Inputs.Where(port => port.Id == parameter.ModulationPortId));
                Assert.Same(parameter, CompactNodePresentation.ForInput(node, input.Id));
            }
            foreach (var parameter in node.Parameters.Where(p => p.Definition.CanModulate && !p.ModulationPortId.HasValue))
                Assert.Contains(parameter, CompactNodePresentation.AdditionalParameters(node));
            foreach (var port in node.Inputs.Where(port => node.Parameters.All(p => p.ModulationPortId != port.Id)))
                Assert.Null(CompactNodePresentation.ForInput(node, port.Id));
        }
    }

    [Fact]
    public void SequencerSummaryAvoidsAllSixtyFourStepEditors()
    {
        var node = Snapshot(BuiltInNodes.CreateRegistry().Create(NoteSequencerGenerator.NodeType));
        var compact = CompactNodePresentation.AdditionalParameters(node);
        Assert.Contains(compact, p => p.Definition.Key == NoteSequencerGenerator.CountKey);
        Assert.Contains(compact, p => p.Definition.Key == NoteSequencerGenerator.ArticulationKey);
        Assert.Contains(compact, p => p.Definition.Key == NoteSequencerGenerator.ReferenceKey);
        Assert.DoesNotContain(compact, p => p.Definition.Key.StartsWith("pattern-step-", StringComparison.Ordinal));
        Assert.Equal(4, node.Outputs.Count);
    }

    [Fact]
    public void InactiveOscillatorModulationPortsStillHaveEditableBaseParameters()
    {
        var node = Snapshot(BuiltInNodes.CreateRegistry().Create(OscillatorGenerator.NodeType));
        var pulse = Assert.Single(node.Inputs.Where(p => p.Key == OscillatorGenerator.PulseWidthPortKey));
        var value = Assert.IsType<ParameterSnapshot>(CompactNodePresentation.ForInput(node, pulse.Id));
        Assert.False(node.IsInputActive(pulse.Id)); // default waveform is sine
        Assert.Equal(OscillatorGenerator.PulseWidthKey, value.Definition.Key);
    }

    private static GraphNodeSnapshot Snapshot(ISignalNode node) => new(node.Id, node.TypeKey, node.Name,
        node.Parameters.Select(parameter => new ParameterSnapshot(parameter.Id, parameter.Definition,
            parameter.BaseValue, parameter.ModulationPortId)).ToArray(),
        node.Inputs.ToArray(), node.Outputs.ToArray());
}
