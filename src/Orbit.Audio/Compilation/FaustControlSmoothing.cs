using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation;

/// <summary>DSP base-control response policies. Durations are approximately 95%
/// settling times, not envelope attacks or host scheduling delays.</summary>
internal static class FaustControlSmoothing
{
    public static string? Filter(ParameterSmoothing policy)
    {
        // Preserve conservative settling for time-varying delay lines, feedback,
        // decay and envelope timing until dedicated acoustic tests justify a change.
        double? milliseconds = policy switch
        {
            ParameterSmoothing.None => null,
            ParameterSmoothing.Pitch => 5,
            ParameterSmoothing.Amplitude or ParameterSmoothing.Phase => 10,
            ParameterSmoothing.Filter or ParameterSmoothing.EffectMix or
                ParameterSmoothing.Rate or ParameterSmoothing.Shape => 15,
            ParameterSmoothing.DelayTime or ParameterSmoothing.Feedback or
                ParameterSmoothing.Decay or ParameterSmoothing.Timing => 68,
            _ => throw new FaustCompilationException($"Missing or unsupported smoothing policy: {policy}.")
        };
        if (milliseconds is null) return null;

        // exp(-3) ~= 0.05 remaining error after the specified duration.
        return $"si.smooth(exp(-3000.0 / (ma.SR * {FaustNumber.Format(milliseconds.Value)})))";
    }
}
