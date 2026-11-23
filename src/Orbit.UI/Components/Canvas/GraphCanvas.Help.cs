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
    private bool portHintsInitStarted;
    private IJSObjectReference? helpFocusController;
    private DotNetObjectReference<GraphCanvas>? helpReceiver;

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

    [JSInvokable]
    public Task DismissNodeHelpFromBackAsync() => InvokeAsync(async () =>
    {
        await CloseNodeHelpAsync();
        StateHasChanged();
    });

    private async Task RenderNodeHelpAsync()
    {
        if (module is null || disposed) return;

        // One passive delegated hover/focus listener per active canvas, not one per port.
        if (!portHintsInitStarted)
        {
            portHintsInitStarted = true;
            try
            {
                var controller = await module.InvokeAsync<IJSObjectReference>("mountPortHints", viewportElement);
                if (disposed)
                {
                    await controller.InvokeVoidAsync("dispose");
                    await controller.DisposeAsync();
                    return;
                }
                portHintsController = controller;
            }
            catch (JSException)
            {
                // A later render may retry; the node ? button remains available.
                portHintsInitStarted = false;
            }
        }

        if (HelpNode is not { } node)
        {
            positionedHelpId = null;
            await ReleaseHelpFocusAsync();
            return;
        }

        if (positionedHelpId == node.Id) return;
        await ReleaseHelpFocusAsync();
        positionedHelpId = node.Id;
        await module.InvokeVoidAsync("positionNodeHelp", viewportElement, node.Id.ToString("D"));
        helpReceiver ??= DotNetObjectReference.Create(this);
        helpFocusController = await module.InvokeAsync<IJSObjectReference>("trapNodeHelpFocus", viewportElement, helpReceiver);
        await helpCloseButton.FocusAsync(preventScroll: true);
    }

    // Clear the shared reference *before* awaiting JS interop. Blazor can
    // enter OnAfterRenderAsync again while another render is still disposing
    // the controller; a second history.back() would leave the app.
    private async Task ReleaseHelpFocusAsync()
    {
        var controller = helpFocusController;
        helpFocusController = null;
        if (controller is null) return;
        await controller.InvokeVoidAsync("dispose");
        await controller.DisposeAsync();
    }

    private async Task DisposeNodeHelpAsync()
    {
        await ReleaseHelpFocusAsync();
        helpReceiver?.Dispose();
        helpReceiver = null;
        if (portHintsController is not null)
        {
            await portHintsController.InvokeVoidAsync("dispose");
            await portHintsController.DisposeAsync();
            portHintsController = null;
        }
    }
}
