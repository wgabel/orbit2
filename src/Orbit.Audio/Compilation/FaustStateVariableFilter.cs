using Orbit.Domain.Nodes.Processors;

namespace Orbit.Audio.Compilation;

/// <summary>Topology-preserving transform state-variable filter with two trapezoidal
/// integrator states. Cutoff and damping are signals, not block-rate controls.</summary>
internal static class FaustStateVariableFilter
{
    internal const double MaximumCutoffRatio = 0.45;
    internal static string MaximumCutoff => $"{FaustNumber.Format(MaximumCutoffRatio)} * ma.SR";

    public static string Emit(FilterMode mode, string input, string cutoff, string resonance)
    {
        var output = mode switch
        {
            FilterMode.LowPass => "low",
            FilterMode.HighPass => "high",
            FilterMode.BandPass => "band",
            FilterMode.Notch => "high + low",
            _ => throw new FaustCompilationException($"Unsupported filter mode: {mode}.")
        };
        return EmitCore(input, cutoff, $"1.0 / ({resonance})", output, "");
    }

    // Bell response: gain-dependent damping gives reciprocal boost/cut curves.
    public static string EmitPeak(string input, string frequency, string gain, string quality) =>
        EmitCore(input, frequency, $"1.0 / (({quality}) * peakGain)",
            "x + k * (peakGain * peakGain - 1.0) * band", $"peakGain = pow(10.0, ({gain}) / 40.0);");

    private static string EmitCore(string input, string cutoff, string damping, string output, string additionalDefinitions)
    {
        // Two delayed feedback signals hold the equivalent integrator states.
        // The third output is the selected response, not a feedback state.
        return $$"""
            ((step ~ (_, _)) : !, !, _) with {
                x = {{input}};
                g = tan(ma.PI * {{cutoff}} / ma.SR);
                {{additionalDefinitions}}
                k = {{damping}};
                a = 1.0 / (1.0 + g * (g + k));
                step(s1, s2) = 2.0 * band - s1, 2.0 * low - s2, {{output}} with {
                    band = a * (s1 + g * (x - s2));
                    low = s2 + g * band;
                    high = x - k * band - low;
                };
            }
            """;
    }
}
