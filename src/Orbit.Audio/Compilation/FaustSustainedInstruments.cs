using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Compilation;

/// <summary>Gate, calibrated length and expression wrapper shared by the pinned Faust sustained models.</summary>
public static class FaustSustainedInstruments
{
    public static string Emit(PhysicalSustainedKind kind, string pitch, string gate, string expression, string character,
        string attack, string release, string vibratoDepth, string vibratoRate, string gain)
    {
        // Empirical effective length/phase corrections for physmodels.lib 1.2.1, not universal constants.
        var model = kind switch
        {
            PhysicalSustainedKind.Clarinet => "pm.clarinetModel(340.0/f+0.101-4760.0/ma.SR, env*(0.55+0.18*expr)*(1.0+0.006*no.noise), 0.2+0.3*color, 0.5) : fi.dcblocker",
            PhysicalSustainedKind.Flute => "pm.fluteModel(504.0/f-0.359+3600.0*(1.0/44100.0-1.0/ma.SR), 0.6, env*(0.75+0.10*expr)*(1.0+(0.003+0.012*color)*no.noise))",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return $$"""
            (0.65 * raw/sqrt(1.0+raw*raw) * env*expr*({{gain}})) with {
                held = ({{gate}})>0.5;
                expr = max(0.0,min(1.0,{{expression}}));
                color = max(0.0,min(1.0,{{character}}));
                env = envStep ~ _;
                envStep(prev) = select2(held,max(0.0,prev-1.0/(max(0.02,{{release}})*ma.SR)),min(1.0,prev+1.0/(max(0.015,{{attack}})*ma.SR)));
                targetPitch = max(-21.0,min(15.0,{{pitch}}));
                // A new gate starts at the requested note; the physical tube/string retunes with a short legato glide.
                tunedPitch = pitchStep ~ _;
                pitchStep(prev) = select2(held & held',targetPitch,prev+(targetPitch-prev)*(1.0-exp(-1.0/(0.004*ma.SR))));
                vibrato = os.osc({{vibratoRate}})*({{vibratoDepth}})*0.01;
                f = 440.0*pow(2.0,max(-21.0,min(15.0,tunedPitch+vibrato))/12.0);
                raw = {{model}};
            }
            """;
    }
}
