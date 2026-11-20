using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Timing;

namespace Orbit.Audio.Compilation;

/// <summary>Resolves the phase source independently of waveform generation.
/// Free-running oscillators integrate instantaneous Hz; synchronized ones
/// derive phase directly from the one shared beat signal.</summary>
internal static class FaustOscillatorTiming
{
    internal sealed record PhaseSource(string Cycles, string? Increment, string MaximumIncrement);

    public static PhaseSource Resolve(FaustNodeContext context) =>
        (OscillatorTimingMode)context.ConstantParameter("timing") switch
        {
            OscillatorTimingMode.Free => Free(context),
            OscillatorTimingMode.Synchronized => Synchronized(context),
            _ => throw new FaustCompilationException("Unsupported oscillator timing mode.")
        };

    private static PhaseSource Free(FaustNodeContext context)
    {
        var frequency = context.Parameter("frequency", "0.49 * ma.SR");
        return new($"os.lf_sawpos({frequency})", $"({frequency}) / ma.SR", "0.49");
    }

    private static PhaseSource Synchronized(FaustNodeContext context)
    {
        var division = context.Parameter("division");
        // Discrete note changes use the canonical cycle lengths (no smoothing).
        var beats = FaustMusicalDivision.Beats(division);
        var maximumFrequency = MusicalTransport.MaximumTempo / 60 /
            Enum.GetValues<MusicalDivision>().Min(MusicalTime.GetBeats);
        // Bound the edge correction on a reset/division change. The waveform
        // phase still immediately follows the transport; only smoothing is bounded.
        return new($"{context.TransportBeat} / ({beats})", null,
            $"{FaustNumber.Format(maximumFrequency)} / ma.SR");
    }
}
