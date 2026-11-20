namespace Orbit.Domain.Timing;

/// <summary>
/// Musical time derived from the shared sample clock, not a wall-clock timer.
/// Tempo changes preserve beat position; stopping freezes it without stopping audio.
/// This model does not generate DSP events or drive samples itself.
/// </summary>
public sealed class MusicalTransport
{
    public const double MinimumTempo = 20;
    public const double MaximumTempo = 300;
    public const double DefaultTempo = 120;
    private double anchorBeat;
    private long anchorSample;
    private int anchorSampleRate;

    public MusicalTransport(double beatsPerMinute = DefaultTempo)
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

    public void SetTempo(double beatsPerMinute, AudioClockSnapshot? clock = null)
    {
        ValidateTempo(beatsPerMinute);
        anchorBeat = GetBeatPosition(clock);
        if (clock is not null)
        {
            anchorSample = clock.CurrentSample;
            anchorSampleRate = clock.SampleRate;
        }
        BeatsPerMinute = beatsPerMinute;
    }

    /// <summary>Return to beat zero, preserving tempo and running state.</summary>
    public void Reset(AudioClockSnapshot? clock = null)
    {
        GetBeatPosition(clock); // Validate the current clock before changing anything.
        anchorBeat = 0;
        if (clock is not null)
        {
            anchorSample = clock.CurrentSample;
            anchorSampleRate = clock.SampleRate;
        }
    }

    public double GetBeatPosition(AudioClockSnapshot? clock = null)
    {
        if (clock is not null) ValidateClock(clock);
        if (!IsPlaying)
            return anchorBeat;

        ArgumentNullException.ThrowIfNull(clock);
        if (clock.SampleRate != anchorSampleRate || clock.CurrentSample < anchorSample)
            throw new InvalidOperationException("The audio clock changed during transport playback.");

        var elapsedSeconds = (clock.CurrentSample - anchorSample) / (double)anchorSampleRate;
        return anchorBeat + elapsedSeconds * BeatsPerMinute / 60;
    }

    /// <summary>Converts a musical cycle length, in beats, to a frequency in Hz.</summary>
    public double GetSynchronizedFrequency(double beatsPerCycle)
    {
        return MusicalTime.GetFrequency(BeatsPerMinute, beatsPerCycle);
    }

    public double GetSynchronizedFrequency(MusicalDivision division) =>
        GetSynchronizedFrequency(MusicalTime.GetBeats(division));

    public static void ValidateTempo(double beatsPerMinute)
    {
        if (!double.IsFinite(beatsPerMinute) || beatsPerMinute < MinimumTempo || beatsPerMinute > MaximumTempo)
            throw new ArgumentOutOfRangeException(nameof(beatsPerMinute), $"Tempo must be between {MinimumTempo} and {MaximumTempo} BPM.");
    }

    private static void ValidateClock(AudioClockSnapshot clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (clock.CurrentSample < 0 || clock.SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(clock));
    }
}
