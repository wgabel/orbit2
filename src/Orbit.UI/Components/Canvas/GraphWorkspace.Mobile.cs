namespace Orbit.UI.Components.Canvas;

public partial class GraphWorkspace
{
    private string? mobilePanel;

    private void ToggleMobilePanel(string panel)
    {
        if (overviewDraft) return;
        outlineOpen = false;
        mobilePanel = mobilePanel == panel ? null : panel;
    }

    private void ToggleMobileOutline()
    {
        if (overviewDraft) return;
        mobilePanel = null;
        outlineOpen = !outlineOpen;
    }

    private async Task AddNodeFromToolbarAsync()
    {
        await AddNodeAsync();
        if (Error is null && mobilePanel == "nodes") mobilePanel = null;
    }
}
