namespace Orbit.Domain.Timing;

/// <summary>Shared conversion rules for musical rates; no clock or UI dependencies.</summary>
public static class MusicalTime
{
    public static double GetBeats(MusicalDivision division) => division switch
    {
        MusicalDivision.SixteenthNote => 0.25,
        MusicalDivision.EighthNote => 0.5,
        MusicalDivision.QuarterNote => 1,
        MusicalDivision.HalfNote => 2,
        MusicalDivision.WholeNote => 4,
        MusicalDivision.QuarterNoteTriplet => 2d / 3,
        MusicalDivision.DottedQuarterNote => 1.5,
        _ => throw new ArgumentOutOfRangeException(nameof(division))
    };

    public static double GetFrequency(double beatsPerMinute, double beatsPerCycle)
    {
        MusicalTransport.ValidateTempo(beatsPerMinute);
        if (!double.IsFinite(beatsPerCycle) || beatsPerCycle <= 0)
            throw new ArgumentOutOfRangeException(nameof(beatsPerCycle));
        return beatsPerMinute / (60 * beatsPerCycle);
    }
}
