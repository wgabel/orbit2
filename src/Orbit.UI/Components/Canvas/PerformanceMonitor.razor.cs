using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>One shared low-rate observer; never owns a DSP or graph lifetime.</summary>
public partial class PerformanceMonitor : IAsyncDisposable
{
    [Parameter] public GraphElementCounts EditableCounts { get; set; }
    [Parameter] public Guid PatchId { get; set; }
    [Inject] private IGraphPerformanceSource GraphSource { get; set; } = default!;
    [Inject] private IAudioPerformanceSource Source { get; set; } = default!;

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    private IJSObjectReference? editorModule;
    private IJSObjectReference? editorMonitor;
    private EditorPerformanceSnapshot? editor;
    private bool editorFailed;

    private readonly SemaphoreSlim lifecycle = new(1);
    private CancellationTokenSource? cancellation;
    private Task? observer;
    private AudioPerformanceSnapshot? snapshot;
    private GraphPerformanceSnapshot? graphDiagnostics;
    private bool expanded;
    private bool failed;
    private bool resetPending;
    private bool disposed;

    private string CompactLatency => failed ? "Unavailable" :
        snapshot?.EstimatedPipelineLatencyMs is double pipeline ? $"~{pipeline:0.#} ms" :
        snapshot?.OutputLatencyMs is double output ? $"Output ~{output:0.#} ms" : "Latency —";
    private GraphPerformanceSnapshot? CurrentGraph => graphDiagnostics?.PatchId == PatchId ? graphDiagnostics : null;
    private string CompactUnderruns => failed ? "Underruns —" :
        snapshot?.UnderrunEvents is long count ? $"Underruns {count}" : "Underruns —";

    private string CompactFps => editorFailed ? "UI —" :
        editor?.State == "paused" ? "UI paused" :
        editor?.Fps is double fps ? $"UI {fps:0} FPS" : "UI —";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if ((!expanded && observer is null) ||
            (expanded && observer is not null && cancellation?.IsCancellationRequested == false)) return;
        await lifecycle.WaitAsync();
        try
        {
            if (observer is not null && (!expanded || disposed || cancellation!.IsCancellationRequested))
                await StopObserverAsync();
            if (expanded && !disposed && observer is null)
            {
                cancellation = new();
                observer = ObserveAsync(cancellation.Token);
            }
        }
        finally { lifecycle.Release(); }
    }

    internal void CloseMobilePanel()
    {
        if (expanded) { Toggle(); StateHasChanged(); }
    }

    private void Toggle()
    {
        expanded = !expanded;
        if (expanded) return;
        cancellation?.Cancel();
        GraphSource.SetPerformanceCollection(false);
        snapshot = null;
        graphDiagnostics = null;
        editor = null;
        failed = editorFailed = resetPending = false;
    }
    private void RequestReset() => resetPending = true;

    private async Task ObserveAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            GraphSource.SetPerformanceCollection(true);
            try { await Source.SetPerformanceCollectionAsync(true); }
            catch (Exception) when (!token.IsCancellationRequested) { failed = true; }
            if (token.IsCancellationRequested) return;
            do
            {
                var changed = false;
                var reset = resetPending;
                try
                {
                    // Only an open monitor owns the editor observer, independent of playback.
                    editorModule ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", BrowserBuild.ModulePath("editorPerformance.js"));
                    if (disposed || token.IsCancellationRequested) return;
                    editorMonitor ??= await editorModule.InvokeAsync<IJSObjectReference>("createEditorPerformanceMonitor");
                    if (disposed || token.IsCancellationRequested) return;
                    var nextEditor = await editorMonitor.InvokeAsync<EditorPerformanceSnapshot>("read", token);
                    if (disposed || token.IsCancellationRequested) return;
                    changed = editorFailed || editor != nextEditor;
                    editor = nextEditor;
                    editorFailed = false;
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    changed = !editorFailed;
                    editorFailed = true;
                    editor = null;
                }

                // Independent failure handling: an unavailable UI metric cannot hide audio metrics.
                try
                {
                    var next = await Source.GetPerformanceSnapshotAsync(reset, token);
                    if (disposed || token.IsCancellationRequested) return;
                    // A hidden tab returns null: retain the last foreground audio sample.
                    if (next is not null)
                    {
                        if (reset) resetPending = false;
                        var graph = GraphSource.GetGraphPerformanceSnapshot(PatchId);
                        if (failed || snapshot != next || graphDiagnostics != graph || reset)
                        {
                            snapshot = next;
                            graphDiagnostics = graph;
                            failed = false;
                            changed = true;
                        }
                    }
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    if (disposed) return;
                    if (!failed || resetPending)
                    {
                        failed = true;
                        resetPending = false;
                        changed = true;
                    }
                }
                if (changed && !disposed) await InvokeAsync(StateHasChanged);
            }
            while (await timer.WaitForNextTickAsync(token));
        }
        catch (Exception) when (token.IsCancellationRequested) { }
        finally
        {
            GraphSource.SetPerformanceCollection(false);
            try { await Source.SetPerformanceCollectionAsync(false); }
            catch (JSException) { }
            // Closing the panel stops even the per-frame JS callbacks.
            if (editorMonitor is not null)
            {
                try { await editorMonitor.InvokeVoidAsync("dispose"); }
                catch (JSException) { }
                finally { await editorMonitor.DisposeAsync(); editorMonitor = null; }
            }
            if (editorModule is not null)
            {
                await editorModule.DisposeAsync();
                editorModule = null;
            }
        }
    }

    private static string Format(double? value, string unit, string format = "0.##") =>
        value is double number ? $"{number.ToString(format)} {unit}" : "Unavailable";

    private async Task StopObserverAsync()
    {
        cancellation?.Cancel();
        if (observer is not null) await observer;
        observer = null;
        cancellation?.Dispose();
        cancellation = null;
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        cancellation?.Cancel();
        await lifecycle.WaitAsync();
        try { await StopObserverAsync(); }
        finally { lifecycle.Release(); }
    }
}
