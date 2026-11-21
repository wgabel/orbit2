using Orbit.Application.Interfaces;
using Orbit.Application.Services;
using Orbit.Domain.Timing;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Exercises the application clock boundary without JS, a browser or real audio time.</summary>
public sealed class MusicalTransportServiceTests
{
    [Fact]
    public async Task EditingBeforePlaybackNeedsNoAudioContext()
    {
        var clock = new TestClock();
        var service = new MusicalTransportService(clock);
        Assert.Equal(TransportSnapshot.Initial, await service.SynchronizeAsync(false));
        var tempo = await service.SetTempoAsync(60);
        Assert.Equal(60, tempo.BeatsPerMinute);
        Assert.Equal(0, tempo.BeatPosition);
        Assert.False(tempo.IsPlaying);
        Assert.Null(tempo.Clock);
        Assert.Equal(1, tempo.GetFrequency(MusicalDivision.QuarterNote));
        var reset = tempo with { ResetGeneration = 1 };
        Assert.Equal(reset, await service.ResetAsync());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetTempoAsync(301));
        Assert.Equal(0, clock.Reads);
        Assert.Equal(reset, await service.SynchronizeAsync(false));
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public async Task LiveTempoAndStopResumePreserveBeatPosition(int sampleRate)
    {
        var clock = new TestClock();
        var service = new MusicalTransportService(clock);
        clock.Set(sampleRate, sampleRate);
        Assert.Equal(0, (await service.SynchronizeAsync(true)).BeatPosition);
        clock.Set(2L * sampleRate, sampleRate);
        Assert.Equal(2, (await service.SynchronizeAsync(true)).BeatPosition);
        var changed = await service.SetTempoAsync(60);
        Assert.Equal(2, changed.BeatPosition);
        Assert.Equal(sampleRate, changed.Clock!.SampleRate);
        clock.Set(3L * sampleRate, sampleRate);
        Assert.Equal(3, (await service.SynchronizeAsync(false)).BeatPosition);
        clock.Set(5L * sampleRate, sampleRate);
        Assert.Equal(3, (await service.SynchronizeAsync(false)).BeatPosition);
        Assert.Equal(3, (await service.SynchronizeAsync(true)).BeatPosition);
        clock.Set(6L * sampleRate, sampleRate);
        Assert.Equal(4, (await service.SynchronizeAsync(true)).BeatPosition);
        var reset = await service.ResetAsync();
        Assert.True(reset.IsPlaying);
        Assert.Equal(0, reset.BeatPosition);
        clock.Set(7L * sampleRate, sampleRate);
        Assert.Equal(1, (await service.SynchronizeAsync(true)).BeatPosition);
    }

    [Fact]
    public async Task PublishesAnchorsOnlyWhenMusicalTimeChanges()
    {
        var clock = new TestClock();
        var sink = new TestSink();
        var service = new MusicalTransportService(clock, sink);
        await service.SetTempoAsync(60);
        Assert.Single(sink.Anchors);
        clock.Set(0, 48000);
        await service.SynchronizeAsync(true);
        clock.Set(24000, 48000);
        await service.SynchronizeAsync(true);
        Assert.Equal(2, sink.Anchors.Count);
        await service.SetTempoAsync(120);
        Assert.Equal(0.5, sink.Anchors[^1].BeatPosition);
        await service.ResetAsync();
        Assert.True(sink.Anchors[^1].IsPlaying);
        Assert.Equal(0, sink.Anchors[^1].BeatPosition);
        clock.Set(48000, 48000);
        await service.SynchronizeAsync(false);
        Assert.False(sink.Anchors[^1].IsPlaying);
        Assert.Equal(1, sink.Anchors[^1].BeatPosition);
        Assert.Equal(5, sink.Anchors.Count);
    }

    private sealed class TestSink : ITransportSignalSink
    {
        public List<TransportSnapshot> Anchors { get; } = [];
        public Task SetTransportAsync(TransportSnapshot snapshot)
        {
            Anchors.Add(snapshot);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ContextSuspensionAndReplacementCannotJumpMusicalTime()
    {
        var clock = new TestClock();
        var service = new MusicalTransportService(clock);
        Assert.False((await service.SynchronizeAsync(true)).IsPlaying);
        clock.Set(0, 48000);
        await service.SynchronizeAsync(true);
        clock.Set(48000, 48000, false);
        var suspended = await service.SynchronizeAsync(true);
        Assert.False(suspended.IsPlaying);
        Assert.Equal(2, suspended.BeatPosition);
        clock.Set(0, 44100);
        Assert.Equal(2, (await service.SynchronizeAsync(true)).BeatPosition);
        clock.Set(44100, 44100);
        Assert.Equal(4, (await service.SynchronizeAsync(true)).BeatPosition);
        clock.Snapshot = null;
        var missing = await service.SynchronizeAsync(true);
        Assert.False(missing.IsPlaying);
        Assert.Equal(4, missing.BeatPosition);
    }

    private sealed class TestClock : IAudioClock
    {
        public AudioClockSnapshot? Snapshot { get; set; }
        public int Reads { get; private set; }
        public void Set(long sample, int rate, bool running = true) => Snapshot = new(sample, rate, running);
        public Task<AudioClockSnapshot?> GetSnapshotAsync()
        {
            Reads++;
            return Task.FromResult(Snapshot);
        }
    }
}
