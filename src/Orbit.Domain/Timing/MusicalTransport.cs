namespace Orbit.Domain.Timing;

/// <summary>
/// Musical time derived from the shared sample clock, not a wall-clock timer.
/// Tempo changes preserve beat position; stopping freezes it without stopping audio.
/// This model does not generate DSP events or drive samples itself.
/// </summary>
public sealed class MusicalTransport
{
    private double anchorBeat;
    private long anchorSample;
    private int anchorSampleRate;

    public MusicalTransport(double beatsPerMinute = 120)
    {
        ValidateTempo(beatsPerMinute);
        BeatsPerMinute = beatsPerMinute;
    }

    public double BeatsPerMinute { get; private set; }
    public bool IsPlaying { get; private set; }

    public void Start(AudioClockSnapshot clock)
    {
        ValidateClock(clock);
        if (IsPlaying)
            return;

        anchorSample = clock.CurrentSample;
        anchorSampleRate = clock.SampleRate;
        IsPlaying = true;
    }

    public void Stop(AudioClockSnapshot clock)
    {
        anchorBeat = GetBeatPosition(clock);
        IsPlaying = false;
    }

    public void SetTempo(double beatsPerMinute, AudioClockSnapshot clock)
    {
        ValidateTempo(beatsPerMinute);
        anchorBeat = GetBeatPosition(clock);
        anchorSample = clock.CurrentSample;
        anchorSampleRate = clock.SampleRate;
        BeatsPerMinute = beatsPerMinute;
    }

    public double GetBeatPosition(AudioClockSnapshot clock)
    {
        ValidateClock(clock);
        if (!IsPlaying)
            return anchorBeat;

        if (clock.SampleRate != anchorSampleRate || clock.CurrentSample < anchorSample)
            throw new InvalidOperationException("The audio clock changed during transport playback.");

        var elapsedSeconds = (clock.CurrentSample - anchorSample) / (double)anchorSampleRate;
        return anchorBeat + elapsedSeconds * BeatsPerMinute / 60;
    }

    /// <summary>Converts a musical cycle length, in beats, to a frequency in Hz.</summary>
    public double GetSynchronizedFrequency(double beatsPerCycle)
    {
        if (!double.IsFinite(beatsPerCycle) || beatsPerCycle <= 0)
            throw new ArgumentOutOfRangeException(nameof(beatsPerCycle));

        return BeatsPerMinute / (60 * beatsPerCycle);
    }

    private static void ValidateTempo(double beatsPerMinute)
    {
        if (!double.IsFinite(beatsPerMinute) || beatsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(beatsPerMinute));
    }

    private static void ValidateClock(AudioClockSnapshot clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (clock.CurrentSample < 0 || clock.SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(clock));
    }
}
