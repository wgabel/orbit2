using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Models;
using Orbit.Domain.Nodes.Composites;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Predicted ranges follow the same graph weights and limits as Faust.</summary>
public sealed class GraphModulationRangeEstimatorTests
{
    private readonly GraphModulationRangeEstimator estimator = new(BuiltInFaustEmitters.Create());

    [Fact]
    public void EstimatesFrequencyAndRespondsToSourceAmplitudeEdits()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
        var lfo = session.Snapshot.Nodes[0];
        var carrier = session.Snapshot.Nodes[1];
        var address = new ParameterAddress(carrier.Id, carrier.Parameters[0].Id);
        Assert.Equal(new ParameterRange(340, 540), Assert.Single(estimator.Estimate(session.Snapshot)).Value!.Value);
        session.Execute(new SetParameterValueCommand(session.GraphId,
            new ParameterAddress(lfo.Id, lfo.Parameters[1].Id), 0.25));
        Assert.Equal(new ParameterRange(415, 465), estimator.Estimate(session.Snapshot)[address]!.Value);
    }

    [Fact]
    public void NegativeWeightsAndClampingFollowParameterLimits()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
        var lfo = session.Snapshot.Nodes[0];
        var carrier = session.Snapshot.Nodes[1];
        var amplitude = carrier.Parameters[1];
        var address = new ParameterAddress(carrier.Id, amplitude.Id);
        session.Execute(new ConnectNodesCommand(session.GraphId, new PortAddress(lfo.Id, lfo.Outputs[0].Id),
            new PortAddress(carrier.Id, amplitude.ModulationPortId!.Value), -0.5));
        Assert.Equal(new ParameterRange(0.5, 1), estimator.Estimate(session.Snapshot)[address]!.Value);
    }

    [Fact]
    public void UnknownSourceBoundsAreNotInvented()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var graph = session.Snapshot;
        var changed = graph with { Nodes = graph.Nodes.Select((node, i) => i == 0
            ? node with { TypeKey = "generator.unknown" } : node).ToArray() };
        Assert.Null(Assert.Single(estimator.Estimate(changed)).Value);
    }

    [Fact]
    public void EstimatesConnectedMacroAndMappedTargetRangesWithClamping()
    {
        using var patch = new NestedFaustPatch();
        var root = patch.Session.Snapshot;
        var wrapper = root.GetGraph(patch.OuterId).Nodes.Single(node => node.Composite?.Graph.Id == patch.ChildId);
        var inputId = wrapper.Inputs.Single().Id;
        var macroId = Guid.NewGuid();
        patch.Session.Execute(new ReplaceCompositeMacrosCommand(patch.OuterId, wrapper.Id,
            [new CompositeMacroDefinition(macroId, "Size", 0.5, 0.5,
                new([new(patch.Frequency, 400, 200, 2)]), inputId)]));

        var updated = patch.Session.Snapshot;
        var macroAddress = new GraphMacroAddress(patch.OuterId, wrapper.Id, macroId);
        var macroRange = estimator.EstimateMacroRanges(updated, patch.OuterId)[macroAddress];
        Assert.Equal(new ParameterRange(0, 1), macroRange!.Value);
        var targetRange = estimator.Estimate(updated, patch.ChildId)[patch.Frequency]!.Value;
        Assert.True(targetRange.Minimum <= 200);
        Assert.True(targetRange.Maximum >= 400);

        var unknownSource = updated with { Nodes = updated.Nodes.Select((node, index) => index == 0
            ? node with { TypeKey = "generator.unknown" } : node).ToArray() };
        Assert.Null(estimator.EstimateMacroRanges(unknownSource, patch.OuterId)[macroAddress]);

        var disconnectedEdge = updated.GetGraph(patch.OuterId).Connections.Single(edge =>
            edge.Destination == new PortAddress(wrapper.Id, inputId));
        patch.Session.Execute(new RemoveConnectionCommand(patch.OuterId, disconnectedEdge.Id));
        var disconnected = patch.Session.Snapshot;
        Assert.Empty(estimator.EstimateMacroRanges(disconnected, patch.OuterId));
        Assert.Equal(new ParameterRange(350, 350), estimator.Estimate(disconnected, patch.ChildId)[patch.Frequency]!.Value);
    }
}
