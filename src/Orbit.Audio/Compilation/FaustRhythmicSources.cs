namespace Orbit.Audio.Compilation;

public static class FaustRhythmicSources
{
    public static string FreeClock(string frequency) => $$"""
        ((1 : mem) * (phase < (phase : mem))) with {
            // Integer phase avoids cumulative floating-point integration drift.
            increment = max(1, int(floor(({{frequency}}) * 1073741824.0 / ma.SR + 0.5)));
            phase = ((+ (increment)) : %(1073741824)) ~ _;
        }
        """;

    // Ignore initial samples, explicit discontinuities, division changes, and
    // impossible forward deltas; the 12-beat wrap is a normal boundary.
    public static string SynchronizedClock(string beat, string reset, string division) => $$"""
        ((1 : mem) * ({{reset}} < 0.5) * (division == (division : mem)) * (delta > 0.0) * (delta <= 5.0 / ma.SR + 0.000002) * (boundary != (boundary : mem))) with {
            position = select2(({{beat}}) >= 12.0, {{beat}}, 0.0);
            difference = position - (position : mem);
            delta = select2(difference < 0.0, difference, difference + 12.0);
            division = int({{division}});
            boundary = int(floor(position / ({{FaustMusicalDivision.Beats("division")}})));
        }
        """;

    public static string TimedGate(string action, string milliseconds) => $$"""
        ((step ~ _) > 0) with {
            command = {{action}};
            duration = max(1, int(floor(({{milliseconds}}) * ma.SR / 1000.0 + 0.5)));
            step(previous) = select2(command > 1.5, select2(command >= 0.5, max(0, previous - 1), duration), 0);
        }
        """;
}
