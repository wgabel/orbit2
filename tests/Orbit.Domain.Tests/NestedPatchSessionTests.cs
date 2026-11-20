using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>All child edits use the ordinary patch commands and the same commit boundary.</summary>
public sealed class NestedPatchSessionTests
{
    [Fact]
    public void ChildCommandsCommitOneRootRevisionAndLocalFacts()
    {
        using var session = CreateSession();
        var drone = Add(session, session.GraphId, CompositeNode.NodeType);
        var child = drone.Composite!.Graph.Id;
        var sibling = Add(session, session.GraphId, CompositeNode.NodeType).Composite!.Graph.Id;
        var wholePatch = new List<GraphChangeSet>();
        var local = new List<GraphChangeSet>();
        var root = new List<GraphChangeSet>();
        var other = new List<GraphChangeSet>();
        var parameterEvents = new List<ParameterChanged>();
        using var allListener = session.Subscribe(wholePatch.Add);
        using var childListener = session.Subscribe(local.Add, child);
        using var rootListener = session.Subscribe(root.Add, session.GraphId);
        using var siblingListener = session.Subscribe(other.Add, sibling);
        using var typed = session.Subscribe<ParameterChanged>(parameterEvents.Add, child);
        var oscillator = Add(session, child, SineGenerator.NodeType);
        var before = session.Snapshot;
        var address = new ParameterAddress(oscillator.Id, oscillator.Parameters[0].Id);
        var result = session.Execute(new SetParameterValueCommand(child, address, 330));
        var change = result.ChangeSet!;
        Assert.Equal(child, change.GraphId);
        Assert.Equal(session.GraphId, change.Snapshot.Id);
        Assert.Same(session.Snapshot, change.Snapshot);
        Assert.Same(session.Snapshot.GetGraph(child), change.ChangedGraph);
        Assert.Equal(before.Revision + 1, change.Revision);
        Assert.Equal(change.Revision, change.ChangedGraph.Revision);
        Assert.Equal(change.Revision, change.Snapshot.GetGraph(sibling).Revision);
        Assert.Equal(440, before.GetGraph(child).GetNode(oscillator.Id).Parameters[0].BaseValue);
        Assert.Equal(330, change.ChangedGraph.GetNode(oscillator.Id).Parameters[0].BaseValue);
        Assert.False(change.TopologyChanged);
        Assert.Equal(child, Assert.Single(parameterEvents).GraphId);
        Assert.Equal(2, local.Count);
        Assert.Equal(2, wholePatch.Count);
        Assert.Empty(root);
        Assert.Empty(other);
        Assert.Empty(result.SubscriberErrors);
    }

    [Fact]
    public void GrandchildRoutingUsesOrdinaryValidationAndAtomicRemoval()
    {
        using var session = CreateSession();
        var outer = Add(session, session.GraphId, CompositeNode.NodeType).Composite!;
        var inner = Add(session, outer.Graph.Id, CompositeNode.NodeType).Composite!;
        var graphId = inner.Graph.Id;
        var oscillator = Add(session, graphId, SineGenerator.NodeType);
        var rack = session.Snapshot.GetGraph(graphId).GetNode(inner.OutputRackId);
        var source = new PortAddress(oscillator.Id, oscillator.Outputs[0].Id);
        var destination = new PortAddress(rack.Id, rack.Inputs[0].Id);
        var edge = session.Execute(new ConnectNodesCommand(graphId, source, destination)).ChangeSet!;
        Assert.Equal(graphId, Assert.IsType<ConnectionCreated>(Assert.Single(edge.Events)).GraphId);
        var connection = Assert.Single(edge.ChangedGraph.Connections);
        var removed = session.Execute(new RemoveNodeCommand(graphId, oscillator.Id)).ChangeSet!;
        Assert.Collection(removed.Events,
            fact => Assert.Equal(connection, Assert.IsType<ConnectionRemoved>(fact).Connection),
            fact => Assert.Equal(oscillator.Id, Assert.IsType<NodeRemoved>(fact).Node.Id));
        Assert.Empty(removed.ChangedGraph.Connections);
        Assert.Equal(2, removed.ChangedGraph.Nodes.Count);
        Assert.Single(edge.ChangedGraph.Connections);
    }

    [Fact]
    public void InvalidAndNoOpChildCommandsDoNotPublishOrMutateThePatch()
    {
        using var session = CreateSession();
        var drone = Add(session, session.GraphId, CompositeNode.NodeType).Composite!;
        var child = drone.Graph.Id;
        var oscillator = Add(session, child, SineGenerator.NodeType);
        var parent = Add(session, session.GraphId, SineGenerator.NodeType);
        var address = new ParameterAddress(oscillator.Id, oscillator.Parameters[0].Id);
        var before = session.Snapshot;
        var notifications = 0;
        using var listener = session.Subscribe(_ => notifications++);
        Assert.Throws<GraphValidationException>(() => session.Execute(new AddNodeCommand(child, MasterOutput.NodeType)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new RemoveNodeCommand(child, drone.InputRackId)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new ConnectNodesCommand(child,
            new PortAddress(parent.Id, parent.Outputs[0].Id), new PortAddress(oscillator.Id, oscillator.Inputs[0].Id))));
        Assert.Throws<GraphValidationException>(() => session.Execute(new SetParameterValueCommand(session.GraphId, address, 220)));
        Assert.False(session.Execute(new SetParameterValueCommand(child, address, 440)).Changed);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Execute(new SetParameterValueCommand(child, address, double.NaN)));
        Assert.Equal(0, notifications);
        Assert.Same(before, session.Snapshot);
    }

    [Fact]
    public void RemovedAndForeignGraphsCannotReceiveCommands()
    {
        using var session = CreateSession();
        using var foreign = CreateSession();
        var parent = Add(session, session.GraphId, CompositeNode.NodeType);
        var child = parent.Composite!.Graph.Id;
        var nested = Add(session, child, CompositeNode.NodeType).Composite!.Graph.Id;
        var foreignChild = Add(foreign, foreign.GraphId, CompositeNode.NodeType).Composite!.Graph.Id;
        session.Execute(new RemoveNodeCommand(session.GraphId, parent.Id));
        var before = session.Snapshot;
        foreach (var graphId in new[] { child, nested, foreignChild })
            Assert.Throws<GraphValidationException>(() => session.Execute(new AddNodeCommand(graphId, SineGenerator.NodeType)));
        Assert.Same(before, session.Snapshot);
    }

    private static PatchSession CreateSession()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Drone", [],
            [new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio)]));
        return new PatchSession(registry);
    }

    private static GraphNodeSnapshot Add(IPatchSession session, Guid graphId, string type) =>
        Assert.IsType<NodeAdded>(Assert.Single(session.Execute(new AddNodeCommand(graphId, type)).ChangeSet!.Events)).Node;
}
