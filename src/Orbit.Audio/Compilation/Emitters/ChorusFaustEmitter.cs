using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class ChorusFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => ChorusProcessor.NodeType;

    // Fractional interpolation and the dry/wet average are convex sums, so
    // the input range (including initial delay-buffer silence) is conservative.
    public ParameterRange? EstimateOutputRange(SignalRangeContext context) =>
        context.Input("audio-in") is { } input
            ? new ParameterRange(Math.Min(0, input.Minimum), Math.Max(0, input.Maximum)) : null;

    public string Emit(FaustNodeContext context)
    {
        var input = context.Input("audio-in");
        var rate = context.Parameter("rate");
        var depth = context.Parameter("depth");
        var delay = context.Parameter("delay");
        var mix = context.Parameter("mix");
        // 50 ms at the maximum supported 192 kHz sample rate.
        return $$"""
            ((1.0 - mix) * x + mix * wet) with {
                x = {{input}};
                mix = {{mix}};
                center = {{delay}};
                excursion = min({{depth}}, center - 1.0);
                phase = os.lf_sawpos({{rate}});
                tap(offset) = max(1.0, min(9600.0, ma.SR * 0.001 * (center + excursion * sin(2.0 * ma.PI * (phase + offset)))));
                wet = 0.5 * ((x : de.fdelay(9600, tap(0.0))) + (x : de.fdelay(9600, tap(1.0 / 3.0))));
            }
            """;
    }
}
