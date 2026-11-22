using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Application.Persistence;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public partial class GraphCanvas
{
    private SavedVisualGroup? groupPreview;
    private SavedVisualGroup? groupStart;
    private bool resizingGroup;
    private SavedVisualGroup? editingGroup;

    public async Task AddGroupAsync()
    {
        if (HasAnnotationDraft || AnnotationActionsDisabled) return;
        try
        {
            var context = await MeasurePlacementAsync();
            if (context is not { } measured || HasAnnotationDraft || AnnotationActionsDisabled) return;
            var members = Snapshot.SelectedNodeIds.Where(measured.NodeBounds.ContainsKey)
                .Select(id => measured.NodeBounds[id])
                .Concat(Snapshot.Notes.Where(note => note.Id == Snapshot.SelectedNoteId).Select(CanvasStateService.NoteBounds)).ToArray();
            const double padding = 24;
            CanvasBounds bounds;
            if (members.Length == 0)
            {
                var left = measured.Visible.Left + Math.Max(0, (measured.Visible.Width - 440) / 2);
                var top = measured.Visible.Top + Math.Max(0, (measured.Visible.Height - 260) / 2);
                bounds = new CanvasBounds(left, top, left + 440, top + 260);
            }
            else bounds = new CanvasBounds(members.Min(item => item.Left) - padding,
                members.Min(item => item.Top) - padding - SavedNote.CollapsedHeight,
                members.Max(item => item.Right) + padding, members.Max(item => item.Bottom) + padding);
            State.AddGroup(bounds);
        }
        catch (Exception exception) { await OnAnnotationError.InvokeAsync(exception.Message); }
    }

    private async Task BeginGroupAsync(SavedVisualGroup group, PointerEventArgs args, bool resize)
    {
        if (RejectAdditionalWirePointer(args) || HasAnnotationDraft || AnnotationActionsDisabled || args.Button != 0) return;
        State.SelectGroup(group.Id);
        pendingSource = null;
        groupStart = groupPreview = group;
        resizingGroup = resize;
        await BeginGesture(args, null, wire: false);
    }

    private void PreviewGroupDelta(CanvasPoint world)
    {
        if (groupPreview is not { } group) return;
        groupPreview = resizingGroup
            ? group with { Width = Math.Clamp(group.Width + world.X, SavedVisualGroup.MinWidth, SavedVisualGroup.MaxSize),
                Height = Math.Clamp(group.Height + world.Y, SavedVisualGroup.MinHeight, SavedVisualGroup.MaxSize) }
            : group with { X = Math.Clamp(group.X + world.X, -SavedNote.MaxCoordinate, SavedNote.MaxCoordinate),
                Y = Math.Clamp(group.Y + world.Y, -SavedNote.MaxCoordinate, SavedNote.MaxCoordinate) };
    }

    private CanvasPoint GroupPreviewDelta => groupStart is { } start && groupPreview is { } preview && !resizingGroup
        ? new CanvasPoint(preview.X - start.X, preview.Y - start.Y) : CanvasPoint.Zero;

    private CanvasPoint DisplayNodePosition(Guid id) => Snapshot.NodePositions[id].Add(
        groupStart?.NodeIds.Contains(id) == true ? GroupPreviewDelta : CanvasPoint.Zero);

    private SavedNote DisplayNote(SavedNote note)
    {
        if (notePreview?.Id == note.Id) return notePreview;
        if (groupStart?.NoteIds.Contains(note.Id) != true) return note;
        var delta = GroupPreviewDelta;
        return note with { X = note.X + delta.X, Y = note.Y + delta.Y };
    }

    private async Task CommitGroupGestureAsync(SavedVisualGroup original, SavedVisualGroup preview, bool resize)
    {
        await NoteAction(() => resize ? State.UpdateGroup(preview) :
            State.MoveGroup(original.Id, new CanvasPoint(preview.X - original.X, preview.Y - original.Y)));
    }

    private async Task BeginGroupEditAsync(SavedVisualGroup group)
    {
        if (HasAnnotationDraft || AnnotationActionsDisabled) return;
        State.SelectGroup(group.Id);
        pendingSource = null;
        editingGroup = group;
        await SetAnnotationDraftAsync(true);
    }

    private async Task FinishGroupEditAsync(SavedVisualGroup? group)
    {
        try { if (group is not null) State.UpdateGroup(group); }
        catch (Exception exception) { await OnAnnotationError.InvokeAsync(exception.Message); return; }
        editingGroup = null;
        await SetAnnotationDraftAsync(false);
        await viewportElement.FocusAsync(preventScroll: true);
    }

    private IReadOnlyList<GroupMemberOption> GroupMemberOptions(Guid id)
    {
        var otherGroups = Snapshot.VisualGroups.Where(group => group.Id != id).ToArray();
        var otherNodes = otherGroups.SelectMany(group => group.NodeIds).ToHashSet();
        var otherNotes = otherGroups.SelectMany(group => group.NoteIds).ToHashSet();
        return Snapshot.Graph.Nodes.Where(node => !otherNodes.Contains(node.Id))
            .Select(node => new GroupMemberOption(node.Id, false, Snapshot.DisplayName(node.Id)))
            .Concat(Snapshot.Notes.Where(note => !otherNotes.Contains(note.Id))
                .Select(note => new GroupMemberOption(note.Id, true, note.Title))).ToArray();
    }
}

public sealed record GroupMemberOption(Guid Id, bool IsNote, string Name);
