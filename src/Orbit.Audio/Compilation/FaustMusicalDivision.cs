using Orbit.Domain.Timing;

namespace Orbit.Audio.Compilation;

internal static class FaustMusicalDivision
{
    public static string Beats(string division)
    {
        var beats = "1.0";
        foreach (var choice in Enum.GetValues<MusicalDivision>())
            beats = $"select2(int({division}) == {(int)choice}, {beats}, {FaustNumber.Format(MusicalTime.GetBeats(choice))})";
        return beats;
    }
}
