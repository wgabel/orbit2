using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Orbit.Application.Persistence;

namespace Orbit.UI.Components.Canvas;

public partial class PatchManager
{
    [Inject] private PatchLibraryService Library { get; set; } = default!;
    [Parameter, EditorRequired] public Func<PatchDocument> Capture { get; set; } = default!;
    [Parameter, EditorRequired] public Func<PatchDocument, Task> OpenDocument { get; set; } = default!;
    [Parameter, EditorRequired] public Func<Task> NewDocument { get; set; } = default!;

    private bool IsOpen { get; set; }
    private bool Busy { get; set; }
    private string Name { get; set; } = "";
    private string Selected { get; set; } = "";
    private string[] Names { get; set; } = [];
    private string? Message { get; set; }
    private string? Error { get; set; }

    private async Task ToggleAsync()
    {
        IsOpen = !IsOpen;
        if (IsOpen) await RunAsync(RefreshAsync);
    }

    private async Task RefreshAsync()
    {
        Names = await Library.ListAsync();
        if (!Names.Contains(Selected)) Selected = "";
    }

    private Task SaveAsync() => RunAsync(async () =>
    {
        await Library.SaveAsync(Name, Capture());
        Selected = Name.Trim();
        await RefreshAsync();
        Message = $"Saved {Selected}.";
    });

    private Task OverwriteAsync() => RunAsync(async () =>
    {
        await Library.SaveAsync(Selected, Capture(), overwrite: true);
        Message = $"Updated {Selected}.";
    });

    private Task DeleteAsync() => RunAsync(async () =>
    {
        await Library.DeleteAsync(Selected);
        await RefreshAsync();
        Message = "Saved patch deleted; current workspace unchanged.";
    });

    private Task OpenAsync() => RunAsync(async () =>
    {
        await OpenDocument(await Library.LoadAsync(Selected));
        Message = $"Opened {Selected}.";
    });

    private Task NewAsync() => RunAsync(async () =>
    {
        await NewDocument();
        Message = "New workspace created.";
    });

    private Task ExportAsync() => RunAsync(() => Library.ExportAsync(Capture()));

    private Task ImportAsync(InputFileChangeEventArgs args) => RunAsync(async () =>
    {
        using var stream = args.File.OpenReadStream(10 * 1024 * 1024);
        using var reader = new StreamReader(stream);
        await OpenDocument(Library.Import(await reader.ReadToEndAsync()));
        Message = "Imported patch.";
    });

    private async Task RunAsync(Func<Task> action)
    {
        if (Busy) return;
        Busy = true;
        Error = Message = null;
        try { await action(); }
        catch (Exception exception) { Error = exception.Message; }
        finally { Busy = false; }
    }
}
