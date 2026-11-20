using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Models;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Checks committed edge identity, validation, local events and downstream DSP/range effects.</summary>
public sealed class ConnectionAmountCommandTests
{
    [Fact]
    public void EditsOnlySelectedEdgeAndPublishesImmutableBeforeAndAfter()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
        var before = session.Snapshot;
        var edge = before.Connections[0];
        var facts = new List<ConnectionAmountChanged>();
        using var listener = session.Subscribe<ConnectionAmountChanged>(facts.Add, session.GraphId);
        var result = session.Execute(new SetConnectionAmountCommand(session.GraphId, edge.Id, -25));
        var after = result.ChangeSet!.Snapshot;
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.Connections.Select(candidate => candidate.Id), after.Connections.Select(candidate => candidate.Id));
        Assert.Equal(edge with { Amount = -25 }, after.Connections[0]);
        Assert.Equal(before.Connections.Skip(1), after.Connections.Skip(1));
        Assert.Equal(100, before.Connections[0].Amount);
        var fact = Assert.Single(facts);
        Assert.Equal(session.GraphId, fact.GraphId);
        Assert.Equal(edge, fact.Before);
        Assert.Equal(after.Connections[0], fact.After);
        Assert.Same(fact, Assert.Single(result.ChangeSet.Events));
    }

    [Fact]
    public void SignedAndZeroAmountsReachCompiledSourceAndPredictedRange()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
        var edge = session.Snapshot.Connections[0];
        var lfo = session.Snapshot.Nodes[0];
        var carrier = session.Snapshot.Nodes[1];
        var address = new ParameterAddress(carrier.Id, carrier.Parameters[0].Id);
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        var compiler = FaustGraphCompiler.CreateBuiltIn();

        session.Execute(new SetConnectionAmountCommand(session.GraphId, edge.Id, -25));
        Assert.Contains($"n_{lfo.Id:N} * -25.0", compiler.Compile(session.Snapshot).Source);
        Assert.Equal(new ParameterRange(415, 465), estimator.Estimate(session.Snapshot)[address]!.Value);
        session.Execute(new SetConnectionAmountCommand(session.GraphId, edge.Id, 0));
        Assert.Contains($"n_{lfo.Id:N} * 0.0", compiler.Compile(session.Snapshot).Source);
        Assert.Equal(new ParameterRange(440, 440), estimator.Estimate(session.Snapshot)[address]!.Value);
        Assert.Contains(session.Snapshot.Connections, candidate => candidate.Id == edge.Id);
    }

    [Fact]
    public void InvalidAndUnchangedAmountsDoNotCommit()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var before = session.Snapshot;
        var edge = before.Connections[0];
        var calls = 0;
        using var listener = session.Subscribe(_ => calls++);
        foreach (var amount in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<GraphValidationException>(() => session.Execute(new SetConnectionAmountCommand(session.GraphId, edge.Id, amount)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new SetConnectionAmountCommand(session.GraphId, Guid.NewGuid(), 1)));
        Assert.False(session.Execute(new SetConnectionAmountCommand(session.GraphId, edge.Id, edge.Amount)).Changed);
        Assert.Same(before, session.Snapshot);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void NestedAudioAmountIsLocalAndRetainsEndpoints()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(), BuiltInNodes.CreateEditorRegistry());
        var added = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType));
        var drone = added.ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var child = drone.Composite!.Graph;
        var edge = child.Connections.First(candidate => child.GetNode(candidate.Destination.NodeId)
            .Inputs.Any(port => port.Id == candidate.Destination.PortId && port.SignalType == SignalType.Audio));
        var rootCalls = 0;
        var childFacts = new List<ConnectionAmountChanged>();
        using var root = session.Subscribe<ConnectionAmountChanged>(_ => rootCalls++, session.GraphId);
        using var local = session.Subscribe<ConnectionAmountChanged>(childFacts.Add, child.Id);
        Assert.Throws<GraphValidationException>(() => session.Execute(new SetConnectionAmountCommand(session.GraphId, edge.Id, 0.5)));
        session.Execute(new SetConnectionAmountCommand(child.Id, edge.Id, 0.5));
        Assert.Equal(0, rootCalls);
        Assert.Equal(edge with { Amount = 0.5 }, Assert.Single(childFacts).After);
        Assert.Equal(edge, child.Connections.Single(candidate => candidate.Id == edge.Id));
        Assert.Equal(0.5, session.Snapshot.GetGraph(child.Id).Connections.Single(candidate => candidate.Id == edge.Id).Amount);
    }
}
