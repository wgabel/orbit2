using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public partial class GraphWorkspace
{
    private bool outlineOpen;
    private OutlineFocusRequest? outlineFocus;

    private void NavigateOutline(OutlineNavigationRequest request)
    {
        if (switchingWorkspace || !CheckAnnotationDraft()) return;
        // Resolve again against the committed index, not the row's stale data.
        var entry = Navigation.GetOutline(overview.Title).Find(request.Target);
        if (entry is null || (request.OpenChild && entry.ChildGraphId is null))
        {
            Error = "The outline target is no longer available.";
            return;
        }
        var target = request.OpenChild
            ? new OutlineTarget(entry.ChildGraphId!.Value, OutlineKind.Canvas, entry.ChildGraphId.Value)
            : request.Target;
        Error = null;
        session.CompleteHistoryGesture();
        CloseRackEditor();
        ClosePresetManager();
        CloseMacroEditor();
        if (SelectedConnection is not null) Canvas.ClearSelection();
        if (!Navigation.JumpToGraph(target.GraphId))
        {
            Error = "The outline canvas is no longer available.";
            return;
        }
        switch (target.Kind)
        {
            case OutlineKind.Note: Canvas.SelectNote(target.Id); break;
            case OutlineKind.Group: Canvas.SelectGroup(target.Id); break;
            case OutlineKind.Node or OutlineKind.Composite: Canvas.SelectNode(target.Id); break;
        }
        outlineOpen = false;
        outlineFocus = new OutlineFocusRequest(target, Guid.NewGuid());
    }
}
