using Orbit.Application.Persistence;

namespace Orbit.Presentation.Canvas;

public sealed partial class CanvasStateService
{
    private const int NoteHistoryLimit = 100;
    private readonly List<IReadOnlyList<SavedNote>> noteUndo = [];
    private readonly List<IReadOnlyList<SavedNote>> noteRedo = [];

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

    public CanvasChangeResult UndoNote() => RestoreNotes(noteUndo, noteRedo);
    public CanvasChangeResult RedoNote() => RestoreNotes(noteRedo, noteUndo);

    private SavedNote GetNote(Guid id) => snapshot.Notes.FirstOrDefault(note => note.Id == id)
        ?? throw new KeyNotFoundException($"Note {id} does not exist in this canvas.");

    private CanvasChangeResult EditNotes(SavedNote[] notes, Guid? selected)
    {
        SavedNote.Validate(notes);
        if (snapshot.Notes.SequenceEqual(notes)) return NoChange();
        PushNotes(noteUndo);
        noteRedo.Clear();
        return PublishNotes(Array.AsReadOnly(notes), selected);
    }

    private void PushNotes(List<IReadOnlyList<SavedNote>> history)
    {
        history.Add(snapshot.Notes);
        if (history.Count > NoteHistoryLimit) history.RemoveAt(0);
    }

    private CanvasChangeResult RestoreNotes(List<IReadOnlyList<SavedNote>> from, List<IReadOnlyList<SavedNote>> to)
    {
        EnsureEditable();
        if (from.Count == 0) return NoChange();
        var notes = from[^1];
        from.RemoveAt(from.Count - 1);
        PushNotes(to);
        return PublishNotes(notes, notes.Any(note => note.Id == snapshot.SelectedNoteId) ? snapshot.SelectedNoteId : null);
    }

    private CanvasChangeResult PublishNotes(IReadOnlyList<SavedNote> notes, Guid? selected) =>
        CommitLayout(CanvasChangeKind.NotesChanged, snapshot with
        {
            Notes = notes, SelectedNoteId = selected,
            SelectedNodeIds = selected is null ? snapshot.SelectedNodeIds : [],
            SelectedConnectionIds = selected is null ? snapshot.SelectedConnectionIds : [],
            CanUndoNotes = noteUndo.Count > 0, CanRedoNotes = noteRedo.Count > 0
        });
}
