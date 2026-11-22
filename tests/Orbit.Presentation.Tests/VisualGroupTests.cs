using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class VisualGroupTests
{
    private static readonly CanvasBounds Bounds = new(0, 0, 800, 600);
    private static Guid Add(PatchSession session, Guid graph, string type = OscillatorGenerator.NodeType) =>
        session.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node.Id;

    [Fact]
    public void MoveIsAtomicAndResizeAndDeleteAffectOnlyFrame()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var a = Add(session, session.GraphId);
        var b = Add(session, session.GraphId);
        var outside = Add(session, session.GraphId);
        using var canvas = new CanvasStateService(session);
        canvas.MoveNode(outside, new CanvasPoint(100, 100));
        canvas.AddNote(Bounds, []);
        var note = Assert.Single(canvas.Snapshot.Notes);
        canvas.SelectNode(a);
        canvas.SelectNode(b, additive: true);
        canvas.AddGroup(Bounds);
        var group = Assert.Single(canvas.Snapshot.VisualGroups);
        canvas.UpdateGroup(group with { Title = "Voices", NoteIds = [note.Id] });
        var before = canvas.Snapshot;
        var graph = session.Snapshot;
        var delta = new CanvasPoint(42, -17);
        canvas.MoveGroup(group.Id, delta);
        var after = canvas.Snapshot;
        Assert.Equal(delta.X, Assert.Single(after.VisualGroups).X);
        Assert.Equal(delta.Y, Assert.Single(after.VisualGroups).Y);
        Assert.Equal(before.NodePositions[a].Add(delta), after.NodePositions[a]);
        Assert.Equal(before.NodePositions[b].Add(delta), after.NodePositions[b]);
        Assert.Equal(before.NodePositions[outside], after.NodePositions[outside]);
        Assert.Equal(note.X + delta.X, Assert.Single(after.Notes).X);
        Assert.Equal(note.Y + delta.Y, Assert.Single(after.Notes).Y);
        canvas.UndoAnnotation();
        Assert.Equal(before.NodePositions[a], canvas.Snapshot.NodePositions[a]);
        Assert.Equal(note, Assert.Single(canvas.Snapshot.Notes));
        Assert.Equal(0, Assert.Single(canvas.Snapshot.VisualGroups).X);
        canvas.RedoAnnotation();
        Assert.Equal(after.NodePositions[a], canvas.Snapshot.NodePositions[a]);
        canvas.UpdateGroup(Assert.Single(after.VisualGroups) with { Width = 1000 });
        Assert.Equal(after.NodePositions[a], canvas.Snapshot.NodePositions[a]);
        Assert.Equal(after.Notes[0], canvas.Snapshot.Notes[0]);
        canvas.DeleteGroup(group.Id);
        Assert.Empty(canvas.Snapshot.VisualGroups);
        Assert.Equal(3, canvas.Snapshot.NodePositions.Count);
        Assert.Single(canvas.Snapshot.Notes);
        canvas.UndoAnnotation();
        Assert.Equal(1000, Assert.Single(canvas.Snapshot.VisualGroups).Width);
        Assert.Equal(800, Assert.Single(before.VisualGroups).Width);
        Assert.Same(graph, session.Snapshot);
    }

    [Fact]
    public void DeletedMembersReconcileAndOnlyOriginalIdsReturnOnUndo()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, session.GraphId);
        using var nav = new CanvasNavigationService(session);
        var canvas = nav.ActiveCanvas;
        canvas.AddNote(Bounds, []);
        var note = Assert.Single(canvas.Snapshot.Notes);
        canvas.SelectNode(node);
        canvas.AddGroup(Bounds);
        var group = Assert.Single(canvas.Snapshot.VisualGroups);
        canvas.UpdateGroup(group with { NoteIds = [note.Id] });
        canvas.SelectNode(node);
        Assert.Throws<ArgumentException>(() => canvas.AddGroup(Bounds));
        Assert.Throws<ArgumentException>(() => canvas.UpdateGroup(group with { NodeIds = [Guid.NewGuid()] }));
        var position = canvas.Snapshot.NodePositions[node];
        canvas.MoveGroup(group.Id, new CanvasPoint(20, 20));
        session.Execute(new RemoveNodeCommand(session.GraphId, node));
        Assert.Empty(Assert.Single(canvas.Snapshot.VisualGroups).NodeIds);
        Assert.Empty(Assert.Single(Assert.Single(nav.CaptureLayouts()).VisualGroups).NodeIds);
        canvas.UndoAnnotation();
        session.Undo();
        Assert.Equal(node, Assert.Single(Assert.Single(canvas.Snapshot.VisualGroups).NodeIds));
        Assert.Equal(position, canvas.Snapshot.NodePositions[node]);
        canvas.DeleteNote(note.Id);
        Assert.Empty(Assert.Single(canvas.Snapshot.VisualGroups).NoteIds);
        canvas.UndoAnnotation();
        Assert.Equal(note.Id, Assert.Single(Assert.Single(canvas.Snapshot.VisualGroups).NoteIds));
    }

    [Fact]
    public void RootAndNestedGroupsRoundTripAndInvalidMembershipIsRejected()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var composite = Add(session, session.GraphId, CompositeNode.NodeType);
        using var nav = new CanvasNavigationService(session);
        nav.ActiveCanvas.SelectNode(composite);
        nav.ActiveCanvas.AddGroup(Bounds);
        var root = Assert.Single(nav.ActiveCanvas.Snapshot.VisualGroups);
        Assert.True(nav.OpenChild(composite));
        var childNode = Add(session, nav.ActiveCanvas.Snapshot.GraphId);
        nav.ActiveCanvas.SelectNode(childNode);
        nav.ActiveCanvas.AddGroup(Bounds);
        var child = Assert.Single(nav.ActiveCanvas.Snapshot.VisualGroups);
        var serializer = new PatchDocumentSerializer();
        var saved = serializer.Capture(session, 120, nav.ActiveCanvas.Snapshot.GraphId, nav.CaptureLayouts());
        var document = serializer.Deserialize(serializer.Serialize(saved));
        using var restored = serializer.Restore(document, registry);
        using var loaded = new CanvasNavigationService(restored, layouts: document.CanvasLayouts, activeGraphId: document.ActiveGraphId);
        Assert.Equal(child.Id, Assert.Single(loaded.ActiveCanvas.Snapshot.VisualGroups).Id);
        Assert.Equal(childNode, Assert.Single(Assert.Single(loaded.ActiveCanvas.Snapshot.VisualGroups).NodeIds));
        loaded.NavigateTo(restored.GraphId);
        Assert.Equal(root.Id, Assert.Single(loaded.ActiveCanvas.Snapshot.VisualGroups).Id);
        session.Execute(new RemoveNodeCommand(session.GraphId, composite));
        session.Undo();
        Assert.True(nav.OpenChild(composite));
        Assert.Equal(child.Id, Assert.Single(nav.ActiveCanvas.Snapshot.VisualGroups).Id);
        var layout = document.CanvasLayouts.Single(l => l.GraphId == session.GraphId);
        foreach (var invalid in new[]
        {
            layout with { VisualGroups = [root, root] },
            layout with { VisualGroups = [root, root with { Id = Guid.NewGuid() }] },
            layout with { VisualGroups = [root with { NodeIds = [childNode] }] },
            layout with { VisualGroups = [root with { Width = double.NaN }] },
            layout with { VisualGroups = [root with { NoteIds = null! }] },
            layout with { GraphId = Guid.NewGuid(), VisualGroups = [root with { NodeIds = [] }] }
        })
            Assert.ThrowsAny<ArgumentException>(() => serializer.Restore(document with { CanvasLayouts = [invalid] }, registry));
        var old = serializer.Capture(session, 120, session.GraphId, [new SavedCanvas(session.GraphId, 0, 0, 1, [])]);
        Assert.Empty(Assert.Single(serializer.Deserialize(serializer.Serialize(old).Replace(",\"visualGroups\":[]", "")).CanvasLayouts).VisualGroups);
    }
}
