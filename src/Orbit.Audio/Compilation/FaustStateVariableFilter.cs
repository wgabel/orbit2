using Orbit.Domain.Nodes.Processors;

namespace Orbit.Audio.Compilation;

/// <summary>Topology-preserving transform state-variable filter with two trapezoidal
/// integrator states. Cutoff and damping are signals, not block-rate controls.</summary>
internal static class FaustStateVariableFilter
{
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
        // Two delayed feedback signals hold the equivalent integrator states.
        // The third output is the selected response, not a feedback state.
        return $$"""
            ((step ~ (_, _)) : !, !, _) with {
                x = {{input}};
                g = tan(ma.PI * {{cutoff}} / ma.SR);
                k = 1.0 / {{resonance}};
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
