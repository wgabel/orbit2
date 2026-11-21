using Orbit.Domain.Parameters;

namespace Orbit.UI.Components.Parameters;

/// <summary>Shared human-readable unit vocabulary for value and modulation editors.</summary>
public static class ParameterUnitLabel
{
    public static string Format(ParameterUnit unit) => unit switch
    {
        ParameterUnit.Hertz => "Hz",
        ParameterUnit.Decibels => "dB",
        ParameterUnit.Seconds => "s",
        ParameterUnit.Milliseconds => "ms",
        ParameterUnit.Cents => "cent",
        ParameterUnit.Semitones => "st",
        ParameterUnit.Degrees => "°",
        ParameterUnit.BeatsPerMinute => "BPM",
        ParameterUnit.Ratio => "ratio",
        _ => "normalized"
    };
}
