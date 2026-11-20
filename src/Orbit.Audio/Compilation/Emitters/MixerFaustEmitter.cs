using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class MixerFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => MixerProcessor.NodeType;
    public string Emit(FaustNodeContext context) => context.Input("audio-in");
    public ParameterRange? EstimateOutputRange(SignalRangeContext context) => context.Input("audio-in");
}
