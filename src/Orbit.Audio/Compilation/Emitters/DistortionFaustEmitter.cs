using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class DistortionFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => DistortionProcessor.NodeType;

    // The wet path is bounded; a partially dry signal can retain larger input peaks.
    public ParameterRange? EstimateOutputRange(SignalRangeContext context) =>
        context.Input("audio-in") is { } input
            ? new ParameterRange(Math.Min(-1, input.Minimum), Math.Max(1, input.Maximum))
            : null;

    public string Emit(FaustNodeContext context)
    {
        var input = context.Input("audio-in");
        var drive = context.Parameter("drive");
        var mix = context.Parameter("mix");
        var gain = context.Parameter("gain");
        return $$"""
            ((1.0 - mix) * x + mix * gain * wet) with {
                x = {{input}};
                mix = {{mix}};
                gain = {{gain}};
                driven = x * {{drive}};
                wet = driven / (1.0 + abs(driven));
            }
            """;
    }
}
