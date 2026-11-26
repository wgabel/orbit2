using Orbit.Application.Graphs.Commands;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public partial class GraphWorkspace
{
    private CompositeMacroDraft? macroDraft;

    private void OpenMacroEditor(Guid graphId, Guid nodeId)
    {
        if (switchingWorkspace || !CheckAnnotationDraft() || !CanLeaveMobilePanel() ||
            graphId != Canvas.Snapshot.GraphId ||
            Canvas.Snapshot.Graph.Nodes.SingleOrDefault(node => node.Id == nodeId)?.Composite is null) return;
        session.CompleteHistoryGesture();
        CloseRackEditor();
        ClosePresetManager();
        outlineOpen = false;
        mobilePanel = null;
        Error = null;
        macroDraft = new CompositeMacroDraft(session.Snapshot, graphId, nodeId);
    }

    private void CloseMacroEditor() => macroDraft = null;

    private string? ApplyMacroDraft(CompositeMacroDraft draft, ReplaceCompositeMacrosCommand command)
    {
        if (switchingWorkspace || annotationDraft || !ReferenceEquals(macroDraft, draft) ||
            command.GraphId != Canvas.Snapshot.GraphId || command.NodeId != draft.NodeId)
            return "This macro editor is no longer active.";
        if (!Execute(command))
        {
            var error = Error;
            Error = null;
            return error ?? "Unable to apply macro configuration.";
        }
        CloseMacroEditor();
        return null;
    }
}
