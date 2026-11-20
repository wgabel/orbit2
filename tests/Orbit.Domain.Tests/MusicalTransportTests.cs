using Orbit.Domain.Timing;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Musical time derives from sample frames, including tempo changes.</summary>
public sealed class MusicalTransportTests
{
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void AudioClockAndTransportRespectActualSampleRate(int sampleRate)
    {
        var transport = new MusicalTransport(120);
        transport.Start(new AudioClockSnapshot(0, sampleRate, true));
        var halfSecond = new AudioClockSnapshot(sampleRate / 2, sampleRate, true);

        Assert.Equal(0.5, halfSecond.CurrentTime);
        Assert.Equal(1d, transport.GetBeatPosition(halfSecond));
        Assert.Equal(2d, transport.GetSynchronizedFrequency(1));
        Assert.Equal(0.5, transport.GetSynchronizedFrequency(4));
    }

    [Fact]
    public void ChangingTempoPreservesBeatPosition()
    {
        var transport = new MusicalTransport(120);
        transport.Start(new AudioClockSnapshot(0, 48000, true));
        var changeAt = new AudioClockSnapshot(48000, 48000, true);
        transport.SetTempo(60, changeAt);

        Assert.Equal(2d, transport.GetBeatPosition(changeAt));
        Assert.Equal(3d, transport.GetBeatPosition(new AudioClockSnapshot(96000, 48000, true)));
    }

    [Fact]
    public void StoppingAndResumingTransportDoNotResetBeatPosition()
    {
        var transport = new MusicalTransport(120);
        transport.Start(new AudioClockSnapshot(48000, 48000, true));
        transport.Stop(new AudioClockSnapshot(96000, 48000, true));

        Assert.False(transport.IsPlaying);
        Assert.Equal(2d, transport.GetBeatPosition(new AudioClockSnapshot(192000, 48000, true)));

        transport.Start(new AudioClockSnapshot(192000, 48000, true));
        Assert.Equal(3d, transport.GetBeatPosition(new AudioClockSnapshot(216000, 48000, true)));
    }

    [Fact]
    public void RejectsInvalidTempoAndClockChanges()
    {
        var transport = new MusicalTransport();
        transport.Start(new AudioClockSnapshot(100, 48000, true));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            transport.SetTempo(0, new AudioClockSnapshot(100, 48000, true)));
        Assert.Throws<ArgumentOutOfRangeException>(() => transport.GetSynchronizedFrequency(double.NaN));
        Assert.Throws<InvalidOperationException>(() =>
            transport.GetBeatPosition(new AudioClockSnapshot(50, 48000, true)));
        Assert.Throws<InvalidOperationException>(() =>
            transport.GetBeatPosition(new AudioClockSnapshot(200, 44100, true)));
    }
}
