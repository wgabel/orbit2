using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class PatchOutlineTests
{
    private static readonly CanvasBounds Bounds = new(0, 0, 800, 600);
    private static GraphNodeSnapshot Add(PatchSession session, Guid graphId, string type = CompositeNode.NodeType) =>
        session.Execute(new AddNodeCommand(graphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    [Fact]
    public void BrowseGroupsAndSearchUnvisitedNestedGraphsWithoutDuplicateEntries()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var outer = Add(session, session.GraphId);
        var sibling = Add(session, session.GraphId);
        var inner = Add(session, outer.Composite!.Graph.Id);
        using var nav = new CanvasNavigationService(session);
        var canvas = nav.ActiveCanvas;
        canvas.SetNodeDescription(new SavedNodeDescription(outer.Id, "Harmonic bed", "Deep space"));
        canvas.AddNote(Bounds, []);
        var note = Assert.Single(canvas.Snapshot.Notes);
        canvas.UpdateNote(note with { Title = "Score", Body = "Dorian progression" });
        canvas.SelectNode(outer.Id);
        canvas.AddGroup(Bounds);
        var group = Assert.Single(canvas.Snapshot.VisualGroups);
        canvas.UpdateGroup(group with { Title = "Foundation", NoteIds = [note.Id] });
        var outline = nav.GetOutline("Distant horizon");
        Assert.Equal("Distant horizon", outline.Root.Title);
        var rootRows = outline.Rows(new HashSet<OutlineTarget> { outline.Root.Target }, "", null).ToArray();
        Assert.Equal(3, rootRows.Length); // root, group, sibling wrapper
        var groupEntry = outline.Find(new OutlineTarget(session.GraphId, OutlineKind.Group, group.Id))!;
        Assert.Equal(new[] { outer.Id, note.Id }, groupEntry.Children.Select(entry => entry.Target.Id));
        Assert.Equal(outer.Composite.Graph.Id, groupEntry.Children[0].ChildGraphId);
        Assert.Single(outline.Rows(new HashSet<OutlineTarget>(), "dOrIaN", OutlineKind.Note));
        Assert.Single(outline.Rows(new HashSet<OutlineTarget>(), "deep space", OutlineKind.Composite));
        var oscillators = outline.Rows(new HashSet<OutlineTarget>(), "oscillator", OutlineKind.Node).ToArray();
        Assert.Equal(3, oscillators.Length);
        Assert.Contains(oscillators, row => row.Entry.Target.GraphId == inner.Composite!.Graph.Id && row.Entry.Breadcrumb.Contains("Harmonic bed"));
        Assert.Contains(oscillators, row => row.Entry.Target.GraphId == sibling.Composite!.Graph.Id);
        Assert.Equal(outline.Count, outline.Rows(new HashSet<OutlineTarget>(), "Distant horizon", null).Select(row => row.Entry.Target).Distinct().Count());
        canvas.PanBy(new CanvasPoint(10, 20));
        canvas.SetAnnotationVisibility(AnnotationVisibility.Hide);
        Assert.Same(outline, nav.GetOutline("Distant horizon"));
        canvas.UpdateNote(note with { Title = "Revised", Body = "Phrygian" });
        Assert.Empty(nav.GetOutline("Distant horizon").Rows(new HashSet<OutlineTarget>(), "dorian", OutlineKind.Note));
        canvas.UndoAnnotation();
        Assert.Single(nav.GetOutline("Distant horizon").Rows(new HashSet<OutlineTarget>(), "dorian", OutlineKind.Note));
    }

    [Fact]
    public void JumpAcrossHierarchyAndRestoreHiddenTargetsAfterDeletionAndImport()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var outer = Add(session, session.GraphId);
        var sibling = Add(session, session.GraphId);
        var inner = Add(session, outer.Composite!.Graph.Id);
        var innerId = inner.Composite!.Graph.Id;
        using var nav = new CanvasNavigationService(session);
        var root = nav.ActiveCanvas;
        root.SetNodeDescription(new SavedNodeDescription(outer.Id, "Foundation", ""));
        root.PanBy(new CanvasPoint(20, 30));
        var viewport = root.Snapshot.Viewport;
        var graph = session.Snapshot;
        Assert.True(nav.JumpToGraph(innerId));
        Assert.Equal(new[] { session.GraphId, outer.Composite.Graph.Id, innerId }, nav.Path.Select(item => item.GraphId));
        Assert.Equal("Foundation", nav.Path[1].Name);
        var canvas = nav.ActiveCanvas;
        canvas.AddNote(Bounds, []);
        var note = Assert.Single(canvas.Snapshot.Notes);
        canvas.SetAnnotationVisibility(AnnotationVisibility.Hide);
        var target = new OutlineTarget(innerId, OutlineKind.Note, note.Id);
        Assert.NotNull(nav.GetOutline("").Find(target));
        Assert.True(nav.JumpToGraph(sibling.Composite!.Graph.Id));
        Assert.True(nav.JumpToGraph(innerId));
        Assert.Same(canvas, nav.ActiveCanvas);
        Assert.Equal(AnnotationVisibility.Hide, canvas.Snapshot.AnnotationVisibility);
        canvas.SelectNote(note.Id);
        Assert.Equal(AnnotationDetail.Full, canvas.Snapshot.NoteDetail(note.Id));
        Assert.True(nav.NavigateTo(session.GraphId));
        Assert.Equal(viewport, root.Snapshot.Viewport);
        Assert.Same(graph, session.Snapshot);
        var serializer = new PatchDocumentSerializer();
        using var restored = serializer.Restore(serializer.Capture(session, 120, session.GraphId, nav.CaptureLayouts()), registry);
        // Saved child layouts must be indexed before the child has ever been opened.
        using var restoredNav = new CanvasNavigationService(restored, layouts: nav.CaptureLayouts());
        Assert.NotNull(restoredNav.GetOutline("").Find(target));
        session.Execute(new RemoveNodeCommand(session.GraphId, outer.Id));
        Assert.Null(nav.GetOutline("").Find(target));
        Assert.False(nav.JumpToGraph(innerId));
        session.Undo();
        Assert.NotNull(nav.GetOutline("").Find(target));
        Assert.True(nav.JumpToGraph(innerId));
        Assert.NotNull(nav.GetOutline("").Find(target));
        Assert.Equal(AnnotationVisibility.Hide, nav.ActiveCanvas.Snapshot.AnnotationVisibility);
    }
}
