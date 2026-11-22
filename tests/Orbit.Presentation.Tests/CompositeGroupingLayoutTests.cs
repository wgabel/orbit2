using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class CompositeGroupingLayoutTests
{
    [Fact]
    public void GroupMovesRelativeLayoutAndDescriptionsAndHistoryRestoresBothCanvases()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var s = new PatchSession(registry);
        Guid Add(string type) => s.Execute(new AddNodeCommand(s.GraphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node.Id;
        var a = Add(OscillatorGenerator.NodeType);
        var b = Add(OscillatorGenerator.NodeType);
        var master = Add(MasterOutput.NodeType);
        using var nav = new CanvasNavigationService(s);
        var parent = nav.ActiveCanvas;
        parent.MoveNode(a, new CanvasPoint(120, 45));
        parent.MoveNode(b, new CanvasPoint(540, 175));
        parent.SetNodeDescription(new SavedNodeDescription(a, "Voice A", "Harmonic bed"));
        var masterPosition = parent.Snapshot.NodePositions[master];
        var result = s.Execute(new GroupNodesCommand(s.GraphId, [a,b], "Pair", s.Snapshot.Revision));
        Assert.Empty(result.SubscriberErrors);
        var wrapper = result.ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
        Assert.Equal(new CanvasPoint(120,45), parent.Snapshot.NodePositions[wrapper.Id]);
        Assert.Equal(masterPosition, parent.Snapshot.NodePositions[master]);
        Assert.Equal(wrapper.Id, Assert.Single(parent.Snapshot.SelectedNodeIds));
        Assert.True(nav.OpenChild(wrapper.Id));
        var child = nav.ActiveCanvas;
        Assert.Equal(new CanvasPoint(420,130), child.Snapshot.NodePositions[b].Subtract(child.Snapshot.NodePositions[a]));
        Assert.Equal("Voice A", child.Snapshot.DisplayName(a));
        var childPosition = child.Snapshot.NodePositions[a];
        Assert.Empty(s.Undo().SubscriberErrors);
        Assert.Same(parent, nav.ActiveCanvas);
        Assert.Equal(new CanvasPoint(120,45), parent.Snapshot.NodePositions[a]);
        Assert.Equal("Voice A", parent.Snapshot.DisplayName(a));
        Assert.Empty(s.Redo().SubscriberErrors);
        Assert.True(nav.OpenChild(wrapper.Id));
        Assert.Equal(childPosition, nav.ActiveCanvas.Snapshot.NodePositions[a]);
        Assert.Equal("Voice A", nav.ActiveCanvas.Snapshot.DisplayName(a));
        var serializer = new PatchDocumentSerializer();
        using var restored = serializer.Restore(serializer.Capture(s, 120, nav.ActiveCanvas.Snapshot.GraphId, nav.CaptureLayouts()), registry);
        Assert.NotNull(restored.Snapshot.FindGraph(wrapper.Composite!.Graph.Id));
    }
}
