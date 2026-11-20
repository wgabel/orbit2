using Orbit.Domain.Parameters;

namespace Orbit.Domain.Timing;

/// <summary>Shared musical note choices for tempo-aware generators and effects.</summary>
public static class MusicalDivisionParameter
{
    public static ParameterDefinition Create(string timingKey, int synchronizedValue) => new(
        "division", "Musical division", (int)MusicalDivision.QuarterNote, 0, 6,
        ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: false)
    {
        ActiveWhen = new(timingKey, synchronizedValue),
        Choices = Array.AsReadOnly(new ParameterChoice[]
        {
            new((int)MusicalDivision.SixteenthNote, "1/16 note"),
            new((int)MusicalDivision.EighthNote, "1/8 note"),
            new((int)MusicalDivision.QuarterNote, "1/4 note"),
            new((int)MusicalDivision.HalfNote, "1/2 note"),
            new((int)MusicalDivision.WholeNote, "1 bar (4/4)"),
            new((int)MusicalDivision.QuarterNoteTriplet, "1/4 triplet"),
            new((int)MusicalDivision.DottedQuarterNote, "Dotted 1/4 note")
        })
    };
}
