using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.Domain.Timing;

namespace Orbit.UI.Components.Canvas;

/// <summary>Owns only display state and a playback lifetime for the supplied snapshot.
/// The service handles base edits and prepares replacement graphs while playback continues.</summary>
public partial class GraphPlaybackControls : IAsyncDisposable
{
    [Inject] private IGraphPlaybackService Playback { get; set; } = default!;
    [Inject] private IMusicalTransportService Transport { get; set; } = default!;
    [Parameter, EditorRequired] public GraphSnapshot Snapshot { get; set; } = default!;
    [Parameter] public EventCallback<GraphParameterTelemetry?> TelemetryChanged { get; set; }
    [Parameter] public EventCallback<double> TempoChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    public bool CanReplaceWorkspace => !IsActive;

    private bool IsReady { get; set; }
    private bool IsActive { get; set; }
    private bool IsStopping { get; set; }
    private bool disposed;
    private Guid activeGraph;
    private long activeRevision;
    private long lastRequestedRevision;
    private long playbackGeneration;
    private bool updateFailed;
    private bool hasTelemetry;
    private PlaybackState state;
    private string? Error { get; set; }
    private TransportSnapshot TransportState { get; set; } = TransportSnapshot.Initial;
    private string? TransportError { get; set; }
    private string Status => !IsReady ? "Audio engine not ready."
        : !IsActive ? "Ready · plays the current graph"
        : IsStopping ? "Stopping…"
        : state != PlaybackState.Playing ? $"Compiling revision {activeRevision}…"
        : updateFailed ? $"Playing revision {activeRevision} · update failed"
        : Snapshot.Revision != activeRevision ? $"Playing revision {activeRevision} · updating patch…"
        : $"Playing revision {activeRevision}";

    protected override async Task OnParametersSetAsync()
    {
        // The canvas already observes committed graph events. Viewport and
        // selection changes can re-render it but do not advance its revision.
        var snapshot = Snapshot;
        if (!IsActive || IsStopping || disposed || snapshot.Id != activeGraph ||
            snapshot.Revision <= lastRequestedRevision) return;
        lastRequestedRevision = snapshot.Revision;
        updateFailed = false;
        var generation = playbackGeneration;
        try
        {
            var update = await Playback.UpdateAsync(snapshot);
            if (disposed || !IsActive || IsStopping || generation != playbackGeneration || !update.IsActive) return;
            activeRevision = Math.Max(activeRevision, update.AppliedRevision);
            if (snapshot.Revision == lastRequestedRevision)
            {
                Error = null;
            }
        }
        catch (Exception exception)
        {
            if (!disposed && IsActive && generation == playbackGeneration && snapshot.Revision == lastRequestedRevision)
            {
                updateFailed = true;
                Error = $"Patch update failed; keeping the last good graph: {exception.Message}";
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            await Playback.InitializeAsync();
            TransportState = await Transport.SynchronizeAsync(false);
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
        if (!IsReady || IsActive || disposed || Disabled) return;
        var graph = Snapshot;
        activeGraph = graph.Id;
        activeRevision = graph.Revision;
        lastRequestedRevision = graph.Revision;
        playbackGeneration++;
        updateFailed = false;
        state = PlaybackState.Starting;
        Error = null;
        IsActive = true;

        using var cancellation = new CancellationTokenSource();
        var observer = ObservePlaybackAsync(graph.Id, cancellation.Token);
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
            await observer;
            await SynchronizeTransportAsync(false);
            IsActive = false;
            IsStopping = false;
            state = PlaybackState.Idle;
            await PublishTelemetryAsync(null);
        }
    }

    // UI-rate sampling only. Faust continues generating values at audio rate.
    private async Task ObservePlaybackAsync(Guid graphId, CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(40));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                if (disposed || cancellation.IsCancellationRequested) return;
                if (state != PlaybackState.Playing)
                {
                    var current = await Playback.GetStateAsync(graphId);
                    if (current != PlaybackState.Playing) continue;
                    if (disposed || cancellation.IsCancellationRequested) return;
                    state = current;
                    await InvokeAsync(StateHasChanged);
                }
                // This is a read of the engine's sample clock, not a timer-based
                // beat increment. Only one transport serves the whole patch.
                await SynchronizeTransportAsync(!IsStopping);
                var frame = !IsStopping && Snapshot.Revision == activeRevision
                    ? await Playback.GetTelemetryAsync(graphId) : null;
                if (disposed || cancellation.IsCancellationRequested) return;
                await PublishTelemetryAsync(!IsStopping && frame?.Revision == Snapshot.Revision ? frame : null);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception) when (disposed || cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Error = $"Audio status unavailable: {exception.Message}";
            await PublishTelemetryAsync(null);
            if (!disposed) await InvokeAsync(StateHasChanged);
        }
    }

    private async Task PublishTelemetryAsync(GraphParameterTelemetry? frame)
    {
        if (disposed || (frame is null && !hasTelemetry)) return;
        hasTelemetry = frame is not null;
        await InvokeAsync(() => TelemetryChanged.InvokeAsync(frame));
    }

    private async Task StopAsync()
    {
        if (!IsActive || IsStopping) return;
        IsStopping = true;
        await SynchronizeTransportAsync(false);
        await PublishTelemetryAsync(null);
        try { await Playback.StopAsync(activeGraph); }
        catch (Exception exception)
        {
            IsStopping = false;
            Error = $"Could not stop playback: {exception.Message}";
        }
    }

    private Task SynchronizeTransportAsync(bool playing) =>
        UpdateTransportAsync(() => Transport.SynchronizeAsync(playing));

    private async Task SetTempoAsync(double beatsPerMinute)
    {
        await UpdateTransportAsync(() => Transport.SetTempoAsync(beatsPerMinute));
        if (TransportError is null) await TempoChanged.InvokeAsync(TransportState.BeatsPerMinute);
    }

    private Task ResetTransportAsync() => UpdateTransportAsync(Transport.ResetAsync);

    private async Task UpdateTransportAsync(Func<Task<TransportSnapshot>> operation)
    {
        // Transport failures are displayed separately and never stop audio.
        var previous = TransportState;
        var previousError = TransportError;
        try
        {
            TransportState = await operation();
            TransportError = null;
        }
        catch (Exception exception)
        {
            TransportError = $"Transport unavailable: {exception.Message}";
        }
        if (!disposed && (previous != TransportState || previousError != TransportError))
            await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (IsActive)
        {
            await Playback.StopAsync(activeGraph);
            await SynchronizeTransportAsync(false);
        }
    }
}
