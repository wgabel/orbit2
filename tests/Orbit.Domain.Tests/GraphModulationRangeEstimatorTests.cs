using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Models;
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
}
