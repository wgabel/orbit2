using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Timing;

namespace Orbit.Presentation.Canvas;

public sealed record PerformanceKey(string Code, string Hint, int Semitone, bool Black);

public sealed record PerformancePianoKey(int Semitone, string? Hint, bool Black, int GridColumn)
{
    public bool Extension => Semitone > 12;
}

public static class PerformanceKeyboardLayout
{
    public static IReadOnlyList<PerformanceKey> Keys { get; } = Array.AsReadOnly(new PerformanceKey[]
    {
        new("KeyA", "A", 0, false), new("KeyW", "W", 1, true), new("KeyS", "S", 2, false),
        new("KeyE", "E", 3, true), new("KeyD", "D", 4, false), new("KeyF", "F", 5, false),
        new("KeyT", "T", 6, true), new("KeyG", "G", 7, false), new("KeyY", "Y", 8, true),
        new("KeyH", "H", 9, false), new("KeyU", "U", 10, true), new("KeyJ", "J", 11, false), new("KeyK", "K", 12, false)
    });

    // Two half-columns per white key. Black keys straddle adjacent whites; the
    // boundary C is shared by the two displayed octaves.
    public static IReadOnlyList<PerformancePianoKey> PianoKeys { get; } = BuildPianoKeys();

    private static IReadOnlyList<PerformancePianoKey> BuildPianoKeys()
    {
        var keys = new List<PerformancePianoKey>();
        var whites = 0;
        for (var semitone = 0; semitone <= 24; semitone++)
        {
            var key = Keys[semitone <= 12 ? semitone : semitone - 12];
            keys.Add(new(semitone, semitone <= 12 ? key.Hint : null, key.Black,
                key.Black ? whites * 2 : whites++ * 2 + 1));
        }
        return keys.AsReadOnly();
    }

    public static int Note(int octave, PerformancePianoKey key) => (octave + 1) * MusicalPitch.SemitonesPerOctave + key.Semitone;
    public static int Note(int octave, PerformanceKey key) => (octave + 1) * MusicalPitch.SemitonesPerOctave + key.Semitone;
    public static bool IsAvailable(int octave, int reference) => octave is >= -1 and <= 9 && Keys.Any(key => NoteSequencerGenerator.IsPlayable(Note(octave, key), reference));
    public static int ReferenceOctave(int reference) => Math.Clamp(reference / 12 - 1, -1, 9);
}
