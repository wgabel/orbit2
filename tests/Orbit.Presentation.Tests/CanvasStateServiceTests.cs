using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

/// <summary>Layout writes never mutate the graph, and graph commits reconcile presentation.</summary>
public sealed class CanvasStateServiceTests
{
    [Fact]
    public void ProjectsInitialNodesAndKeepsPositionsAcrossParameterEdits()
    {
        using var session = NewSession();
        var first = AddNode(session, OscillatorGenerator.NodeType);
        var second = AddNode(session, GainProcessor.NodeType);
        using var canvas = new CanvasStateService(session);
        var initial = canvas.Snapshot;
        Assert.Equal(2, initial.NodePositions.Count);
        Assert.NotEqual(initial.NodePositions[first.Id], initial.NodePositions[second.Id]);
        var position = new CanvasPoint(-50, 150);
        canvas.MoveNode(first.Id, position);
        var moved = canvas.Snapshot;
        var received = new List<CanvasStateChange>();
        using var observer = canvas.Subscribe(received.Add);

        var frequency = first.Parameters.Single(parameter => parameter.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(session.GraphId,
            new ParameterAddress(first.Id, frequency.Id), 220));

        Assert.Equal(CanvasChangeKind.GraphCommitted, Assert.Single(received).Kind);
        Assert.Same(session.Snapshot, canvas.Snapshot.Graph);
        Assert.Equal(moved.LayoutRevision, canvas.Snapshot.LayoutRevision);
        Assert.Equal(position, canvas.Snapshot.NodePositions[first.Id]);
        Assert.Equal(initial.NodePositions[second.Id], canvas.Snapshot.NodePositions[second.Id]);
        Assert.Equal(440d, initial.Graph.GetNode(first.Id).Parameters.Single(p => p.Id == frequency.Id).BaseValue);
        Assert.NotEqual(position, initial.NodePositions[first.Id]);
    }

    [Fact]
    public void InteractivePlacementCommitsOnceAndUndoRedoRetainsThePosition()
    {
        using var session = NewSession();
        var existing = AddNode(session, MasterOutput.NodeType);
        using var canvas = new CanvasStateService(session);
        canvas.PanBy(new CanvasPoint(-1000, -800));
        var visible = new CanvasBounds(1000, 800, 1700, 1300);
        var before = canvas.Snapshot;
        var changes = new List<CanvasStateChange>();
        using var observer = canvas.Subscribe(changes.Add);
        GraphNodeSnapshot added;
        using (canvas.ReserveVisibleNodePlacement(visible, []))
            added = AddNode(session, OscillatorGenerator.NodeType);
        var after = Assert.Single(changes).Snapshot;
        Assert.Equal(before.LayoutRevision + 1, after.LayoutRevision);
        Assert.Equal(before.Viewport, after.Viewport);
        Assert.Equal(before.NodePositions[existing.Id], after.NodePositions[existing.Id]);
        var position = after.NodePositions[added.Id];
        Assert.InRange(position.X, visible.Left, visible.Right);
        Assert.InRange(position.Y, visible.Top, visible.Bottom);

        session.Undo();
        canvas.PanBy(new CanvasPoint(4000, 4000));
        session.Redo();
        Assert.Equal(position, canvas.Snapshot.NodePositions[added.Id]);
    }

    [Fact]
    public void RejectedAddDoesNotLeakItsReservedPlacementIntoLaterCommands()
    {
        using var session = NewSession();
        using var canvas = new CanvasStateService(session);
        using (canvas.ReserveVisibleNodePlacement(new CanvasBounds(2000, 3000, 2800, 3600), []))
            Assert.Throws<KeyNotFoundException>(() => AddNode(session, "unknown"));
        var added = AddNode(session, OscillatorGenerator.NodeType);
        Assert.Equal(CanvasPoint.Zero, canvas.Snapshot.NodePositions[added.Id]);
    }

    [Fact]
    public void LayoutOnlyEditsNeverPublishGraphEventsOrChangeGraphRevision()
    {
        using var session = NewSession();
        var node = AddNode(session, OscillatorGenerator.NodeType);
        using var canvas = new CanvasStateService(session);
        var originalGraph = session.Snapshot;
        var layoutEvents = new List<CanvasStateChange>();
        var graphEvents = 0;
        using var graphListener = session.Subscribe(_ => graphEvents++);
        using var layoutListener = canvas.Subscribe(layoutEvents.Add);

        canvas.PanBy(new CanvasPoint(-20, 35));
        canvas.ZoomAt(2, CanvasPoint.Zero);
        var origin = canvas.Snapshot.NodePositions[node.Id];
        canvas.MoveNodeByScreenDelta(node.Id, new CanvasPoint(40, -20));
        canvas.SelectNode(node.Id);
        canvas.ResetViewport();

        Assert.Equal(0, graphEvents);
        Assert.Same(originalGraph, session.Snapshot);
        Assert.Same(originalGraph, canvas.Snapshot.Graph);
        Assert.Equal(5L, canvas.Snapshot.LayoutRevision);
        Assert.Equal(origin.Add(new CanvasPoint(20, -10)), canvas.Snapshot.NodePositions[node.Id]);
        Assert.Equal(CanvasViewport.Initial, canvas.Snapshot.Viewport);
        Assert.Equal(new[] { CanvasChangeKind.ViewportChanged, CanvasChangeKind.ViewportChanged,
            CanvasChangeKind.NodeMoved, CanvasChangeKind.SelectionChanged, CanvasChangeKind.ViewportChanged },
            layoutEvents.Select(change => change.Kind));
    }

    [Fact]
    public void NodeRemovalReconcilesAllIncidentConnectionsAndSelectionsOnce()
    {
        using var session = NewSession();
        var source = AddNode(session, OscillatorGenerator.NodeType);
        var gain = AddNode(session, GainProcessor.NodeType);
        var destination = AddNode(session, MasterOutput.NodeType);
        var first = Connect(session, source, gain);
        var second = Connect(session, gain, destination);
        using var canvas = new CanvasStateService(session);

        canvas.SelectNode(gain.Id);
        canvas.SelectNode(source.Id, additive: true);
        canvas.SelectConnection(first, additive: true);
        canvas.SelectConnection(second, additive: true);
        canvas.MoveNode(source.Id, new CanvasPoint(80, 120));
        var before = canvas.Snapshot;
        var changes = new List<CanvasStateChange>();
        using var listener = canvas.Subscribe(changes.Add);

        session.Execute(new RemoveNodeCommand(session.GraphId, gain.Id));

        var change = Assert.Single(changes);
        Assert.Equal(CanvasChangeKind.GraphCommitted, change.Kind);
        Assert.Equal(before.GraphRevision + 1, change.Snapshot.GraphRevision);
        Assert.Equal(before.LayoutRevision + 1, change.Snapshot.LayoutRevision);
        Assert.Empty(change.Snapshot.Graph.Connections);
        Assert.Empty(change.Snapshot.SelectedConnectionIds);
        Assert.Equal(source.Id, Assert.Single(change.Snapshot.SelectedNodeIds));
        Assert.False(change.Snapshot.NodePositions.ContainsKey(gain.Id));
        Assert.Equal(before.NodePositions[source.Id], change.Snapshot.NodePositions[source.Id]);
        Assert.Equal(2, before.SelectedConnectionIds.Count);
    }

    [Fact]
    public void NewNodesGetPositionsAndRejectedCommandsDoNotChangeCanvas()
    {
        using var session = NewSession();
        using var canvas = new CanvasStateService(session);
        var changes = new List<CanvasStateChange>();
        using var observer = canvas.Subscribe(changes.Add);
        var first = AddNode(session, OscillatorGenerator.NodeType);
        var second = AddNode(session, MasterOutput.NodeType);
        var before = canvas.Snapshot;

        Assert.Throws<KeyNotFoundException>(() => session.Execute(new AddNodeCommand(session.GraphId, "unknown")));
        Assert.Same(before, canvas.Snapshot);
        Assert.Equal(2, changes.Count);
        Assert.Equal(2L, before.LayoutRevision);
        Assert.NotEqual(before.NodePositions[first.Id], before.NodePositions[second.Id]);
    }

    [Fact]
    public void ViewportsAndSelectionAreIndependentOfOtherCanvasInstances()
    {
        using var firstSession = NewSession();
        using var secondSession = NewSession();
        var firstNode = AddNode(firstSession, OscillatorGenerator.NodeType);
        var secondNode = AddNode(secondSession, OscillatorGenerator.NodeType);
        using var first = new CanvasStateService(firstSession);
        using var second = new CanvasStateService(secondSession);

        first.PanBy(new CanvasPoint(300, -500));
        first.SelectNode(firstNode.Id);
        AddNode(firstSession, MasterOutput.NodeType);

        Assert.Equal(CanvasViewport.Initial, second.Snapshot.Viewport);
        Assert.Empty(second.Snapshot.SelectedNodeIds);
        Assert.Equal(secondNode.Id, Assert.Single(second.Snapshot.NodePositions.Keys));
        Assert.Equal(1L, second.Snapshot.GraphRevision);
    }

    [Fact]
    public void NoOpAndInvalidLayoutChangesDoNotPublish()
    {
        using var session = NewSession();
        var node = AddNode(session, OscillatorGenerator.NodeType);
        using var canvas = new CanvasStateService(session);
        var changes = new List<CanvasStateChange>();
        using var observer = canvas.Subscribe(changes.Add);
        var before = canvas.Snapshot;

        Assert.False(canvas.MoveNode(node.Id, before.NodePositions[node.Id]).Changed);
        Assert.False(canvas.ResetViewport().Changed);
        Assert.False(canvas.ClearSelection().Changed);
        Assert.Throws<KeyNotFoundException>(() => canvas.MoveNode(Guid.NewGuid(), CanvasPoint.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.MoveNode(node.Id, new CanvasPoint(0, double.NaN)));
        Assert.Throws<KeyNotFoundException>(() => canvas.SelectConnection(Guid.NewGuid()));

        Assert.Empty(changes);
        Assert.Same(before, canvas.Snapshot);
    }

    [Fact]
    public void SelectionSupportsReplacementAdditiveToggleAndCleanup()
    {
        using var session = NewSession();
        var source = AddNode(session, OscillatorGenerator.NodeType);
        var destination = AddNode(session, MasterOutput.NodeType);
        var connection = Connect(session, source, destination);
        using var canvas = new CanvasStateService(session);

        canvas.SelectNode(source.Id);
        Assert.False(canvas.SelectNode(source.Id).Changed);
        canvas.SelectNode(destination.Id, additive: true);
        Assert.Equal(2, canvas.Snapshot.SelectedNodeIds.Count);
        canvas.SelectNode(source.Id, additive: true);
        Assert.Equal(destination.Id, Assert.Single(canvas.Snapshot.SelectedNodeIds));
        canvas.SelectConnection(connection);
        Assert.Empty(canvas.Snapshot.SelectedNodeIds);
        Assert.Equal(connection, Assert.Single(canvas.Snapshot.SelectedConnectionIds));

        session.Execute(new RemoveConnectionCommand(session.GraphId, connection));
        Assert.Empty(canvas.Snapshot.SelectedConnectionIds);
    }

    [Fact]
    public void SubscriberErrorsDoNotRollBackLayoutAndDisposalStopsProjection()
    {
        using var session = NewSession();
        using var canvas = new CanvasStateService(session);
        var notifications = 0;
        using var failure = canvas.Subscribe(_ => throw new InvalidOperationException("listener"));
        using var listener = canvas.Subscribe(_ => notifications++);
        var result = canvas.PanBy(new CanvasPoint(-100, 45));

        Assert.True(result.Changed);
        Assert.Equal("listener", Assert.Single(result.SubscriberErrors).Message);
        Assert.Equal(1, notifications);
        Assert.Equal(new CanvasPoint(-100, 45), canvas.Snapshot.Viewport.Pan);
        var committed = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType));
        Assert.Single(committed.SubscriberErrors);
        Assert.Equal(2, notifications);
        Assert.Single(canvas.Snapshot.NodePositions);

        var before = canvas.Snapshot;
        canvas.Dispose();
        AddNode(session, MasterOutput.NodeType);
        Assert.Equal(2, notifications);
        Assert.Same(before, canvas.Snapshot);
        Assert.Throws<ObjectDisposedException>(() => canvas.ResetViewport());
    }

    [Fact]
    public void GraphCommitDuringNotificationDoesNotReorderCanvasUpdates()
    {
        using var session = NewSession();
        using var canvas = new CanvasStateService(session);
        var received = new List<CanvasStateChange>();
        using var edit = canvas.Subscribe(change =>
        {
            if (change.Kind == CanvasChangeKind.ViewportChanged)
                AddNode(session, OscillatorGenerator.NodeType);
        });
        using var observer = canvas.Subscribe(received.Add);

        var result = canvas.PanBy(new CanvasPoint(50, 50));

        Assert.Empty(result.SubscriberErrors);
        Assert.Collection(received,
            first =>
            {
                Assert.Equal(CanvasChangeKind.ViewportChanged, first.Kind);
                Assert.Empty(first.Snapshot.Graph.Nodes);
            },
            second =>
            {
                Assert.Equal(CanvasChangeKind.GraphCommitted, second.Kind);
                Assert.Single(second.Snapshot.Graph.Nodes);
            });
        Assert.Single(canvas.Snapshot.NodePositions);
    }

    private static PatchSession NewSession() => new(BuiltInNodes.CreateRegistry());

    private static GraphNodeSnapshot AddNode(IPatchSession session, string type) =>
        session.Execute(new AddNodeCommand(session.GraphId, type))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    private static Guid Connect(IPatchSession session, GraphNodeSnapshot source, GraphNodeSnapshot destination) =>
        session.Execute(new ConnectNodesCommand(session.GraphId,
            new PortAddress(source.Id, source.Outputs.Single().Id),
            new PortAddress(destination.Id, destination.Inputs.Single(port => port.Key == "audio-in").Id)))
            .ChangeSet!.Events.OfType<ConnectionCreated>().Single().Connection.Id;
}
