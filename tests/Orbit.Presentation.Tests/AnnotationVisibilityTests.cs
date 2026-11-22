using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Composites;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class AnnotationVisibilityTests
{
    [Fact]
    public void ZoomUsesHysteresisAndNeverChangesAuthoredContentOrHistory()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        using var canvas = new CanvasStateService(session);
        var graph = session.Snapshot;
        canvas.AddNote(new CanvasBounds(0, 0, 1000, 1000), []);
        var note = Assert.Single(canvas.Snapshot.Notes);
        canvas.UpdateNote(note with { Body = "A minor", Collapsed = true });
        canvas.AddGroup(new CanvasBounds(10, 10, 500, 300));
        var group = Assert.Single(canvas.Snapshot.VisualGroups);
        canvas.ClearSelection();
        var authored = canvas.Snapshot;
        var changes = new List<CanvasChangeKind>();
        using var subscription = canvas.Subscribe(change => changes.Add(change.Kind));

        foreach (var (zoom, expected) in new[]
        {
            (.64, AnnotationDetail.Titles), (.70, AnnotationDetail.Titles),
            (.76, AnnotationDetail.Full), (.34, AnnotationDetail.Marker),
            (.40, AnnotationDetail.Marker), (.46, AnnotationDetail.Titles),
            (1.0, AnnotationDetail.Full)
        })
        {
            canvas.ZoomAt(zoom, CanvasPoint.Zero);
            Assert.Equal(expected, canvas.Snapshot.NoteDetail(note.Id));
            Assert.Equal(expected, canvas.Snapshot.GroupDetail(group.Id));
        }

        canvas.SetAnnotationVisibility(AnnotationVisibility.TitlesOnly);
        Assert.Equal(AnnotationDetail.Titles, canvas.Snapshot.NoteDetail(note.Id));
        Assert.False(canvas.Snapshot.ShowSecondaryAnnotations);
        canvas.SelectNote(note.Id);
        Assert.Equal(AnnotationDetail.Full, canvas.Snapshot.NoteDetail(note.Id));
        canvas.SetAnnotationVisibility(AnnotationVisibility.Hide);
        Assert.Null(canvas.Snapshot.SelectedNoteId);
        Assert.Equal(AnnotationDetail.Hidden, canvas.Snapshot.NoteDetail(note.Id));
        Assert.Equal(AnnotationDetail.Hidden, canvas.Snapshot.GroupDetail(group.Id));
        Assert.Empty(AnnotationDetailPolicy.FitBounds(canvas.Snapshot));
        canvas.SelectGroup(group.Id);
        Assert.Equal(AnnotationDetail.Full, canvas.Snapshot.GroupDetail(group.Id));
        Assert.Equal(CanvasStateService.GroupBounds(group), Assert.Single(AnnotationDetailPolicy.FitBounds(canvas.Snapshot)));
        canvas.ClearSelection();
        Assert.Empty(AnnotationDetailPolicy.FitBounds(canvas.Snapshot));
        Assert.Equal(authored.Notes, canvas.Snapshot.Notes);
        Assert.Equal(authored.VisualGroups, canvas.Snapshot.VisualGroups);
        Assert.Same(graph, session.Snapshot);
        Assert.False(session.CanUndo);
        Assert.All(changes, change => Assert.Contains(change, new[]
        {
            CanvasChangeKind.ViewportChanged, CanvasChangeKind.AnnotationVisibilityChanged, CanvasChangeKind.SelectionChanged
        }));
        canvas.UndoAnnotation();
        Assert.Empty(canvas.Snapshot.VisualGroups);
        Assert.Equal(AnnotationVisibility.Hide, canvas.Snapshot.AnnotationVisibility);
        Assert.True(Assert.Single(canvas.Snapshot.Notes).Collapsed);
    }

    [Fact]
    public void PreferencesRoundTripIndependentlyInNestedCanvasesAndRejectInvalidValues()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var composite = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        using var navigation = new CanvasNavigationService(session);
        navigation.ActiveCanvas.SetAnnotationVisibility(AnnotationVisibility.Hide);
        navigation.ActiveCanvas.SetConnectionLabels(ConnectionLabelMode.Modified);
        navigation.OpenChild(composite.Id);
        Assert.Equal(AnnotationVisibility.Show, navigation.ActiveCanvas.Snapshot.AnnotationVisibility);
        navigation.ActiveCanvas.SetAnnotationVisibility(AnnotationVisibility.TitlesOnly);
        navigation.ActiveCanvas.ZoomAt(.34, CanvasPoint.Zero);
        navigation.ActiveCanvas.AddNote(new CanvasBounds(0, 0, 800, 600), []);
        var note = Assert.Single(navigation.ActiveCanvas.Snapshot.Notes);
        navigation.ActiveCanvas.ClearSelection();
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, navigation.ActiveCanvas.Snapshot.GraphId, navigation.CaptureLayouts());
        var json = serializer.Serialize(document);
        Assert.Contains("\"annotationVisibility\":2", json);
        var restored = serializer.Deserialize(json);
        using var loaded = serializer.Restore(restored, registry);
        using var loadedNav = new CanvasNavigationService(loaded, layouts: restored.CanvasLayouts, activeGraphId: restored.ActiveGraphId);
        Assert.Equal(AnnotationVisibility.TitlesOnly, loadedNav.ActiveCanvas.Snapshot.AnnotationVisibility);
        Assert.Equal(AnnotationDetail.Marker, loadedNav.ActiveCanvas.Snapshot.NoteDetail(note.Id));
        Assert.Equal(note, Assert.Single(loadedNav.ActiveCanvas.Snapshot.Notes));
        Assert.False(loadedNav.ActiveCanvas.Snapshot.CanUndoAnnotations);
        loadedNav.NavigateTo(loaded.GraphId);
        Assert.Equal(AnnotationVisibility.Hide, loadedNav.ActiveCanvas.Snapshot.AnnotationVisibility);
        Assert.Equal(ConnectionLabelMode.Modified, loadedNav.ActiveCanvas.Snapshot.ConnectionLabels);
        Assert.Throws<ArgumentOutOfRangeException>(() => loadedNav.ActiveCanvas.SetAnnotationVisibility((AnnotationVisibility)99));
        Assert.Throws<ArgumentException>(() => serializer.Restore(restored with
        {
            CanvasLayouts = [restored.CanvasLayouts[0] with { AnnotationVisibility = (AnnotationVisibility)99 }]
        }, registry));
        var old = json.Replace(",\"annotationVisibility\":2", "").Replace(",\"annotationVisibility\":1", "");
        Assert.All(serializer.Deserialize(old).CanvasLayouts, layout => Assert.Equal(AnnotationVisibility.Show, layout.AnnotationVisibility));
    }
}
