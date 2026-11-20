using Orbit.Application.Interfaces;
using Orbit.Domain.Timing;

namespace Orbit.Application.Services;

/// <summary>One transport for the patch. Serializes asynchronous clock observations
/// and user edits so a delayed observation cannot overwrite a newer anchor.</summary>
public sealed class MusicalTransportService(IAudioClock clock, ITransportSignalSink? sink = null) : IMusicalTransportService
{
    private readonly MusicalTransport transport = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private AudioClockSnapshot? lastClock;
    private bool signalDirty;

    public Task<TransportSnapshot> SynchronizeAsync(bool audioPlaying) => ExecuteAsync(async () =>
    {
        // Do not even query the browser clock before playback has started.
        if (audioPlaying || transport.IsPlaying)
            Observe(await clock.GetSnapshotAsync(), audioPlaying);
    });

    public Task<TransportSnapshot> SetTempoAsync(double beatsPerMinute) => ExecuteAsync(async () =>
    {
        MusicalTransport.ValidateTempo(beatsPerMinute);
        if (transport.IsPlaying) Observe(await clock.GetSnapshotAsync(), true);
        if (transport.BeatsPerMinute != beatsPerMinute) signalDirty = true;
        transport.SetTempo(beatsPerMinute, lastClock);
    });

    public Task<TransportSnapshot> ResetAsync() => ExecuteAsync(async () =>
    {
        if (transport.IsPlaying) Observe(await clock.GetSnapshotAsync(), true);
        transport.Reset(lastClock);
        signalDirty = true;
    });

    private void Observe(AudioClockSnapshot? current, bool audioPlaying)
    {
        // If the context vanished or changed, freeze at the last valid sample.
        // A later observation can resume from that beat on the new context.
        var discontinuity = current is null || lastClock is { } previous &&
            (current.SampleRate != previous.SampleRate || current.CurrentSample < previous.CurrentSample);
        if (transport.IsPlaying && discontinuity)
        {
            transport.Stop(lastClock!);
            signalDirty = true;
        }
        if (current is not null) lastClock = current;

        if (audioPlaying && current is { IsRunning: true })
        {
            if (!transport.IsPlaying)
            {
                transport.Start(current);
                signalDirty = true;
            }
        }
        else if (transport.IsPlaying)
        {
            transport.Stop(lastClock!);
            signalDirty = true;
        }
    }

    private async Task<TransportSnapshot> ExecuteAsync(Func<Task> operation)
    {
        await gate.WaitAsync();
        try
        {
            await operation();
            var snapshot = new TransportSnapshot(transport.BeatsPerMinute,
                transport.GetBeatPosition(lastClock), transport.IsPlaying, lastClock);
            if (signalDirty && sink is not null)
            {
                await sink.SetTransportAsync(snapshot);
                signalDirty = false;
            }
            return snapshot;
        }
        finally { gate.Release(); }
    }
}
