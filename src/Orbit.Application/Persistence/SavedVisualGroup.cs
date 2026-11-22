namespace Orbit.Application.Persistence;

/// <summary>A presentation frame with explicit, graph-local membership.</summary>
public sealed record SavedVisualGroup(Guid Id, string Title, string Description, NoteAccent Accent,
    double X, double Y, double Width, double Height, IReadOnlyList<Guid> NodeIds, IReadOnlyList<Guid> NoteIds)
{
    public const int MaxCount = 200;
    public const int MaxMembers = 512;
    public const double MinWidth = 260;
    public const double MinHeight = 120;
    public const double MaxSize = 2000000;

    public static void Validate(IEnumerable<SavedVisualGroup> groups, IEnumerable<Guid> nodeIds, IEnumerable<Guid> noteIds)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var ids = new HashSet<Guid>();
        var nodes = nodeIds.ToHashSet();
        var notes = noteIds.ToHashSet();
        foreach (var group in groups)
        {
            if (group is null || group.Id == Guid.Empty || !ids.Add(group.Id) || ids.Count > MaxCount ||
                group.Title is null || group.Title.Length > SavedNote.MaxTitleLength ||
                group.Description is null || group.Description.Length > SavedNote.MaxBodyLength ||
                !Enum.IsDefined(group.Accent) || !double.IsFinite(group.X) || Math.Abs(group.X) > SavedNote.MaxCoordinate ||
                !double.IsFinite(group.Y) || Math.Abs(group.Y) > SavedNote.MaxCoordinate ||
                !double.IsFinite(group.Width) || group.Width < MinWidth || group.Width > MaxSize ||
                !double.IsFinite(group.Height) || group.Height < MinHeight || group.Height > MaxSize ||
                group.NodeIds is null || group.NoteIds is null || (long)group.NodeIds.Count + group.NoteIds.Count > MaxMembers)
                throw new ArgumentException("Invalid visual group or group limit exceeded.");
            foreach (var id in group.NodeIds)
                if (!nodes.Remove(id)) throw new ArgumentException("Node is unknown or already belongs to a visual group.");
            foreach (var id in group.NoteIds)
                if (!notes.Remove(id)) throw new ArgumentException("Note is unknown or already belongs to a visual group.");
        }
    }
}
