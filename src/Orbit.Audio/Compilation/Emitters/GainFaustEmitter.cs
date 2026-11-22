using Orbit.Domain.Nodes.Processors;
using Orbit.Audio.Analysis;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

/// <summary>A gain stage multiplies its incoming audio by its modulated gain.</summary>
public sealed class GainFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => GainProcessor.NodeType;

    public ParameterRange? EstimateOutputRange(SignalRangeContext context) =>
        SignalRangeContext.Multiply(context.Input("audio-in"), context.Parameter("gain"));

    public string Emit(FaustNodeContext context) =>
        $"{context.Input("audio-in")} * {context.Parameter("gain", settlingMilliseconds: FaustControlSmoothing.Amplitude)}";
}
