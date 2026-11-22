using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class CompositeUngroupingLayoutTests
{
    [Fact]
    public void ExpandsCurrentEditedChildLayoutAndMetadataWithoutChangingNeighbors()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var s = new PatchSession(registry);
        var root = s.GraphId;
        var a = Add(s, OscillatorGenerator.NodeType);
        var b = Add(s, GainProcessor.NodeType);
        var master = Add(s, MasterOutput.NodeType);
        var edge = s.Execute(new ConnectNodesCommand(root, new(a.Id, a.Outputs[0].Id), new(b.Id, b.Inputs.Single(p => p.Key == "audio-in").Id)))
            .ChangeSet!.Events.OfType<ConnectionCreated>().Single().Connection;
        using var nav = new CanvasNavigationService(s);
        var parent = nav.ActiveCanvas;
        parent.SetNodeDescription(new SavedNodeDescription(a.Id, "Old alias", ""));
        parent.SetConnectionDescription(new SavedConnectionDescription(edge.Id, "Old cable"));
        var neighbor = parent.Snapshot.NodePositions[master.Id];
        var wrapper = s.Execute(new GroupNodesCommand(root, [a.Id,b.Id], "Pair", s.Snapshot.Revision)).ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
        parent.MoveNode(wrapper.Id, new CanvasPoint(800, 500));
        nav.OpenChild(wrapper.Id);
        var child = nav.ActiveCanvas;
        child.MoveNode(a.Id, new CanvasPoint(300, 100));
        child.MoveNode(b.Id, new CanvasPoint(810, 260));
        child.SetNodeDescription(new SavedNodeDescription(a.Id, "New alias", "Edited inside"));
        child.SetConnectionDescription(new SavedConnectionDescription(edge.Id, "New cable"));
        nav.NavigateTo(root);
        var result = nav.UngroupComposite(new UngroupCompositeCommand(root, wrapper.Id, s.Snapshot.Revision));
        Assert.Empty(result.SubscriberErrors);
        Assert.Equal(new CanvasPoint(800,500), parent.Snapshot.NodePositions[a.Id]);
        Assert.Equal(new CanvasPoint(1310,660), parent.Snapshot.NodePositions[b.Id]);
        Assert.Equal(neighbor, parent.Snapshot.NodePositions[master.Id]);
        Assert.Equal("New alias", parent.Snapshot.DisplayName(a.Id));
        Assert.Equal("New cable", parent.Snapshot.ConnectionDescriptions[edge.Id].Label);
        Assert.True(new[] { a.Id,b.Id }.ToHashSet().SetEquals(parent.Snapshot.SelectedNodeIds));
        Assert.Empty(s.Undo().SubscriberErrors);
        Assert.True(nav.OpenChild(wrapper.Id));
        Assert.Equal(new CanvasPoint(810,260), nav.ActiveCanvas.Snapshot.NodePositions[b.Id]);
        Assert.Equal("New alias", nav.ActiveCanvas.Snapshot.DisplayName(a.Id));
        Assert.Empty(s.Redo().SubscriberErrors);
        Assert.Same(parent, nav.ActiveCanvas);
        Assert.Equal(new CanvasPoint(1310,660), parent.Snapshot.NodePositions[b.Id]);
        Assert.Equal("New cable", parent.Snapshot.ConnectionDescriptions[edge.Id].Label);
        var serializer = new PatchDocumentSerializer();
        using var restored = serializer.Restore(serializer.Capture(s, 120, root, nav.CaptureLayouts()), registry);
        Assert.Equal(s.Snapshot.Nodes.Select(n => n.Id), restored.Snapshot.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void ChildNotesAndWrapperDescriptionsPreventSilentDataLoss()
    {
        using var s = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var root = s.GraphId;
        var osc = Add(s, OscillatorGenerator.NodeType);
        var wrapper = s.Execute(new GroupNodesCommand(root, [osc.Id], "Voice", s.Snapshot.Revision)).ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
        using var nav = new CanvasNavigationService(s);
        nav.ActiveCanvas.SetNodeDescription(new SavedNodeDescription(wrapper.Id, "Important", "Do not lose"));
        var before = s.Snapshot;
        Assert.Throws<GraphValidationException>(() => nav.UngroupComposite(new(root, wrapper.Id, before.Revision)));
        Assert.Same(before, s.Snapshot);
        nav.ActiveCanvas.SetNodeDescription(new SavedNodeDescription(wrapper.Id, "", ""));
        nav.OpenChild(wrapper.Id);
        nav.ActiveCanvas.AddNote(new CanvasBounds(0,0,1000,800), []);
        nav.NavigateTo(root);
        Assert.Throws<GraphValidationException>(() => nav.UngroupComposite(new(root, wrapper.Id, before.Revision)));
        Assert.Same(before, s.Snapshot);
        Assert.True(nav.OpenChild(wrapper.Id));
        Assert.Single(nav.ActiveCanvas.Snapshot.Notes);
    }

    [Fact]
    public void ExpandingAncestorKeepsStillLiveDescendantCanvasAndRebuildsBreadcrumbs()
    {
        using var s = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var root = s.GraphId;
        var osc = Add(s, OscillatorGenerator.NodeType);
        using var nav = new CanvasNavigationService(s);
        var nested = s.Execute(new GroupNodesCommand(root, [osc.Id], "Inner", s.Snapshot.Revision)).ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
        var outer = s.Execute(new GroupNodesCommand(root, [nested.Id], "Outer", s.Snapshot.Revision)).ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
        nav.OpenChild(outer.Id);
        nav.OpenChild(nested.Id);
        var innerCanvas = nav.ActiveCanvas;
        Assert.Equal(3, nav.Path.Count);
        var result = nav.UngroupComposite(new(root, outer.Id, s.Snapshot.Revision));
        Assert.Empty(result.SubscriberErrors);
        Assert.Same(innerCanvas, nav.ActiveCanvas);
        Assert.Equal(new[] { root, nested.Composite!.Graph.Id }, nav.Path.Select(crumb => crumb.GraphId));
        Assert.Empty(s.Undo().SubscriberErrors);
        Assert.Equal(3, nav.Path.Count);
        Assert.Same(innerCanvas, nav.ActiveCanvas);
    }

    private static Orbit.Application.Graphs.Snapshots.GraphNodeSnapshot Add(PatchSession s, string type) => s.Execute(new AddNodeCommand(s.GraphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
}
