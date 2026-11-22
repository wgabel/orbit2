using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Orbit.UI.Components.Canvas;

public partial class GraphWorkspace
{
    [Inject] private IJSRuntime MobileJavaScript { get; set; } = default!;
    private string? mobilePanel;
    private bool desktopLibraryOpen;
    private bool NodeLibraryOpen => desktopLibraryOpen || mobilePanel == "nodes";
    private void ToggleNodeLibrary() => desktopLibraryOpen = !desktopLibraryOpen;
    private void CloseNodeLibrary() { desktopLibraryOpen = false; if (mobilePanel == "nodes") mobilePanel = null; }
    private string? ActiveMobilePanel => outlineOpen ? "outline" : mobilePanel;
    private ElementReference workspaceElement;
    private PatchManager? patchManager;
    private PerformanceMonitor? performanceMonitor;
    private IJSObjectReference? mobilePanelModule;
    private IJSObjectReference? mobilePanelController;
    private DotNetObjectReference<GraphWorkspace>? mobilePanelReference;
    private bool mobilePanelInitStarted;

    private string MobileSelectionSummary => Canvas.Snapshot.SelectedNoteId is not null ? "Note selected"
        : Canvas.Snapshot.SelectedGroupId is not null ? "Visual group selected"
        : Canvas.Snapshot.SelectedNodeIds.Count + Canvas.Snapshot.SelectedConnectionIds.Count == 0 ? "No selection"
        : $"{Canvas.Snapshot.SelectedNodeIds.Count} nodes · {Canvas.Snapshot.SelectedConnectionIds.Count} connections selected";

    private bool CanLeaveMobilePanel()
    {
        if (overviewDraft || switchingWorkspace) return false;
        if (mobilePanel != "more") return true;
        if (patchManager?.TryCloseMobilePanel() == false) return false;
        performanceMonitor?.CloseMobilePanel();
        return true;
    }

    private void ToggleMobilePanel(string panel)
    {
        if (!CanLeaveMobilePanel()) return;
        outlineOpen = false;
        desktopLibraryOpen = false;
        mobilePanel = mobilePanel == panel || (panel == "edit" && mobilePanel == "selection") ? null : panel;
    }

    private void ToggleMobileOutline()
    {
        if (!CanLeaveMobilePanel()) return;
        mobilePanel = null;
        desktopLibraryOpen = false;
        outlineOpen = !outlineOpen;
    }

    private void CloseOutline() => outlineOpen = false;

    private Task DismissMobilePanelAsync() => DismissMobilePanelCoreAsync();

    [JSInvokable]
    public async Task<bool> DismissMobilePanelCoreAsync()
    {
        if (disposed || !CanLeaveMobilePanel()) return false;
        outlineOpen = false;
        mobilePanel = null;
        await InvokeAsync(StateHasChanged);
        return true;
    }

    private void CompleteMobileCanvasTool()
    {
        if (mobilePanel is "selection" or "view") mobilePanel = null;
    }

    private void ToggleScopeFromToolbar()
    {
        ToggleScope();
        CompleteMobileCanvasTool();
    }

    private async Task DescribeFromToolbarAsync()
    {
        if (mobilePanel == "edit") mobilePanel = null;
        if (canvasView is not null) await canvasView.EditSelectedDescriptionAsync();
    }

    private async Task GroupFromToolbarAsync()
    {
        if (mobilePanel == "edit") mobilePanel = null;
        if (canvasView is not null) await canvasView.AddGroupAsync();
    }

    private async Task<bool> AddNodeFromLibraryAsync(string type)
    {
        var added = await AddNodeAsync(type);
        if (added) CloseNodeLibrary();
        await InvokeAsync(StateHasChanged);
        return added;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!initialized || disposed || mobilePanelInitStarted) return;
        mobilePanelInitStarted = true;
        // DOM-only focus/navigation adapter; graph and panel state remain in C#.
        try
        {
            mobilePanelModule = await MobileJavaScript.InvokeAsync<IJSObjectReference>("import", "./js/workspacePanels.js");
            if (disposed) { await mobilePanelModule.DisposeAsync(); return; }
            mobilePanelReference = DotNetObjectReference.Create(this);
            mobilePanelController = await mobilePanelModule.InvokeAsync<IJSObjectReference>("mountWorkspacePanels", workspaceElement, mobilePanelReference);
            if (disposed) await DisposeMobilePanelsAsync();
        }
        catch (JSException) { } // Explicit close/toggle controls remain usable.
    }

    private async ValueTask DisposeMobilePanelsAsync()
    {
        if (mobilePanelController is not null)
        {
            try { await mobilePanelController.InvokeVoidAsync("dispose"); }
            catch (JSException) { }
            await mobilePanelController.DisposeAsync();
            mobilePanelController = null;
        }
        mobilePanelReference?.Dispose();
        mobilePanelReference = null;
        if (mobilePanelModule is not null)
        {
            await mobilePanelModule.DisposeAsync();
            mobilePanelModule = null;
        }
    }
}
