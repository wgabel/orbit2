using Orbit.Application.Interfaces;
using Orbit.Domain.Timing;

namespace Orbit.Application.Services;

/// <summary>One transport for the patch. Serializes asynchronous clock observations
/// and user edits so a delayed observation cannot overwrite a newer anchor.</summary>
public sealed class MusicalTransportService(IAudioClock clock) : IMusicalTransportService
{
    private readonly MusicalTransport transport = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private AudioClockSnapshot? lastClock;

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
        transport.SetTempo(beatsPerMinute, lastClock);
    });

    public Task<TransportSnapshot> ResetAsync() => ExecuteAsync(async () =>
    {
        if (transport.IsPlaying) Observe(await clock.GetSnapshotAsync(), true);
        transport.Reset(lastClock);
    });

    private void Observe(AudioClockSnapshot? current, bool audioPlaying)
    {
        // If the context vanished or changed, freeze at the last valid sample.
        // A later observation can resume from that beat on the new context.
        var discontinuity = current is null || lastClock is { } previous &&
            (current.SampleRate != previous.SampleRate || current.CurrentSample < previous.CurrentSample);
        if (transport.IsPlaying && discontinuity)
            transport.Stop(lastClock!);
        if (current is not null) lastClock = current;

        if (audioPlaying && current is { IsRunning: true })
        {
            if (!transport.IsPlaying) transport.Start(current);
        }
        else if (transport.IsPlaying)
            transport.Stop(lastClock!);
    }

    private async Task<TransportSnapshot> ExecuteAsync(Func<Task> operation)
    {
        await gate.WaitAsync();
        try
        {
            await operation();
            return new TransportSnapshot(transport.BeatsPerMinute,
                transport.GetBeatPosition(lastClock), transport.IsPlaying, lastClock);
        }
        finally { gate.Release(); }
    }
}
