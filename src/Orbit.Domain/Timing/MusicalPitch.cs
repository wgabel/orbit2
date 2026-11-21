namespace Orbit.Domain.Timing;

/// <summary>Shared equal-tempered pitch convention; fractional semitones are valid.</summary>
public static class MusicalPitch
{
    public const double ReferenceHertz = 440;
    public const int ReferenceMidiNote = 69;
    public const int SemitonesPerOctave = 12;
    public const int CentsPerSemitone = 100;

    public static double Semitones(double semitones, double cents) => semitones + cents / CentsPerSemitone;
    public static double Ratio(double semitones) => Math.Pow(2, semitones / SemitonesPerOctave);
    public static double FromMidiNote(double note) => ReferenceHertz * Ratio(note - ReferenceMidiNote);
    public static double ToMidiNote(double hertz) => ReferenceMidiNote + SemitonesPerOctave * Math.Log2(hertz / ReferenceHertz);

    public static double Frequency(double baseHertz, double semitones, double additiveHertz, double minimum, double maximum) =>
        Math.Clamp(baseHertz * Ratio(semitones) + additiveHertz, minimum, maximum);
}
