using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Application.Presets;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public partial class GraphCanvas
{
    [Inject] private NodePresetService ContextPresets { get; set; } = default!;
    [Parameter] public EventCallback<bool> OnContextAddNode { get; set; }
    [Parameter] public EventCallback<bool> OnContextSelection { get; set; }
    [Parameter] public EventCallback OnContextDelete { get; set; }
    [Parameter] public EventCallback<Guid> OnContextDuplicate { get; set; }
    [Parameter] public EventCallback OnContextUndo { get; set; }
    [Parameter] public EventCallback OnContextRedo { get; set; }
    [Parameter] public bool ContextCanUndo { get; set; }
    [Parameter] public bool ContextCanRedo { get; set; }

    public sealed record ContextRequest(string Kind, Guid? Id, double X, double Y, bool Touch, bool Mobile);
    private sealed record ContextAction(string Label, Func<Task> Run, bool Destructive = false);
    private sealed record ContextMenu(ContextRequest Request, string Title, CanvasStateSnapshot Snapshot, IReadOnlyList<ContextAction> Actions);
    private ContextMenu? contextMenu;
    private ContextMenu? positionedContext;
    private IJSObjectReference? contextModule;
    private IJSObjectReference? contextController;
    private DotNetObjectReference<GraphCanvas>? contextReceiver;
    private bool contextInitStarted;

    private async Task RenderContextAsync()
    {
        if (contextModule is null && !contextInitStarted)
        {
            contextInitStarted = true;
            contextModule = await JavaScript.InvokeAsync<IJSObjectReference>("import", BrowserBuild.ModulePath("canvasContext.js"));
            if (disposed) { await contextModule.DisposeAsync(); return; }
            contextReceiver = DotNetObjectReference.Create(this);
            contextController = await contextModule.InvokeAsync<IJSObjectReference>("mountContextInput", viewportElement, contextReceiver);
        }
        if (contextModule is not null && contextMenu is { } menu && !ReferenceEquals(menu, positionedContext))
        {
            positionedContext = menu;
            await contextModule.InvokeVoidAsync("positionContextMenu", viewportElement, menu.Request.X, menu.Request.Y);
        }
    }

    [JSInvokable]
    public async Task OpenContextAsync(ContextRequest request)
    {
        if (disposed || AnnotationControlsLocked || contextMenu is not null || !double.IsFinite(request.X) || !double.IsFinite(request.Y)) return;
        fitGeneration++;
        fitTaps.Reset();
        EndGesture();
        pendingSource = null;
        var actions = new List<ContextAction>();
        void Add(string label, Func<Task> run, bool destructive = false) => actions.Add(new(label, run, destructive));
        void Change(string label, Action run) => Add(label, () => { run(); return Task.CompletedTask; });
        string title;
        async Task SelectionActions()
        {
            if (OnContextSelection.HasDelegate) await OnContextSelection.InvokeAsync(request.Mobile);
        }
        void AddSelectionActions()
        {
            if (CanGroup) Add("Create composite…", SelectionActions);
            Add("Create visual group", AddGroupAsync);
            if (OnContextDelete.HasDelegate && (Snapshot.SelectedConnectionIds.Count > 0 || Snapshot.Graph.Nodes.Any(n =>
                Snapshot.SelectedNodeIds.Contains(n.Id) && n.TypeKey is not (InputRack.NodeType or OutputRack.NodeType))))
                Add("Delete selection", () => OnContextDelete.InvokeAsync(), true);
            Change("Clear selection", () => State.ClearSelection());
        }
        switch (request.Kind)
        {
            case "canvas":
                title = "Canvas";
                State.ClearSelection();
                if (OnContextAddNode.HasDelegate) Add("Add node…", () => OnContextAddNode.InvokeAsync(request.Mobile));
                Add("Add note", async () => { if (!await AddNoteAsync()) await OnAnnotationError.InvokeAsync("The canvas view changed. Try adding the note again."); });
                Add("Create visual group", AddGroupAsync);
                Add(marqueeMode ? "Return to pan" : "Marquee select", ToggleMarqueeMode);
                if (EligibleNodes.Any()) Change("Select all module nodes", SelectAllModuleNodes);
                if (ContextCanUndo) Add("Undo", () => OnContextUndo.InvokeAsync());
                if (ContextCanRedo) Add("Redo", () => OnContextRedo.InvokeAsync());
                if (Snapshot.CanUndoAnnotations) Change("Undo annotation", () => State.UndoAnnotation());
                if (Snapshot.CanRedoAnnotations) Change("Redo annotation", () => State.RedoAnnotation());
                break;
            case "node" when Snapshot.Graph.Nodes.FirstOrDefault(n => n.Id == request.Id) is { } node:
                if (!Snapshot.SelectedNodeIds.Contains(node.Id)) State.SelectNode(node.Id);
                if (Snapshot.SelectedNodeIds.Count > 1 || Snapshot.SelectedConnectionIds.Count > 0)
                {
                    title = "Selection";
                    AddSelectionActions();
                    break;
                }
                title = Snapshot.DisplayName(node.Id);
                if (node.TypeKey is not (InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType) && OnContextDuplicate.HasDelegate)
                    Add("Duplicate node", () => OnContextDuplicate.InvokeAsync(node.Id));
                if (node.Composite is not null && OnOpenComposite.HasDelegate) Add("Open composite", () => OnOpenComposite.InvokeAsync(node.Id));
                if (SelectedComposite is not null && OnUngroupComposite.HasDelegate) Add("Ungroup composite…", SelectionActions);
                if (node.TypeKey is (InputRack.NodeType or OutputRack.NodeType) && OnEditRack.HasDelegate) Add("Edit ports", () => OnEditRack.InvokeAsync(node.Id));
                if (node.SupportsMute && OnAudition.HasDelegate) Add(node.IsMuted ? "Unmute" : "Mute", () => OnAudition.InvokeAsync(new(node.Id, NodeAuditionKind.Mute, !node.IsMuted)));
                if (node.BypassInputKey is not null && OnAudition.HasDelegate) Add(node.IsBypassed ? "Unbypass" : "Bypass", () => OnAudition.InvokeAsync(new(node.Id, NodeAuditionKind.Bypass, !node.IsBypassed)));
                if (NodeVisualizationCapabilities.EligiblePorts(node).Any())
                {
                    var settings = Snapshot.VisualizationFor(node.Id);
                    Change(settings.Visible ? "Hide node plot" : "Show node plot", () => State.SetVisualization(node.Id, settings with { Visible = !settings.Visible }));
                    Change(Snapshot.ExpandedVisualizationNodeId == node.Id ? "Close scope" : "Open scope", () => State.SetExpandedVisualization(Snapshot.ExpandedVisualizationNodeId == node.Id ? null : node.Id));
                }
                if (OnManagePresets.HasDelegate && ContextPresets.Supports(node.TypeKey)) Add("Manage presets…", () => OnManagePresets.InvokeAsync(node.Id));
                Add("Describe / rename…", EditSelectedDescriptionAsync);
                Add("Focus node", () => FocusNodeAsync(node.Id));
                AddSelectionActions();
                break;
            case "connection" when Snapshot.Graph.Connections.FirstOrDefault(c => c.Id == request.Id) is { } edge:
                title = "Connection";
                State.SelectConnection(edge.Id);
                var input = Snapshot.Graph.GetNode(edge.Destination.NodeId).Inputs.Single(port => port.Id == edge.Destination.PortId);
                Add(input.SignalType.IsEvent() ? "Inspect connection" : "Edit amount", () => Task.CompletedTask); // Closing reveals the existing selected-connection editor.
                Add("Describe…", EditSelectedDescriptionAsync);
                Add("Focus source", () => FocusNodeAsync(edge.Source.NodeId));
                Add("Focus destination", () => FocusNodeAsync(edge.Destination.NodeId));
                if (OnContextDelete.HasDelegate) Add("Delete connection", () => OnContextDelete.InvokeAsync(), true);
                break;
            case "note" when Snapshot.Notes.FirstOrDefault(n => n.Id == request.Id) is { } note:
                title = note.Title;
                State.SelectNote(note.Id);
                Add("Edit note…", () => BeginNoteEditAsync(note.Id));
                Add(note.Collapsed ? "Expand note" : "Collapse note", () => UpdateNote(note with { Collapsed = !note.Collapsed }));
                Add("Duplicate note", () => NoteAction(() => State.DuplicateNote(note.Id)));
                Add("Create visual group", AddGroupAsync);
                Add("Delete note", () => NoteAction(() => State.DeleteNote(note.Id)), true);
                break;
            case "group" when Snapshot.VisualGroups.FirstOrDefault(g => g.Id == request.Id) is { } group:
                title = group.Title;
                State.SelectGroup(group.Id);
                Add("Edit visual group…", () => BeginGroupEditAsync(group));
                Add("Delete visual group", () => NoteAction(() => State.DeleteGroup(group.Id)), true);
                break;
            default: return;
        }
        contextMenu = new(request, title, Snapshot, actions);
        await InvokeAsync(StateHasChanged);
    }

    private async Task CloseContextAsync()
    {
        contextMenu = positionedContext = null;
        await viewportElement.FocusAsync(preventScroll: true);
    }

    [JSInvokable]
    public async Task DismissContextAsync()
    {
        if (disposed || contextMenu is null) return;
        contextMenu = positionedContext = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task RunContextAsync(ContextAction action)
    {
        var menu = contextMenu;
        await CloseContextAsync();
        if (menu is null || AnnotationControlsLocked || !ReferenceEquals(menu.Snapshot, Snapshot) || !menu.Actions.Contains(action)) return;
        try { await action.Run(); }
        catch (Exception exception) { await OnAnnotationError.InvokeAsync(exception.Message); }
    }

    private Task ContextKeyDown(KeyboardEventArgs args) => args.Key == "Escape" ? CloseContextAsync() : Task.CompletedTask;

    private async ValueTask DisposeContextAsync()
    {
        if (contextController is not null)
        {
            await contextController.InvokeVoidAsync("dispose");
            await contextController.DisposeAsync();
        }
        contextReceiver?.Dispose();
        if (contextModule is not null) await contextModule.DisposeAsync();
    }
}
