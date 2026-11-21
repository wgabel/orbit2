using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Presets;

namespace Orbit.UI.Components.Canvas;

/// <summary>Shared management panel for every eligible node, separate from graph edits.</summary>
public partial class NodePresetManager
{
    [Inject] private NodePresetLibraryService Library { get; set; } = default!;
    [Inject] private NodePresetService Catalog { get; set; } = default!;
    [Parameter, EditorRequired] public GraphNodeSnapshot Node { get; set; } = default!;
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnLibraryChanged { get; set; }

    private string Name { get; set; } = "";
    private string Description { get; set; } = "";
    private string SelectedId { get; set; } = "";
    private bool Busy { get; set; }
    private string? Message { get; set; }
    private string? Error { get; set; }
    private bool CanManage => !Busy && Library.IsLoaded && Catalog.GetCustomPresets(Node.TypeKey).Any(preset => preset.Id == SelectedId);

    protected override Task OnInitializedAsync() => RefreshAsync();

    private Task RefreshAsync() => RunAsync(async () =>
    {
        await Library.LoadAsync();
        if (!Catalog.GetCustomPresets(Node.TypeKey).Any(preset => preset.Id == SelectedId)) SelectedId = "";
    });

    private Task SaveAsync() => RunAsync(async () =>
    {
        var saved = await Library.SaveAsync(Node, Name, Description);
        SelectedId = saved.Id;
        Message = $"Saved {saved.Name}.";
    });

    private Task RenameAsync() => RunAsync(async () =>
    {
        var saved = await Library.RenameAsync(SelectedId, Node.TypeKey, Name);
        Message = $"Renamed to {saved.Name}.";
    });

    private Task OverwriteAsync() => RunAsync(async () =>
    {
        var saved = await Library.OverwriteAsync(SelectedId, Node);
        Message = $"Updated {saved.Name}.";
    });

    private Task DeleteAsync() => RunAsync(async () =>
    {
        await Library.DeleteAsync(SelectedId, Node.TypeKey);
        SelectedId = "";
        Message = "Custom preset deleted; current node unchanged.";
    });

    private async Task RunAsync(Func<Task> action)
    {
        if (Busy) return;
        Busy = true;
        Error = Message = null;
        try
        {
            await action();
            await OnLibraryChanged.InvokeAsync();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { Busy = false; }
    }
}
