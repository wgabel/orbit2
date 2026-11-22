using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Composites;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class CanvasNoteTests
{
    private static readonly CanvasBounds Visible = new(1000, 800, 2000, 1600);

    [Fact]
    public void NoteOperationsDoNotChangeGraphOrGraphHistory()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        using var canvas = new CanvasStateService(session);
        var graph = session.Snapshot;
        var changes = new List<CanvasStateChange>();
        using var subscription = canvas.Subscribe(changes.Add);
        canvas.AddNote(Visible, []);
        var initial = canvas.Snapshot;
        var note = Assert.Single(initial.Notes);
        Assert.Equal(note.Id, initial.SelectedNoteId);
        Assert.InRange(note.X, Visible.Left, Visible.Right - note.Width);
        Assert.InRange(note.Y, Visible.Top, Visible.Bottom - note.Height);
        var edited = note with { Title = "Harmony", Body = "A minor\nOpen fifth", Accent = NoteAccent.Idea,
            Width = 450, Height = 300, Collapsed = true };
        canvas.UpdateNote(edited);
        canvas.UpdateNote(edited);
        canvas.DuplicateNote(note.Id);
        var copy = Assert.Single(canvas.Snapshot.Notes, n => n.Id != note.Id);
        Assert.Equal(edited.Body, copy.Body);
        canvas.DeleteNote(copy.Id);
        canvas.UndoNote();
        Assert.Equal(2, canvas.Snapshot.Notes.Count);
        canvas.UndoNote();
        Assert.Equal(edited, Assert.Single(canvas.Snapshot.Notes));
        canvas.UndoNote();
        Assert.Equal(note, Assert.Single(canvas.Snapshot.Notes));
        canvas.RedoNote();
        Assert.Equal(edited, Assert.Single(canvas.Snapshot.Notes));
        Assert.Equal(note, Assert.Single(initial.Notes));
        Assert.Same(graph, session.Snapshot);
        Assert.False(session.CanUndo);
        Assert.All(changes, change => Assert.Equal(CanvasChangeKind.NotesChanged, change.Kind));
        Assert.Throws<ArgumentException>(() => canvas.UpdateNote(edited with { Width = double.NaN }));
        Assert.Equal(edited, Assert.Single(canvas.Snapshot.Notes));
        canvas.UpdateNote(edited with { Collapsed = false });
        Assert.False(canvas.Snapshot.CanRedoNotes);
    }

    [Fact]
    public void RootAndChildNotesRoundTripAndGraphUndoRestoresRemovedChildLayout()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var composite = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        using var navigation = new CanvasNavigationService(session);
        navigation.ActiveCanvas.AddNote(Visible, []);
        var rootNote = Assert.Single(navigation.ActiveCanvas.Snapshot.Notes);
        navigation.OpenChild(composite.Id);
        navigation.ActiveCanvas.AddNote(Visible, []);
        var childNote = Assert.Single(navigation.ActiveCanvas.Snapshot.Notes) with { Body = "<script>plain text</script>\nLine 2" };
        navigation.ActiveCanvas.UpdateNote(childNote);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, navigation.ActiveCanvas.Snapshot.GraphId, navigation.CaptureLayouts());
        var restored = serializer.Deserialize(serializer.Serialize(document));
        using var loaded = serializer.Restore(restored, registry);
        using var loadedNav = new CanvasNavigationService(loaded, layouts: restored.CanvasLayouts, activeGraphId: restored.ActiveGraphId);
        Assert.Equal(childNote, Assert.Single(loadedNav.ActiveCanvas.Snapshot.Notes));
        Assert.False(loadedNav.ActiveCanvas.Snapshot.CanUndoNotes);
        loadedNav.NavigateTo(loaded.GraphId);
        Assert.Equal(rootNote, Assert.Single(loadedNav.ActiveCanvas.Snapshot.Notes));
        session.Execute(new RemoveNodeCommand(session.GraphId, composite.Id));
        Assert.Single(navigation.CaptureLayouts());
        session.Undo();
        Assert.True(navigation.OpenChild(composite.Id));
        Assert.Equal(childNote, Assert.Single(navigation.ActiveCanvas.Snapshot.Notes));
    }

    [Fact]
    public void PlacementAvoidsNotesAndHistoryIsBounded()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        using var canvas = new CanvasStateService(session);
        canvas.AddNote(Visible, []);
        canvas.AddNote(Visible, []);
        var first = CanvasStateService.NoteBounds(canvas.Snapshot.Notes[0]);
        var second = CanvasStateService.NoteBounds(canvas.Snapshot.Notes[1]);
        Assert.True(first.Right <= second.Left || second.Right <= first.Left ||
            first.Bottom <= second.Top || second.Bottom <= first.Top);
        var note = canvas.Snapshot.Notes[1];
        for (var step = 0; step < 110; step++)
            canvas.UpdateNote(note with { Title = $"Edit {step}" });
        var undoCount = 0;
        while (canvas.Snapshot.CanUndoNotes)
        {
            canvas.UndoNote();
            undoCount++;
        }
        Assert.Equal(100, undoCount);
        Assert.Equal("Edit 9", canvas.Snapshot.Notes[1].Title);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void InvalidNoteDocumentsAreRejectedAndOldLayoutsDefaultToEmpty()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var serializer = new PatchDocumentSerializer();
        var layout = new SavedCanvas(session.GraphId, 0, 0, 1, []);
        var document = serializer.Capture(session, 120, session.GraphId, [layout]);
        var oldJson = serializer.Serialize(document).Replace(",\"notes\":[]", "");
        Assert.Empty(Assert.Single(serializer.Deserialize(oldJson).CanvasLayouts).Notes);
        var note = new SavedNote(Guid.NewGuid(), "Title", "Body", NoteAccent.Neutral, 0, 0, 300, 200, false);
        foreach (var invalid in new[]
        {
            layout with { Notes = [note, note] },
            layout with { Notes = [note with { Body = new string('x', SavedNote.MaxBodyLength + 1) }] },
            layout with { Notes = [note with { Accent = (NoteAccent)99 }] },
            layout with { GraphId = Guid.NewGuid(), Notes = [note] },
            layout with { Notes = null! }
        })
            Assert.ThrowsAny<ArgumentException>(() => serializer.Restore(document with { CanvasLayouts = [invalid] }, registry));
    }
}
