namespace Orbit.Audio.Compilation;

/// <summary>Schroeder-Moorer mono late reverb: eight damped feedback paths and
/// four serial all-pass diffusers. All lengths are expressed in seconds.</summary>
internal static class FaustSchroederReverb
{
    public static string Emit(string input, string decay, string damping, string mix) => $$"""
        ((1.0 - mix) * x + mix * wet) with {
            x = {{input}};
            mix = {{mix}};
            decay = {{decay}};
            damping = {{damping}};
            cutoff = min(0.45 * ma.SR, 20000.0 * pow(0.025, damping));
            pole = exp(-2.0 * ma.PI * cutoff / ma.SR);
            damp = *(1.0 - pole) : (+ ~ *(pole));
            comb(seconds) = ((+ : de.delay(8192, samples - 1)) ~ (damp : *(feedback))) : mem with {
                samples = int(max(1.0, min(8192.0, seconds * ma.SR)));
                feedback = pow(10.0, -3.0 * float(samples) / (ma.SR * decay));
            };
            diffuse(seconds) = fi.allpass_comb(4096, int(max(1.0, min(4096.0, seconds * ma.SR))), -0.5);
            wet = x <: (comb(0.0253), comb(0.0269), comb(0.0289), comb(0.0307),
                        comb(0.0322), comb(0.0338), comb(0.0353), comb(0.0367)) :>
                *(0.01875) : diffuse(0.0127) : diffuse(0.0100) : diffuse(0.0077) : diffuse(0.0051);
        }
        """;
}
