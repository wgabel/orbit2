using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Timing;

namespace Orbit.Presentation.Canvas;

public sealed record PerformanceKey(string Code, string Hint, int Semitone, bool Black);

public static class PerformanceKeyboardLayout
{
    public static IReadOnlyList<PerformanceKey> Keys { get; } = Array.AsReadOnly(new PerformanceKey[]
    {
        new("KeyA", "A", 0, false), new("KeyW", "W", 1, true), new("KeyS", "S", 2, false),
        new("KeyE", "E", 3, true), new("KeyD", "D", 4, false), new("KeyF", "F", 5, false),
        new("KeyT", "T", 6, true), new("KeyG", "G", 7, false), new("KeyY", "Y", 8, true),
        new("KeyH", "H", 9, false), new("KeyU", "U", 10, true), new("KeyJ", "J", 11, false), new("KeyK", "K", 12, false)
    });

    public static int Note(int octave, PerformanceKey key) => (octave + 1) * MusicalPitch.SemitonesPerOctave + key.Semitone;
    public static bool IsAvailable(int octave, int reference) => octave is >= -1 and <= 8 && Keys.Any(key => NoteSequencerGenerator.IsPlayable(Note(octave, key), reference));
    public static int ReferenceOctave(int reference) => Math.Clamp(reference / 12 - 1, -1, 8);
}
