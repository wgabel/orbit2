using Microsoft.JSInterop;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;

namespace Orbit.UI.Components.Canvas;

/// <summary>
/// Canvas-owned presentation state. DSP snapshots and undo history do not contain
/// expansion state; only explicit pins are remembered in browser preferences.
/// </summary>
public partial class GraphCanvas
{
    private readonly HashSet<Guid> pinnedNodes = [];
    private Guid? expandedNodeId;
    private Guid? pendingNodeTap;
    private bool nodeTapMoved;
    private Orbit.Presentation.Canvas.CanvasPoint nodeTapOrigin;
    private IJSObjectReference? presentationModule;
    private string? pinsLoadedFor;

    private static bool SupportsExpansion(Orbit.Application.Graphs.Snapshots.GraphNodeSnapshot node) =>
        node.Composite is null && node.TypeKey is not (InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType);

    private bool IsNodeExpanded(Guid id) => expandedNodeId == id || pinnedNodes.Contains(id);
    private bool IsNodePinned(Guid id) => pinnedNodes.Contains(id);

    private void TrackNodeTap(Guid id, Microsoft.AspNetCore.Components.Web.PointerEventArgs args)
    {
        pendingNodeTap = id;
        nodeTapMoved = false;
        nodeTapOrigin = new(args.ClientX, args.ClientY);
    }

    private void TrackTapMovement(Orbit.Presentation.Canvas.CanvasPoint current)
    {
        if (pendingNodeTap is not null && current.Subtract(nodeTapOrigin) is { } delta &&
            delta.X * delta.X + delta.Y * delta.Y > 64)
            nodeTapMoved = true;
    }

    private void FinishNodeTap()
    {
        var tappedNode = pendingNodeTap;
        var wasDrag = nodeTapMoved;
        pendingNodeTap = null;
        nodeTapMoved = false;
        if (wasDrag || tappedNode is not Guid id ||
            !Snapshot.Graph.Nodes.Any(node => node.Id == id && SupportsExpansion(node))) return;

        expandedNodeId = id;
        visualizationGeometryGeneration++;
    }

    private Task ToggleNodeExpandedAsync(Guid id)
    {
        if (!Snapshot.Graph.Nodes.Any(node => node.Id == id && SupportsExpansion(node))) return Task.CompletedTask;
        if (IsNodeExpanded(id))
        {
            pinnedNodes.Remove(id);
            if (expandedNodeId == id) expandedNodeId = null;
        }
        else expandedNodeId = id;
        visualizationGeometryGeneration++;
        return SavePinsAsync();
    }

    private Task ToggleNodePinnedAsync(Guid id)
    {
        if (!Snapshot.Graph.Nodes.Any(node => node.Id == id && SupportsExpansion(node))) return Task.CompletedTask;
        if (!pinnedNodes.Add(id))
            pinnedNodes.Remove(id);
        expandedNodeId = id;
        visualizationGeometryGeneration++;
        return SavePinsAsync();
    }

    private async Task LoadPinsAsync()
    {
        var key = $"{PatchId:N}.{Snapshot.GraphId:N}";
        if (pinsLoadedFor == key) return;
        presentationModule ??= await JavaScript.InvokeAsync<IJSObjectReference>(
            "import", BrowserBuild.ModulePath("nodePresentation.js"));
        var saved = await presentationModule.InvokeAsync<string[]>("readPinned", PatchId.ToString("N"), Snapshot.GraphId.ToString("N"));
        if (disposed || key != $"{PatchId:N}.{Snapshot.GraphId:N}") return;
        pinnedNodes.Clear();
        var eligible = Snapshot.Graph.Nodes.Where(SupportsExpansion).Select(node => node.Id).ToHashSet();
        foreach (var raw in saved)
            if (Guid.TryParse(raw, out var id) && eligible.Contains(id))
                pinnedNodes.Add(id);
        pinsLoadedFor = key;
        if (pinnedNodes.Count > 0) await InvokeAsync(StateHasChanged);
    }

    private async Task SavePinsAsync()
    {
        if (pinsLoadedFor != $"{PatchId:N}.{Snapshot.GraphId:N}") return;
        if (presentationModule is not null)
            await presentationModule.InvokeVoidAsync("writePinned", PatchId.ToString("N"),
                Snapshot.GraphId.ToString("N"), pinnedNodes.Select(id => id.ToString("N")).ToArray());
    }

    private void ResetExpansionForCanvas()
    {
        expandedNodeId = null;
        pendingNodeTap = null;
        nodeTapMoved = false;
        pinsLoadedFor = null;
        pinnedNodes.Clear();
    }
}
