namespace Orbit.Audio.Compilation;

/// <summary>DSP base-control response policies. Durations are approximately 95%
/// settling times, not envelope attacks or host scheduling delays.</summary>
internal static class FaustControlSmoothing
{
    public const double Pitch = 5;
    public const double Amplitude = 10;
    public const double Phase = 10;
    public const double Cutoff = 15;

    public static string Filter(double? settlingMilliseconds)
    {
        if (settlingMilliseconds is not double milliseconds)
            return "si.smoo";
        if (!double.IsFinite(milliseconds) || milliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(settlingMilliseconds));

        // exp(-3) ~= 0.05 remaining error after the specified duration.
        return $"si.smooth(exp(-3000.0 / (ma.SR * {FaustNumber.Format(milliseconds)})))";
    }
}
