using Microsoft.AspNetCore.Components;
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

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) observer = ObserveAsync(cancellation.Token);
        return Task.CompletedTask;
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
                var reset = resetPending;
                try
                {
                    var next = await Source.GetPerformanceSnapshotAsync(reset, token);
                    if (disposed || token.IsCancellationRequested) return;
                    // A hidden tab returns null: retain the last foreground sample.
                    if (next is not null)
                    {
                        if (reset) resetPending = false;
                        var graph = GraphSource.GetGraphPerformanceSnapshot(PatchId);
                        if (failed || snapshot != next || graphDiagnostics != graph || reset)
                        {
                            snapshot = next;
                            graphDiagnostics = graph;
                            failed = false;
                            await InvokeAsync(StateHasChanged);
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
                        await InvokeAsync(StateHasChanged);
                    }
                }
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
        cancellation.Dispose();
    }
}
