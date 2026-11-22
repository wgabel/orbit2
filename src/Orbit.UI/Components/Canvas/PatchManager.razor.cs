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
    [Parameter, EditorRequired] public SavedPatchOverview Overview { get; set; } = SavedPatchOverview.Empty;
    [Parameter] public bool OverviewEditDisabled { get; set; }
    [Parameter] public EventCallback<bool> OnOverviewDraftChanged { get; set; }
    [Parameter] public EventCallback<SavedPatchOverview> OnOverviewApplied { get; set; }

    private bool IsOpen { get; set; }
    private bool Busy { get; set; }
    private string Name { get; set; } = "";
    private string Selected { get; set; } = "";
    private PatchLibraryEntry[] Entries { get; set; } = [];
    private PatchLibraryEntry[] Filtered => Entries.Where(entry => entry.Matches(Query, Tag)).ToArray();
    private PatchLibraryEntry? SelectedEntry => Filtered.FirstOrDefault(entry => entry.Name == Selected);
    private string[] AvailableTags => Entries.SelectMany(entry => entry.Overview.Tags)
        .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    private string Query { get; set; } = "";
    private string Tag { get; set; } = "";
    private bool EditingOverview { get; set; }
    private string Example { get; set; } = PatchExampleCatalog.ModulationKey;
    private string? Message { get; set; }
    private string? Error { get; set; }

    private async Task ToggleAsync()
    {
        if (EditingOverview || Busy) return;
        IsOpen = !IsOpen;
        if (IsOpen) await RunAsync(RefreshAsync);
    }

    private async Task RefreshAsync()
    {
        Entries = await Library.ListEntriesAsync();
        if (!AvailableTags.Contains(Tag, StringComparer.OrdinalIgnoreCase)) Tag = "";
        ReconcileSelection();
    }

    private void ReconcileSelection()
    {
        if (SelectedEntry is null) Selected = "";
    }

    private void ChangeQuery(ChangeEventArgs args) { Query = args.Value?.ToString() ?? ""; ReconcileSelection(); }
    private void ChangeTag(ChangeEventArgs args) { Tag = args.Value?.ToString() ?? ""; ReconcileSelection(); }
    private void ClearFilters() { Query = Tag = ""; ReconcileSelection(); }

    private async Task BeginOverviewAsync()
    {
        if (Busy || OverviewEditDisabled) return;
        try
        {
            Error = Message = null;
            await OnOverviewDraftChanged.InvokeAsync(true);
            EditingOverview = true;
        }
        catch (Exception exception) { Error = exception.Message; }
    }

    private async Task ApplyOverviewAsync(SavedPatchOverview overview)
    {
        await OnOverviewApplied.InvokeAsync(overview);
        await CancelOverviewAsync();
        Message = "Patch overview updated.";
    }

    private async Task CancelOverviewAsync()
    {
        await OnOverviewDraftChanged.InvokeAsync(false);
        EditingOverview = false;
    }

    private Task SaveAsync() => RunAsync(async () =>
    {
        await Library.SaveAsync(Name, Capture());
        ClearFilters();
        Selected = Name.Trim();
        await RefreshAsync();
        Message = $"Saved {Selected}.";
    });

    private Task OverwriteAsync() => RunAsync(async () =>
    {
        await Library.SaveAsync(Selected, Capture(), overwrite: true);
        var savedName = Selected;
        await RefreshAsync();
        Message = $"Updated {savedName}.";
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
    private Task ExportExampleAsync() => RunAsync(() => Library.ExportExampleAsync(Example));

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
