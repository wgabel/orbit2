using Orbit.Domain.Nodes.Processors;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class FilterFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => FilterProcessor.NodeType;

    public string Emit(FaustNodeContext context) => FaustStateVariableFilter.Emit(
        (FilterMode)context.ConstantParameter("mode"), context.Input("audio-in"),
        context.Parameter("cutoff", FaustStateVariableFilter.MaximumCutoff), context.Parameter("resonance"));
}
