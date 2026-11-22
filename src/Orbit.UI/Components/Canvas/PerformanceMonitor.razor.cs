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

    private readonly CancellationTokenSource cancellation = new();
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

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) observer = ObserveAsync(cancellation.Token);
        return Task.CompletedTask;
    }

    internal void CloseMobilePanel()
    {
        if (expanded) { expanded = false; StateHasChanged(); }
    }

    private void Toggle() => expanded = !expanded;
    private void RequestReset() => resetPending = true;

    private async Task ObserveAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                var changed = false;
                var reset = resetPending;
                try
                {
                    // The editor observer belongs to this workspace, not audio playback.
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
    }

    private static string Format(double? value, string unit, string format = "0.##") =>
        value is double number ? $"{number.ToString(format)} {unit}" : "Unavailable";

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        cancellation.Cancel();
        if (observer is not null) await observer;
        // Cancel sampling first, then explicitly stop rAF and PerformanceObservers before releasing the JS references.
        if (editorMonitor is not null)
        {
            try { await editorMonitor.InvokeVoidAsync("dispose"); }
            catch (JSException) { }
            finally { await editorMonitor.DisposeAsync(); }
        }
        if (editorModule is not null) await editorModule.DisposeAsync();
        cancellation.Dispose();
    }
}
