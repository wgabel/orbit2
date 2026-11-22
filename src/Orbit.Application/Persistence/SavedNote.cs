namespace Orbit.Application.Persistence;

public enum NoteAccent { Neutral, Idea, Instruction, Warning }

/// <summary>A graph-owned presentation record, never a signal node.</summary>
public sealed record SavedNote(Guid Id, string Title, string Body, NoteAccent Accent,
    double X, double Y, double Width, double Height, bool Collapsed)
{
    public const int MaxCount = 200;
    public const int MaxTitleLength = 120;
    public const int MaxBodyLength = 16000;
    public const double MaxCoordinate = 1000000;
    public const double MinWidth = 220;
    public const double MinHeight = 150;
    public const double MaxSize = 1200;
    public const double CollapsedHeight = 44;

    public static void Validate(IEnumerable<SavedNote> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var ids = new HashSet<Guid>();
        foreach (var note in notes)
        {
            if (note is null || note.Id == Guid.Empty || !ids.Add(note.Id) || ids.Count > MaxCount ||
                note.Title is null || note.Title.Length > MaxTitleLength ||
                note.Body is null || note.Body.Length > MaxBodyLength || !Enum.IsDefined(note.Accent) ||
                !double.IsFinite(note.X) || Math.Abs(note.X) > MaxCoordinate ||
                !double.IsFinite(note.Y) || Math.Abs(note.Y) > MaxCoordinate ||
                !double.IsFinite(note.Width) || note.Width < MinWidth || note.Width > MaxSize ||
                !double.IsFinite(note.Height) || note.Height < MinHeight || note.Height > MaxSize)
                throw new ArgumentException("Invalid canvas note or note limit exceeded.");
        }
    }
}
