using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

/// <summary>Independent canvas state for multiple graphs of the same patch.</summary>
public sealed class NestedCanvasStateTests
{
    [Fact]
    public void RootAndChildCanvasesObserveOnePatchRevisionAndKeepSeparateLayouts()
    {
        using var session = CreateSession();
        var first = Add(session, session.GraphId, CompositeNode.NodeType);
        var second = Add(session, session.GraphId, CompositeNode.NodeType);
        using var root = new CanvasStateService(session);
        using var child = new CanvasStateService(session, first.Composite!.Graph.Id);
        using var sibling = new CanvasStateService(session, second.Composite!.Graph.Id);
        root.MoveNode(first.Id, new CanvasPoint(30, 40));
        child.PanBy(new CanvasPoint(100, 200));
        var rootBefore = root.Snapshot;
        var childBefore = child.Snapshot;
        var siblingBefore = sibling.Snapshot;
        var changes = new List<CanvasStateChange>();
        using var subscription = child.Subscribe(changes.Add);

        var oscillator = Add(session, child.Snapshot.GraphId, OscillatorGenerator.NodeType);

        Assert.Equal(CanvasChangeKind.GraphCommitted, Assert.Single(changes).Kind);
        Assert.Same(session.Snapshot, root.Snapshot.Graph);
        Assert.Same(session.Snapshot.GetGraph(child.Snapshot.GraphId), child.Snapshot.Graph);
        Assert.Same(session.Snapshot.GetGraph(sibling.Snapshot.GraphId), sibling.Snapshot.Graph);
        Assert.Equal(root.Snapshot.GraphRevision, child.Snapshot.GraphRevision);
        Assert.Equal(root.Snapshot.GraphRevision, sibling.Snapshot.GraphRevision);
        Assert.Equal(rootBefore.NodePositions[first.Id], root.Snapshot.NodePositions[first.Id]);
        Assert.Equal(rootBefore.LayoutRevision, root.Snapshot.LayoutRevision);
        Assert.Equal(childBefore.Viewport, child.Snapshot.Viewport);
        Assert.Equal(siblingBefore.Viewport, sibling.Snapshot.Viewport);
        Assert.Equal(siblingBefore.LayoutRevision, sibling.Snapshot.LayoutRevision);
        Assert.True(child.Snapshot.NodePositions.ContainsKey(oscillator.Id));
        Assert.Equal(childBefore.LayoutRevision + 1, child.Snapshot.LayoutRevision);
        Assert.Equal(2, childBefore.Graph.Nodes.Count);
    }

    [Fact]
    public void DeletingAnAncestorInvalidatesTheChildCanvasExactlyOnce()
    {
        using var session = CreateSession();
        var parent = Add(session, session.GraphId, CompositeNode.NodeType);
        var nested = Add(session, parent.Composite!.Graph.Id, CompositeNode.NodeType);
        using var canvas = new CanvasStateService(session, nested.Composite!.Graph.Id);
        var old = canvas.Snapshot;
        var changes = new List<CanvasStateChange>();
        using var listener = canvas.Subscribe(changes.Add);
        session.Execute(new RemoveNodeCommand(session.GraphId, parent.Id));
        Assert.False(canvas.Snapshot.IsAvailable);
        Assert.Same(old.Graph, canvas.Snapshot.Graph);
        Assert.Equal(CanvasChangeKind.GraphRemoved, Assert.Single(changes).Kind);
        Assert.Throws<InvalidOperationException>(() => canvas.PanBy(new CanvasPoint(1, 1)));
        Add(session, session.GraphId, OscillatorGenerator.NodeType);
        Assert.Single(changes);
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
