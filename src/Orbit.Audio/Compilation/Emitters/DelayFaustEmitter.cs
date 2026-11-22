using Orbit.Domain.Nodes.Processors;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class DelayFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => DelayProcessor.NodeType;

    public string Emit(FaustNodeContext context)
    {
        var synchronized = (DelayTimingMode)context.ConstantParameter("timing") == DelayTimingMode.Synchronized;
        var time = synchronized
            ? context.SmoothDerivedControl("time", $"(60.0 * ({FaustMusicalDivision.Beats(context.Parameter("division"))}) / {context.TransportTempo})")
            : context.Parameter("time");
        // The longest synchronized division is four beats at 20 BPM (12 s).
        // Size for Faust's maximum supported sample rate, with a single delay line.
        var maximumSamples = synchronized ? 2304000 : 384000;
        var input = context.Input("audio-in");
        var feedback = context.Parameter("feedback");
        var mix = context.Parameter("mix");
        return $$"""
            ((1.0 - mix) * x + mix * wet) with {
                x = {{input}};
                mix = {{mix}};
                delaySamples = max(1.0, min({{maximumSamples}}.0, ma.SR * {{time}}));
                wet = x : ((+ : de.fdelay({{maximumSamples}}, delaySamples - 1.0)) ~ *({{feedback}})) : mem;
            }
            """;
    }
}
