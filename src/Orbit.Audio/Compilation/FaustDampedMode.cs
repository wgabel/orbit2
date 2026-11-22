namespace Orbit.Audio.Compilation;

/// <summary>Two delayed orthogonal states. A rotation followed by contraction remains
/// stable with time-varying frequency and radius. Normalized excitation uses (1-r)
/// so a long decay does not implicitly increase the worst-case steady-state gain.</summary>
public static class FaustDampedMode
{
    public static string Emit(string input, string frequency, string decay, string excitation, string outputGain, string mix) => $$"""
        ((step ~ (_, _)) : _, !) * ({{outputGain}}) * ({{mix}}) + x * (1.0 - ({{mix}})) with {
            x = {{input}};
            theta = 2.0 * ma.PI * ({{frequency}}) / ma.SR;
            r = exp(log(0.001) / (({{decay}}) * ma.SR));
            c = r * cos(theta);
            s = r * sin(theta);
            e = (1.0 - r) * ({{excitation}}) * x;
            step(re, im) = c * re - s * im + e, s * re + c * im;
        }
        """;
}
