using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Application.Services;

namespace Orbit.UI.Components.Canvas;

/// <summary>Owns only display state and a playback lifetime for the supplied snapshot.
/// Base-only edits are forwarded through the service; structural edits remain staged.</summary>
public partial class GraphPlaybackControls : IAsyncDisposable
{
    [Inject] private IGraphPlaybackService Playback { get; set; } = default!;
    [Parameter, EditorRequired] public GraphSnapshot Snapshot { get; set; } = default!;

    private bool IsReady { get; set; }
    private bool IsActive { get; set; }
    private bool IsStopping { get; set; }
    private bool disposed;
    private Guid activeGraph;
    private long activeRevision;
    private long lastRequestedRevision;
    private long playbackGeneration;
    private bool requiresRestart;
    private PlaybackState state;
    private string? Error { get; set; }
    private string Status => !IsReady ? "Audio engine not ready."
        : !IsActive ? "Ready · plays the current graph"
        : IsStopping ? "Stopping…"
        : state != PlaybackState.Playing ? $"Compiling revision {activeRevision}…"
        : requiresRestart ? $"Playing revision {activeRevision} · restart to apply edits"
        : Snapshot.Revision != activeRevision ? $"Playing revision {activeRevision} · applying edits…"
        : $"Playing revision {activeRevision}";

    protected override async Task OnParametersSetAsync()
    {
        // The canvas already observes committed graph events. Viewport and
        // selection changes can re-render it but do not advance its revision.
        var snapshot = Snapshot;
        if (!IsActive || IsStopping || disposed || snapshot.Id != activeGraph ||
            snapshot.Revision <= lastRequestedRevision) return;
        lastRequestedRevision = snapshot.Revision;
        var generation = playbackGeneration;
        try
        {
            var update = await Playback.UpdateAsync(snapshot);
            if (disposed || !IsActive || IsStopping || generation != playbackGeneration || !update.IsActive) return;
            activeRevision = Math.Max(activeRevision, update.AppliedRevision);
            if (snapshot.Revision == lastRequestedRevision)
            {
                requiresRestart = update.RequiresRestart;
                Error = null;
            }
        }
        catch (Exception exception)
        {
            if (!disposed && IsActive && generation == playbackGeneration && snapshot.Revision == lastRequestedRevision)
            {
                requiresRestart = true;
                Error = $"Live parameter update failed: {exception.Message}";
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            await Playback.InitializeAsync();
            IsReady = true;
        }
        catch (Exception exception)
        {
            Error = $"Audio initialization failed: {exception.Message}";
        }
        if (!disposed) StateHasChanged();
    }

    private async Task PlayAsync()
    {
        if (!IsReady || IsActive || disposed) return;
        var graph = Snapshot;
        activeGraph = graph.Id;
        activeRevision = graph.Revision;
        lastRequestedRevision = graph.Revision;
        playbackGeneration++;
        requiresRestart = false;
        state = PlaybackState.Starting;
        Error = null;
        IsActive = true;

        using var cancellation = new CancellationTokenSource();
        var startup = ObserveStartupAsync(graph.Id, cancellation.Token);
        try
        {
            await Playback.PlayAsync(graph);
        }
        catch (Exception exception)
        {
            if (!disposed) Error = $"Playback failed: {exception.Message}";
        }
        finally
        {
            cancellation.Cancel();
            await startup;
            IsActive = false;
            IsStopping = false;
            state = PlaybackState.Idle;
        }
    }

    private async Task ObserveStartupAsync(Guid graphId, CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                if (disposed || cancellation.IsCancellationRequested) return;
                var current = await Playback.GetStateAsync(graphId);
                if (current != PlaybackState.Playing) continue;
                if (disposed || cancellation.IsCancellationRequested) return;
                state = current;
                await InvokeAsync(StateHasChanged);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception) when (disposed || cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Error = $"Audio status unavailable: {exception.Message}";
            if (!disposed) await InvokeAsync(StateHasChanged);
        }
    }

    private async Task StopAsync()
    {
        if (!IsActive || IsStopping) return;
        IsStopping = true;
        try { await Playback.StopAsync(activeGraph); }
        catch (Exception exception)
        {
            IsStopping = false;
            Error = $"Could not stop playback: {exception.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (IsActive)
            await Playback.StopAsync(activeGraph);
    }
}
