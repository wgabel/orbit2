using Orbit.Application.Persistence;

namespace Orbit.Presentation.Canvas;

public sealed partial class CanvasStateService
{
    // Dormant member references stay in the bounded definitions while a node
    // or note is deleted. Snapshots and exports contain only live members.
    private readonly Dictionary<Guid, SavedVisualGroup> groupDefinitions = [];

    public static CanvasBounds GroupBounds(SavedVisualGroup group) =>
        new(group.X, group.Y, group.X + group.Width, group.Y + group.Height);

    public CanvasChangeResult AddGroup(CanvasBounds bounds)
    {
        EnsureEditable();
        if (!bounds.IsValid) throw new ArgumentException("Invalid group bounds.", nameof(bounds));
        var group = new SavedVisualGroup(Guid.NewGuid(), "Visual group", "", NoteAccent.Neutral,
            bounds.Left, bounds.Top, Math.Max(bounds.Width, SavedVisualGroup.MinWidth),
            Math.Max(bounds.Height, SavedVisualGroup.MinHeight), snapshot.SelectedNodeIds,
            snapshot.SelectedNoteId is Guid note ? [note] : []);
        ValidateGroupChange(group);
        return EditGroup(null, FreezeGroup(group));
    }

    public CanvasChangeResult SelectGroup(Guid id)
    {
        EnsureEditable();
        _ = GetGroup(id);
        if (snapshot.SelectedGroupId == id) return NoChange();
        return CommitLayout(CanvasChangeKind.SelectionChanged, GroupSelection(snapshot, id));
    }

    public CanvasChangeResult UpdateGroup(SavedVisualGroup group)
    {
        EnsureEditable();
        var original = GetGroup(group.Id);
        if (group.X != original.X || group.Y != original.Y)
            throw new ArgumentException("Use MoveGroup to translate the frame and its members.");
        ValidateGroupChange(group);
        var liveNodes = snapshot.NodePositions.Keys.ToHashSet();
        var liveNotes = snapshot.Notes.Select(note => note.Id).ToHashSet();
        var updated = FreezeGroup(group with
        {
            NodeIds = group.NodeIds.Concat(original.NodeIds.Where(id => !liveNodes.Contains(id))).ToArray(),
            NoteIds = group.NoteIds.Concat(original.NoteIds.Where(id => !liveNotes.Contains(id))).ToArray()
        });
        ValidateDefinitions(updated);
        if (original == (updated with { NodeIds = original.NodeIds, NoteIds = original.NoteIds }) &&
            original.NodeIds.SequenceEqual(updated.NodeIds) && original.NoteIds.SequenceEqual(updated.NoteIds))
            return NoChange();
        return EditGroup(original, updated);
    }

    public CanvasChangeResult DeleteGroup(Guid id)
    {
        EnsureEditable();
        return EditGroup(GetGroup(id), null);
    }

    public CanvasChangeResult MoveGroup(Guid id, CanvasPoint delta)
    {
        EnsureEditable();
        delta.RequireFinite(nameof(delta));
        var original = GetGroup(id);
        if (delta == CanvasPoint.Zero) return NoChange();
        var moved = original with { X = original.X + delta.X, Y = original.Y + delta.Y };
        ValidateDefinitions(moved);
        var beforeNodes = snapshot.NodePositions.Where(pair => original.NodeIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var afterNodes = beforeNodes.ToDictionary(pair => pair.Key, pair => pair.Value.Add(delta).RequireFinite(nameof(delta)));
        var beforeNotes = snapshot.Notes.Where(note => original.NoteIds.Contains(note.Id))
            .ToDictionary(note => note.Id, note => new CanvasPoint(note.X, note.Y));
        var afterNotes = beforeNotes.ToDictionary(pair => pair.Key, pair => pair.Value.Add(delta));
        var movedNotes = snapshot.Notes.Select(note => afterNotes.TryGetValue(note.Id, out var point)
            ? note with { X = point.X, Y = point.Y } : note).ToArray();
        SavedNote.Validate(movedNotes);
        return CommitAnnotation(new AnnotationEdit(CanvasChangeKind.GroupsChanged, forward =>
        {
            groupDefinitions[id] = forward ? moved : original;
            var positions = new Dictionary<Guid, CanvasPoint>(snapshot.NodePositions);
            foreach (var (nodeId, point) in forward ? afterNodes : beforeNodes)
            {
                if (positions.ContainsKey(nodeId)) positions[nodeId] = point;
                else if (removedPositions.ContainsKey(nodeId)) removedPositions[nodeId] = point;
            }
            var notePositions = forward ? afterNotes : beforeNotes;
            var notes = snapshot.Notes.Select(note => notePositions.TryGetValue(note.Id, out var point)
                ? note with { X = point.X, Y = point.Y } : note).ToArray();
            return GroupSelection(snapshot with { NodePositions = Freeze(positions), Notes = Array.AsReadOnly(notes) }, id);
        }));
    }

    private SavedVisualGroup GetGroup(Guid id) => groupDefinitions.TryGetValue(id, out var group) ? group :
        throw new KeyNotFoundException($"Visual group {id} does not exist in this canvas.");

    private static SavedVisualGroup FreezeGroup(SavedVisualGroup group) => group with
    {
        NodeIds = Array.AsReadOnly(group.NodeIds.ToArray()),
        NoteIds = Array.AsReadOnly(group.NoteIds.ToArray())
    };

    private void ValidateGroupChange(SavedVisualGroup group)
    {
        SavedVisualGroup.Validate([group], snapshot.NodePositions.Keys, snapshot.Notes.Select(note => note.Id));
        ValidateDefinitions(group);
    }

    private void ValidateDefinitions(SavedVisualGroup candidate)
    {
        var definitions = groupDefinitions.Values.Where(group => group.Id != candidate.Id).Append(candidate).ToArray();
        var nodes = snapshot.NodePositions.Keys.Concat(groupDefinitions.Values.SelectMany(group => group.NodeIds));
        var notes = snapshot.Notes.Select(note => note.Id).Concat(groupDefinitions.Values.SelectMany(group => group.NoteIds));
        SavedVisualGroup.Validate(definitions, nodes, notes);
    }

    private CanvasChangeResult EditGroup(SavedVisualGroup? before, SavedVisualGroup? after)
    {
        var id = (after ?? before)!.Id;
        return CommitAnnotation(new AnnotationEdit(CanvasChangeKind.GroupsChanged, forward =>
        {
            var current = forward ? after : before;
            if (current is null) groupDefinitions.Remove(id);
            else groupDefinitions[id] = current;
            return GroupSelection(snapshot, current?.Id);
        }));
    }

    private static CanvasStateSnapshot GroupSelection(CanvasStateSnapshot state, Guid? id) => state with
    {
        SelectedGroupId = id,
        SelectedNoteId = id is null ? state.SelectedNoteId : null,
        SelectedNodeIds = id is null ? state.SelectedNodeIds : [],
        SelectedConnectionIds = id is null ? state.SelectedConnectionIds : []
    };

    private CanvasStateSnapshot ProjectGroups(CanvasStateSnapshot state)
    {
        var noteIds = state.Notes.Select(note => note.Id).ToHashSet();
        var groups = groupDefinitions.Values.Select(group =>
        {
            if (group.NodeIds.All(state.NodePositions.ContainsKey) && group.NoteIds.All(noteIds.Contains)) return group;
            return group with
            {
                NodeIds = Array.AsReadOnly(group.NodeIds.Where(state.NodePositions.ContainsKey).ToArray()),
                NoteIds = Array.AsReadOnly(group.NoteIds.Where(noteIds.Contains).ToArray())
            };
        }).ToArray();
        return state with { VisualGroups = Array.AsReadOnly(groups),
            SelectedGroupId = state.SelectedGroupId is Guid id && groupDefinitions.ContainsKey(id) ? id : null };
    }
}
