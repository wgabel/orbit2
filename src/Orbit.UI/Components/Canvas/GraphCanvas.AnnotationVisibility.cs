using Microsoft.AspNetCore.Components;
using Orbit.Application.Persistence;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public partial class GraphCanvas
{
    private bool finderOpen;
    private string finderTarget = "";
    private bool AnnotationControlsLocked => HasAnnotationDraft || AnnotationActionsDisabled;
    private bool HasAnnotations => Snapshot.Notes.Count > 0 || Snapshot.VisualGroups.Count > 0;
    private AnnotationVisibility VisibilityMode
    {
        get => Snapshot.AnnotationVisibility;
        set
        {
            if (AnnotationControlsLocked) return;
            State.SetAnnotationVisibility(value);
            finderOpen = false;
            finderTarget = "";
        }
    }

    private bool FinderTargetExists => Snapshot.Notes.Any(note => finderTarget == $"note:{note.Id}") ||
        Snapshot.VisualGroups.Any(group => finderTarget == $"group:{group.Id}");

    private void ToggleFinder()
    {
        if (AnnotationControlsLocked) return;
        finderOpen = !finderOpen;
        if (!FinderTargetExists)
            finderTarget = Snapshot.Notes.FirstOrDefault() is { } note ? $"note:{note.Id}" :
                Snapshot.VisualGroups.FirstOrDefault() is { } group ? $"group:{group.Id}" : "";
    }

    private async Task SelectAnnotationAsync(Guid id, bool isGroup)
    {
        if (AnnotationControlsLocked) return;
        if (isGroup) State.SelectGroup(id); else State.SelectNote(id);
        pendingSource = null;
        await viewportElement.FocusAsync(preventScroll: true);
    }

    private async Task RevealAnnotationAsync()
    {
        if (AnnotationControlsLocked || !FinderTargetExists) return;
        var selected = finderTarget;
        var state = State;
        await MeasureAsync();
        if (disposed || !ReferenceEquals(state, State) || selected != finderTarget || !FinderTargetExists ||
            AnnotationControlsLocked || measurement is null || measurement.Width <= 0 || measurement.Height <= 0) return;
        var isGroup = selected.StartsWith("group:", StringComparison.Ordinal);
        var id = Guid.Parse(selected[(selected.IndexOf(':') + 1)..]);
        var bounds = isGroup
            ? CanvasStateService.GroupBounds(Snapshot.VisualGroups.Single(group => group.Id == id))
            : CanvasStateService.NoteBounds(Snapshot.Notes.Single(note => note.Id == id));
        finderOpen = false;
        await SelectAnnotationAsync(id, isGroup);
        if (!disposed && ReferenceEquals(State, state) && measurement is not null && !AnnotationControlsLocked)
            State.FitToBounds(bounds, measurement.Width, measurement.Height, 32);
    }

    private async Task FitSignalsAsync()
    {
        if (AnnotationControlsLocked) return;
        var state = State;
        await MeasureAsync();
        if (!disposed && ReferenceEquals(state, State) && !AnnotationControlsLocked)
            FitMeasured(signalOnly: true);
    }
}
