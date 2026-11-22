using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class ModuleSelectionTests
{
    private static GraphNodeSnapshot Add(PatchSession session, string type) =>
        session.Execute(new AddNodeCommand(session.GraphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    [Fact]
    public void BulkSelectionIsAtomicDeduplicatedAndPresentationOnly()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var first = Add(session, OscillatorGenerator.NodeType);
        var second = Add(session, OscillatorGenerator.NodeType);
        var master = Add(session, MasterOutput.NodeType);
        using var canvas = new CanvasStateService(session);
        canvas.SelectNode(master.Id);
        var original = session.Snapshot;
        var before = canvas.Snapshot;
        var changes = new List<CanvasStateChange>();
        using var listener = canvas.Subscribe(changes.Add);
        canvas.SelectModuleNodes([first.Id, first.Id, master.Id]);
        Assert.Equal(first.Id, Assert.Single(canvas.Snapshot.SelectedNodeIds));
        Assert.Equal(CanvasChangeKind.SelectionChanged, Assert.Single(changes).Kind);
        canvas.SelectModuleNodes([second.Id], additive: true);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), canvas.Snapshot.SelectedNodeIds);
        canvas.SelectModuleNodes([second.Id], additive: true);
        Assert.Equal(2, changes.Count);
        Assert.Throws<KeyNotFoundException>(() => canvas.SelectModuleNodes([first.Id, Guid.NewGuid()]));
        Assert.Equal(2, changes.Count);
        Assert.Same(original, session.Snapshot);
        Assert.Equal(before.Viewport, canvas.Snapshot.Viewport);
        Assert.Equal(before.NodePositions, canvas.Snapshot.NodePositions);
        Assert.Empty(canvas.Snapshot.SelectedConnectionIds);
    }

    [Fact]
    public void MarqueeUsesMeasuredIntersectionAndExcludesProtectedNodes()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var inside = Add(session, OscillatorGenerator.NodeType);
        var touching = Add(session, OscillatorGenerator.NodeType);
        var master = Add(session, MasterOutput.NodeType);
        var composite = Add(session, CompositeNode.NodeType);
        var viewport = new CanvasViewport(new CanvasPoint(100, 60), 0.5);
        var rect = ModuleSelection.Rectangle(viewport.ScreenToWorld(new(150, 110)), viewport.ScreenToWorld(new(100, 60)));
        var measured = new Dictionary<Guid, CanvasBounds>
        {
            [inside.Id] = new(50, 50, 150, 150),
            [touching.Id] = new(100, 10, 200, 70),
            [master.Id] = new(5, 5, 10, 10),
            [composite.Id] = new(-20, 20, 5, 40)
        };
        Assert.Equal(new[] { inside.Id, composite.Id }, ModuleSelection.HitTest(session.Snapshot, rect, measured));
        Assert.Empty(ModuleSelection.HitTest(session.Snapshot, new(0, 0, 0, 10), measured));
        Assert.Empty(ModuleSelection.HitTest(session.Snapshot, rect, new Dictionary<Guid, CanvasBounds>()));
    }

    [Fact]
    public void NestedBulkSelectionExcludesRacksAndPreservesParentSelection()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var outer = Add(session, CompositeNode.NodeType);
        using var nav = new CanvasNavigationService(session);
        nav.ActiveCanvas.SelectNode(outer.Id);
        Assert.True(nav.OpenChild(outer.Id));
        var child = nav.ActiveCanvas;
        child.SelectModuleNodes(child.Snapshot.Graph.Nodes.Select(node => node.Id));
        Assert.NotEmpty(child.Snapshot.SelectedNodeIds);
        Assert.All(child.Snapshot.SelectedNodeIds, id => Assert.True(ModuleSelection.IsEligible(child.Snapshot.Graph.GetNode(id))));
        Assert.Throws<KeyNotFoundException>(() => child.SelectModuleNodes([outer.Id]));
        Assert.True(nav.NavigateTo(session.GraphId));
        Assert.Equal(outer.Id, Assert.Single(nav.ActiveCanvas.Snapshot.SelectedNodeIds));
    }
}
