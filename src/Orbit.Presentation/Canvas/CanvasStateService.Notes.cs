using Orbit.Application.Persistence;

namespace Orbit.Presentation.Canvas;

public sealed partial class CanvasStateService
{
    private const int AnnotationHistoryLimit = 100;
    private sealed record AnnotationEdit(CanvasChangeKind Kind, Func<bool, CanvasStateSnapshot> Apply);
    private readonly List<AnnotationEdit> annotationUndo = [];
    private readonly List<AnnotationEdit> annotationRedo = [];

    public static CanvasBounds NoteBounds(SavedNote note) =>
        new(note.X, note.Y, note.X + note.Width, note.Y + (note.Collapsed ? SavedNote.CollapsedHeight : note.Height));

    public CanvasChangeResult AddNote(CanvasBounds visible, IEnumerable<CanvasBounds> occupied)
    {
        EnsureEditable();
        var point = placement.FindVisiblePosition(visible, occupied.Concat(snapshot.Notes.Select(NoteBounds)), 300, 200);
        var note = new SavedNote(Guid.NewGuid(), "Note", "", NoteAccent.Neutral,
            Math.Clamp(point.X, -SavedNote.MaxCoordinate, SavedNote.MaxCoordinate),
            Math.Clamp(point.Y, -SavedNote.MaxCoordinate, SavedNote.MaxCoordinate), 300, 200, false);
        return EditNotes([.. snapshot.Notes, note], note.Id);
    }

    public CanvasChangeResult UpdateNote(SavedNote note)
    {
        EnsureEditable();
        _ = GetNote(note.Id);
        return EditNotes(snapshot.Notes.Select(existing => existing.Id == note.Id ? note : existing).ToArray(), note.Id);
    }

    public CanvasChangeResult SelectNote(Guid id)
    {
        EnsureEditable();
        _ = GetNote(id);
        if (snapshot.SelectedNoteId == id) return NoChange();
        return CommitLayout(CanvasChangeKind.SelectionChanged, snapshot with
        {
            SelectedNoteId = id, SelectedNodeIds = [], SelectedConnectionIds = []
        });
    }

    public CanvasChangeResult DeleteNote(Guid id)
    {
        EnsureEditable();
        _ = GetNote(id);
        return EditNotes(snapshot.Notes.Where(note => note.Id != id).ToArray(),
            snapshot.SelectedNoteId == id ? null : snapshot.SelectedNoteId);
    }

    public CanvasChangeResult DuplicateNote(Guid id)
    {
        EnsureEditable();
        var source = GetNote(id);
        var copy = source with { Id = Guid.NewGuid(),
            X = Math.Clamp(source.X + 32, -SavedNote.MaxCoordinate, SavedNote.MaxCoordinate),
            Y = Math.Clamp(source.Y + 32, -SavedNote.MaxCoordinate, SavedNote.MaxCoordinate) };
        return EditNotes([.. snapshot.Notes, copy], copy.Id);
    }

    public CanvasChangeResult UndoAnnotation() => RestoreAnnotation(annotationUndo, annotationRedo, false);
    public CanvasChangeResult RedoAnnotation() => RestoreAnnotation(annotationRedo, annotationUndo, true);

    private SavedNote GetNote(Guid id) => snapshot.Notes.FirstOrDefault(note => note.Id == id)
        ?? throw new KeyNotFoundException($"Note {id} does not exist in this canvas.");

    private CanvasChangeResult EditNotes(SavedNote[] notes, Guid? selected)
    {
        SavedNote.Validate(notes);
        if (snapshot.Notes.SequenceEqual(notes)) return NoChange();
        var before = snapshot.Notes;
        var after = Array.AsReadOnly(notes);
        return CommitAnnotation(new AnnotationEdit(CanvasChangeKind.NotesChanged, forward =>
        {
            var current = forward ? after : before;
            var id = forward ? selected : snapshot.SelectedNoteId;
            return NoteState(current, current.Any(note => note.Id == id) ? id : null);
        }));
    }

    private CanvasChangeResult CommitAnnotation(AnnotationEdit edit)
    {
        var state = edit.Apply(true);
        annotationUndo.Add(edit);
        if (annotationUndo.Count > AnnotationHistoryLimit) annotationUndo.RemoveAt(0);
        annotationRedo.Clear();
        return PublishAnnotation(edit.Kind, state);
    }

    private CanvasChangeResult RestoreAnnotation(List<AnnotationEdit> from, List<AnnotationEdit> to, bool forward)
    {
        EnsureEditable();
        if (from.Count == 0) return NoChange();
        var edit = from[^1];
        var state = edit.Apply(forward);
        from.RemoveAt(from.Count - 1);
        to.Add(edit);
        return PublishAnnotation(edit.Kind, state);
    }

    private CanvasStateSnapshot NoteState(IReadOnlyList<SavedNote> notes, Guid? selected) =>
        snapshot with
        {
            Notes = notes, SelectedNoteId = selected,
            SelectedNodeIds = selected is null ? snapshot.SelectedNodeIds : [],
            SelectedConnectionIds = selected is null ? snapshot.SelectedConnectionIds : []
        };

    private CanvasChangeResult PublishAnnotation(CanvasChangeKind kind, CanvasStateSnapshot state) =>
        CommitLayout(kind, state with { CanUndoAnnotations = annotationUndo.Count > 0, CanRedoAnnotations = annotationRedo.Count > 0 });
}
