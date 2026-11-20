using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Tests committed commands, immutable revisions and observer isolation without a UI.</summary>
public sealed class PatchSessionTests
{
    [Fact]
    public void PublishesOneCommittedRevisionToIndependentSubscribers()
    {
        using var session = NewSession();
        var received = new List<GraphChangeSet>();
        var typedEvents = new List<NodeAdded>();
        using var first = session.Subscribe(change =>
        {
            Assert.Same(change.Snapshot, session.Snapshot);
            received.Add(change);
        });
        using var second = session.Subscribe<NodeAdded>(typedEvents.Add);

        var result = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType));

        Assert.True(result.Changed);
        Assert.Empty(result.SubscriberErrors);
        var change = Assert.Single(received);
        Assert.Same(result.ChangeSet, change);
        Assert.Equal(session.PatchId, change.PatchId);
        Assert.Equal(session.GraphId, change.GraphId);
        Assert.Equal(1L, change.Revision);
        Assert.True(change.TopologyChanged);
        var graphEvent = Assert.IsType<NodeAdded>(Assert.Single(change.Events));
        Assert.Equal(graphEvent, Assert.Single(typedEvents));
        Assert.Equal(graphEvent.Node.Id, Assert.Single(change.Snapshot.Nodes).Id);
    }

    [Fact]
    public void ConnectsAndDisconnectsWithCompleteEdgeFacts()
    {
        using var session = NewSession();
        var source = AddNode(session, OscillatorGenerator.NodeType);
        var destination = AddNode(session, MasterOutput.NodeType);
        var created = new List<ConnectionCreated>();
        var removed = new List<ConnectionRemoved>();
        using var creation = session.Subscribe<ConnectionCreated>(created.Add);
        using var removal = session.Subscribe<ConnectionRemoved>(removed.Add);

        var add = session.Execute(new ConnectNodesCommand(session.GraphId,
            Output(source), Input(destination, "audio-in"), 0.5));
        var edge = Assert.Single(session.Snapshot.Connections);
        Assert.Equal(edge, Assert.Single(created).Connection);
        Assert.Equal(0.5, edge.Amount);
        Assert.Equal(3L, add.ChangeSet!.Revision);

        var delete = session.Execute(new RemoveConnectionCommand(session.GraphId, edge.Id));
        Assert.Empty(session.Snapshot.Connections);
        Assert.Equal(edge, Assert.Single(removed).Connection);
        Assert.Equal(4L, delete.ChangeSet!.Revision);
        Assert.Equal(edge, Assert.Single(add.ChangeSet.Snapshot.Connections));
    }

    [Fact]
    public void RemovingNodeCommitsAllIncidentEdgesInOneChangeSet()
    {
        using var session = NewSession();
        var source = AddNode(session, OscillatorGenerator.NodeType);
        var processor = AddNode(session, GainProcessor.NodeType);
        var destination = AddNode(session, MasterOutput.NodeType);
        session.Execute(new ConnectNodesCommand(session.GraphId, Output(source), Input(processor, "audio-in")));
        session.Execute(new ConnectNodesCommand(session.GraphId, Output(processor), Input(destination, "audio-in")));
        var before = session.Snapshot;
        var revisions = new List<GraphChangeSet>();
        var edges = new List<ConnectionRemoved>();
        using var graphListener = session.Subscribe(revisions.Add);
        using var edgeListener = session.Subscribe<ConnectionRemoved>(edges.Add);

        var result = session.Execute(new RemoveNodeCommand(session.GraphId, processor.Id));

        var change = Assert.Single(revisions);
        Assert.Same(change, result.ChangeSet);
        Assert.Equal(before.Revision + 1, change.Revision);
        Assert.True(change.TopologyChanged);
        Assert.Equal(2, edges.Count);
        Assert.IsType<ConnectionRemoved>(change.Events[0]);
        Assert.IsType<ConnectionRemoved>(change.Events[1]);
        Assert.Equal(processor.Id, Assert.IsType<NodeRemoved>(change.Events[2]).Node.Id);
        Assert.Empty(change.Snapshot.Connections);
        Assert.DoesNotContain(change.Snapshot.Nodes, node => node.Id == processor.Id);
        Assert.Equal(2, before.Connections.Count);
    }

    [Fact]
    public void ParameterEditLeavesEarlierSnapshotsUnchangedAndIsNotTopologyChange()
    {
        using var session = NewSession();
        var node = AddNode(session, OscillatorGenerator.NodeType);
        var address = FrequencyAddress(node);
        var before = session.Snapshot;
        var parameters = new List<ParameterChanged>();
        using var subscription = session.Subscribe<ParameterChanged>(parameters.Add);

        var result = session.Execute(new SetParameterValueCommand(session.GraphId, address, 220));

        Assert.True(result.Changed);
        Assert.False(result.ChangeSet!.TopologyChanged);
        var change = Assert.Single(parameters);
        Assert.Equal(address, change.Address);
        Assert.Equal(440d, change.PreviousValue);
        Assert.Equal(220d, change.CurrentValue);
        Assert.Equal(440d, Frequency(before.GetNode(node.Id)).BaseValue);
        Assert.Equal(220d, Frequency(session.Snapshot.GetNode(node.Id)).BaseValue);
        Assert.True(Assert.IsAssignableFrom<IList<GraphNodeSnapshot>>(before.Nodes).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<ParameterSnapshot>>(node.Parameters).IsReadOnly);
    }

    [Fact]
    public void RejectedAndUnchangedCommandsPublishNothing()
    {
        using var session = NewSession();
        var source = AddNode(session, OscillatorGenerator.NodeType);
        var destination = AddNode(session, MasterOutput.NodeType);
        var before = session.Snapshot;
        var notifications = 0;
        using var subscription = session.Subscribe(_ => notifications++);

        Assert.Throws<GraphValidationException>(() => session.Execute(
            new ConnectNodesCommand(session.GraphId, Input(destination, "audio-in"), Input(source, "frequency-modulation-in"))));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new ConnectNodesCommand(session.GraphId, Output(source), new PortAddress(destination.Id, Guid.NewGuid()))));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new AddNodeCommand(Guid.NewGuid(), OscillatorGenerator.NodeType)));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Execute(
            new SetParameterValueCommand(session.GraphId, FrequencyAddress(source), double.NaN)));
        Assert.Throws<KeyNotFoundException>(() => session.Execute(
            new AddNodeCommand(session.GraphId, "missing.node")));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new RemoveConnectionCommand(session.GraphId, Guid.NewGuid())));

        var noChange = session.Execute(new SetParameterValueCommand(session.GraphId, FrequencyAddress(source), 440));
        Assert.False(noChange.Changed);
        Assert.Same(before, session.Snapshot);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void SubscriberErrorsDoNotRollbackOrPreventOtherSubscribers()
    {
        using var session = NewSession();
        var successful = new List<GraphChangeSet>();
        using var failure = session.Subscribe(_ => throw new InvalidOperationException("listener failure"));
        using var reentrant = session.Subscribe(_ => session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType)));
        using var observer = session.Subscribe(successful.Add);

        var result = session.Execute(new AddNodeCommand(session.GraphId, GainProcessor.NodeType));

        Assert.True(result.Changed);
        Assert.Equal(2, result.SubscriberErrors.Count);
        Assert.Equal("listener failure", result.SubscriberErrors[0].Message);
        Assert.Contains("inside a change callback", result.SubscriberErrors[1].Message);
        Assert.Single(successful);
        Assert.Single(session.Snapshot.Nodes);
        Assert.Equal(1L, session.Snapshot.Revision);
        failure.Dispose();
        reentrant.Dispose();

        Assert.Empty(session.Execute(new AddNodeCommand(session.GraphId, MasterOutput.NodeType)).SubscriberErrors);
        Assert.Equal(2L, session.Snapshot.Revision);
    }

    [Fact]
    public void AFailingTypedListenerStillReceivesEveryMatchingFactAndOtherListenersRun()
    {
        using var session = NewSession();
        var source = AddNode(session, OscillatorGenerator.NodeType);
        var gain = AddNode(session, GainProcessor.NodeType);
        var output = AddNode(session, MasterOutput.NodeType);
        session.Execute(new ConnectNodesCommand(session.GraphId, Output(source), Input(gain, "audio-in")));
        session.Execute(new ConnectNodesCommand(session.GraphId, Output(gain), Input(output, "audio-in")));
        var attempts = 0;
        var received = new List<ConnectionRemoved>();
        using var first = session.Subscribe<ConnectionRemoved>(_ =>
        {
            attempts++;
            throw new InvalidOperationException("typed listener");
        });
        using var second = session.Subscribe<ConnectionRemoved>(received.Add);

        var result = session.Execute(new RemoveNodeCommand(session.GraphId, gain.Id));

        Assert.Equal(2, attempts);
        Assert.Equal(2, received.Count);
        Assert.Equal(2, result.SubscriberErrors.Count);
        Assert.Empty(session.Snapshot.Connections);
    }

    [Fact]
    public void SubscriptionsAreScopedFilteredAndIndependentlyDisposable()
    {
        using var first = NewSession();
        using var second = NewSession();
        var received = 0;
        var filtered = 0;
        Action<GraphChangeSet> listener = _ => received++;
        using var subscriptionOne = first.Subscribe(listener);
        using var subscriptionTwo = first.Subscribe(listener);
        using var filter = first.Subscribe(_ => filtered++, second.GraphId);

        AddNode(second, OscillatorGenerator.NodeType);
        Assert.Equal(0, received);
        AddNode(first, OscillatorGenerator.NodeType);
        Assert.Equal(2, received);
        subscriptionOne.Dispose();
        subscriptionOne.Dispose();
        AddNode(first, GainProcessor.NodeType);
        Assert.Equal(3, received);
        Assert.Equal(0, filtered);
    }

    [Fact]
    public void DisposingSessionStopsNewEditsAndSubscriptions()
    {
        var session = NewSession();
        using var subscription = session.Subscribe(_ => { });
        session.Dispose();
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() =>
            session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType)));
        Assert.Throws<ObjectDisposedException>(() => session.Subscribe(_ => { }));
    }

    private static PatchSession NewSession() => new(BuiltInNodes.CreateRegistry());

    private static GraphNodeSnapshot AddNode(IPatchSession session, string type) =>
        session.Execute(new AddNodeCommand(session.GraphId, type)).ChangeSet!.Events
            .OfType<NodeAdded>().Single().Node;

    private static PortAddress Output(GraphNodeSnapshot node) =>
        new(node.Id, node.Outputs.Single().Id);

    private static PortAddress Input(GraphNodeSnapshot node, string key) =>
        new(node.Id, node.Inputs.Single(port => port.Key == key).Id);

    private static ParameterSnapshot Frequency(GraphNodeSnapshot node) =>
        node.Parameters.Single(parameter => parameter.Definition.Key == "frequency");

    private static ParameterAddress FrequencyAddress(GraphNodeSnapshot node) =>
        new(node.Id, Frequency(node).Id);
}
