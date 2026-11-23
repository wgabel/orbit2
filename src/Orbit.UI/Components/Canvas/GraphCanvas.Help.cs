using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Orbit.Application.Graphs.Snapshots;

namespace Orbit.UI.Components.Canvas;

/// <summary>Read-only help overlays; no graph commands, history changes or DSP work.</summary>
public partial class GraphCanvas
{
    private Guid? helpNodeId;
    private Guid? positionedHelpId;
    private ElementReference helpCloseButton;
    private IJSObjectReference? portHintsController;
    private IJSObjectReference? helpFocusController;

    private GraphNodeSnapshot? HelpNode => helpNodeId is Guid id
        ? Snapshot.Graph.Nodes.FirstOrDefault(node => node.Id == id) : null;

    private void ShowNodeHelp(Guid nodeId)
    {
        if (disposed || contextMenu is not null || !Snapshot.Graph.Nodes.Any(node => node.Id == nodeId))
            return;
        pendingSource = null;
        helpNodeId = nodeId;
        positionedHelpId = null;
    }

    private async Task CloseNodeHelpAsync()
    {
        var id = helpNodeId;
        helpNodeId = null;
        positionedHelpId = null;
        if (id is Guid nodeId && module is not null && !disposed)
            await module.InvokeVoidAsync("focusNodeHelpTrigger", viewportElement, nodeId.ToString("D"));
    }

    private Task HandleNodeHelpKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CloseNodeHelpAsync() : Task.CompletedTask;

    private async Task RenderNodeHelpAsync()
    {
        if (module is null || disposed) return;

        // One passive delegated hover/focus listener per active canvas, not one per port.
        portHintsController ??= await module.InvokeAsync<IJSObjectReference>("mountPortHints", viewportElement);

        if (HelpNode is not { } node)
        {
            positionedHelpId = null;
            if (helpFocusController is not null)
            {
                await helpFocusController.InvokeVoidAsync("dispose");
                await helpFocusController.DisposeAsync();
                helpFocusController = null;
            }
            return;
        }

        if (positionedHelpId == node.Id) return;
        if (helpFocusController is not null)
        {
            await helpFocusController.InvokeVoidAsync("dispose");
            await helpFocusController.DisposeAsync();
        }
        positionedHelpId = node.Id;
        await module.InvokeVoidAsync("positionNodeHelp", viewportElement, node.Id.ToString("D"));
        helpFocusController = await module.InvokeAsync<IJSObjectReference>("trapNodeHelpFocus", viewportElement);
        await helpCloseButton.FocusAsync(preventScroll: true);
    }

    private async Task DisposeNodeHelpAsync()
    {
        if (helpFocusController is not null)
        {
            await helpFocusController.InvokeVoidAsync("dispose");
            await helpFocusController.DisposeAsync();
            helpFocusController = null;
        }
        if (portHintsController is not null)
        {
            await portHintsController.InvokeVoidAsync("dispose");
            await portHintsController.DisposeAsync();
            portHintsController = null;
        }
    }
}
