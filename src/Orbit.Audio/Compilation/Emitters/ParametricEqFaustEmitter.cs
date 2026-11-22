using Orbit.Domain.Nodes.Processors;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class ParametricEqFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => ParametricEqProcessor.NodeType;

    public string Emit(FaustNodeContext context) => FaustStateVariableFilter.EmitPeak(
        context.Input("audio-in"), context.Parameter("frequency", FaustStateVariableFilter.MaximumCutoff),
        context.Parameter("gain"), context.Parameter("q"));
}
