using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Compilation;

/// <summary>Phase-to-waveform DSP expressions. A single phasor preserves frequency
/// integration under modulation. Polynomial step/slope corrections reduce aliasing
/// without a minimum audio frequency, so these shapes also work as slow LFOs.</summary>
internal static class FaustOscillatorWaveforms
{
    public static string Emit(OscillatorWaveform waveform, string frequency, string phaseDegrees)
    {
        // PolyBLEP smooths saw/square steps over the adjacent sample intervals.
        // Integrating that correction (PolyBLAMP) rounds the triangle's slope changes.
        // Clamp the normalized result before multiplying by the editable amplitude,
        // keeping the signal-range estimator true even at extreme sample rates.
        var expression = waveform switch
        {
            OscillatorWaveform.Sine => "sin(2.0 * ma.PI * p)",
            OscillatorWaveform.Triangle =>
                "1.0 - 4.0 * abs(q - 0.5) + 4.0 * (blamp(q) - blamp(wrap(q + 0.5)))",
            OscillatorWaveform.Square => "2.0 * float(p < 0.5) - 1.0 + blep(p) - blep(wrap(p + 0.5))",
            OscillatorWaveform.Sawtooth => "2.0 * p - 1.0 - blep(p)",
            _ => throw new FaustCompilationException($"Unsupported oscillator waveform: {waveform}.")
        };
        return $$"""
            min(1.0, max(-1.0, {{expression}})) with {
                f = {{frequency}};
                d = f / ma.SR;
                wrap(x) = x - floor(x);
                p = wrap(os.lf_sawpos(f) + {{phaseDegrees}} / 360.0);
                q = wrap(p + 0.25);
                left(x) = max(0.0, 1.0 - x / d);
                right(x) = max(0.0, 1.0 - (1.0 - x) / d);
                blep(x) = right(x) * right(x) - left(x) * left(x);
                blamp(x) = d / 3.0 * (left(x) * left(x) * left(x) + right(x) * right(x) * right(x));
            }
            """;
    }
}
