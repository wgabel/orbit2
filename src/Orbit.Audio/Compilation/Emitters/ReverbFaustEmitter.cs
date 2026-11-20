using Orbit.Domain.Nodes.Processors;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class ReverbFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => ReverbProcessor.NodeType;

    public string Emit(FaustNodeContext context) => FaustSchroederReverb.Emit(
        context.Input("audio-in"), context.Parameter("decay"), context.Parameter("damping"), context.Parameter("mix"));
}
