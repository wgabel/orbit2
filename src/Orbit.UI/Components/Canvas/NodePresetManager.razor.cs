using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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
    private NodePreset? ImportPreview { get; set; }
    private string ImportCopyName { get; set; } = "";
    private int ImportInputRevision { get; set; }
    private NodePreset? MatchingImportPreset => Catalog.GetCustomPresets(Node.TypeKey)
        .FirstOrDefault(preset => preset.Id == ImportPreview?.Id);
    private bool CanManage => !Busy && Library.IsLoaded && Catalog.GetCustomPresets(Node.TypeKey).Any(preset => preset.Id == SelectedId);

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

    private Task ExportAsync() => RunAsync(async () =>
    {
        await Library.ExportAsync(SelectedId, Node.TypeKey);
        Message = "Exported selected preset.";
    }, libraryChanged: false);

    private Task PreviewImportAsync(InputFileChangeEventArgs args) => RunAsync(async () =>
    {
        ImportPreview = null;
        try
        {
            using var stream = args.File.OpenReadStream(256 * 1024);
            using var reader = new StreamReader(stream);
            ImportPreview = Library.PreviewImport(await reader.ReadToEndAsync(), Node.TypeKey);
            ImportCopyName = ImportPreview.Name;
        }
        finally { ImportInputRevision++; }
    }, libraryChanged: false);

    private Task ImportAsync(NodePresetImportMode mode) => RunAsync(async () =>
    {
        if (ImportPreview is not { } preview) return;
        var saved = await Library.ImportAsync(preview, Node.TypeKey, mode, ImportCopyName);
        SelectedId = saved.Id;
        ImportPreview = null;
        Message = $"Imported {saved.Name}.";
    });

    private void CancelImport()
    {
        ImportPreview = null;
        Error = Message = null;
    }

    private async Task RunAsync(Func<Task> action, bool libraryChanged = true)
    {
        if (Busy) return;
        Busy = true;
        Error = Message = null;
        try
        {
            await action();
            if (libraryChanged) await OnLibraryChanged.InvokeAsync();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { Busy = false; }
    }
}
