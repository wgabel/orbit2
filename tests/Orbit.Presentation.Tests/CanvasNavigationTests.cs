using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

/// <summary>Navigation is independently testable without the browser or DSP engine.</summary>
public sealed class CanvasNavigationTests
{
    [Fact]
    public void OpeningAndReturningRetainsIndependentCanvasState()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var drone = Add(session, session.GraphId, CompositeNode.NodeType);
        using var navigation = new CanvasNavigationService(session);
        var root = navigation.ActiveCanvas;
        root.PanBy(new CanvasPoint(50, 60));
        root.ZoomAt(0.75, CanvasPoint.Zero);
        root.MoveNode(drone.Id, new CanvasPoint(90, 80));
        var rootViewport = root.Snapshot.Viewport;
        var position = root.Snapshot.NodePositions[drone.Id];
        Assert.True(navigation.IsFirstVisit);
        Assert.True(navigation.OpenChild(drone.Id));
        var child = navigation.ActiveCanvas;
        Assert.True(navigation.IsFirstVisit);
        Assert.Equal(new[] { session.GraphId, drone.Composite!.Graph.Id }, navigation.Path.Select(crumb => crumb.GraphId));
        child.ZoomAt(1.5, CanvasPoint.Zero);
        child.PanBy(new CanvasPoint(-80, 100));
        var childViewport = child.Snapshot.Viewport;
        Assert.True(navigation.NavigateTo(session.GraphId));
        Assert.Same(root, navigation.ActiveCanvas);
        Assert.False(navigation.IsFirstVisit);
        Assert.Equal(rootViewport, root.Snapshot.Viewport);
        Assert.Equal(position, root.Snapshot.NodePositions[drone.Id]);
        Assert.True(navigation.OpenChild(drone.Id));
        Assert.Same(child, navigation.ActiveCanvas);
        Assert.Equal(childViewport, child.Snapshot.Viewport);
        Assert.False(navigation.IsFirstVisit);
        Assert.False(navigation.NavigateTo(child.Snapshot.GraphId));
        Assert.False(navigation.OpenChild(Guid.NewGuid()));
    }

    [Fact]
    public void RemovingAncestorReturnsToSurvivingCanvasAndPublishesItsNewRevision()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var outer = Add(session, session.GraphId, CompositeNode.NodeType);
        var inner = Add(session, outer.Composite!.Graph.Id, CompositeNode.NodeType);
        using var navigation = new CanvasNavigationService(session);
        var root = navigation.ActiveCanvas;
        navigation.OpenChild(outer.Id);
        var child = navigation.ActiveCanvas;
        navigation.OpenChild(inner.Id);
        var grandchild = navigation.ActiveCanvas;
        var updates = new List<CanvasStateSnapshot>();
        using var listener = navigation.Subscribe(updates.Add);
        var result = session.Execute(new RemoveNodeCommand(session.GraphId, outer.Id));
        Assert.Empty(result.SubscriberErrors);
        Assert.Same(root, navigation.ActiveCanvas);
        Assert.Single(navigation.Path);
        Assert.Same(session.Snapshot, Assert.Single(updates).Graph);
        Assert.False(navigation.IsFirstVisit);
        Assert.Throws<ObjectDisposedException>(() => child.PanBy(CanvasPoint.Zero));
        Assert.Throws<ObjectDisposedException>(() => grandchild.PanBy(CanvasPoint.Zero));
        Assert.False(navigation.NavigateTo(inner.Composite!.Graph.Id));
    }

    [Fact]
    public void DroneTemplateHasIndependentEditableRacksAndGenerators()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var first = Add(session, session.GraphId, CompositeNode.NodeType);
        var second = Add(session, session.GraphId, CompositeNode.NodeType);
        var firstGraph = first.Composite!.Graph;
        var secondGraph = second.Composite!.Graph;
        Assert.Equal(new[] { "Pitch", "Gain" }, first.Inputs.Select(port => port.Name));
        Assert.Equal("Audio", Assert.Single(first.Outputs).Name);
        Assert.Equal(4, firstGraph.Nodes.Count);
        Assert.Equal(4, firstGraph.Connections.Count);
        Assert.DoesNotContain(firstGraph.Nodes, node => node.TypeKey == MasterOutput.NodeType);
        Assert.NotEqual(firstGraph.Id, secondGraph.Id);
        var oscillator = firstGraph.Nodes.Single(node => node.TypeKey == SineGenerator.NodeType);
        var other = secondGraph.Nodes.Single(node => node.TypeKey == SineGenerator.NodeType);
        var address = new ParameterAddress(oscillator.Id, oscillator.Parameters[0].Id);
        Assert.NotEqual(oscillator.Id, other.Id);
        using var navigation = new CanvasNavigationService(session);
        navigation.OpenChild(first.Id);
        session.Execute(new SetParameterValueCommand(firstGraph.Id, address, 330));
        Assert.Equal(330, navigation.ActiveCanvas.Snapshot.Graph.GetNode(oscillator.Id).Parameters[0].BaseValue);
        navigation.NavigateTo(session.GraphId);
        navigation.OpenChild(second.Id);
        Assert.Equal(440, navigation.ActiveCanvas.Snapshot.Graph.GetNode(other.Id).Parameters[0].BaseValue);
    }

    private static GraphNodeSnapshot Add(IPatchSession session, Guid graphId, string type) =>
        session.Execute(new AddNodeCommand(graphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
}
