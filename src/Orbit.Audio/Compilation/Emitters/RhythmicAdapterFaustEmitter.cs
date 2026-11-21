using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class RhythmicAdapterFaustEmitter(bool gate) : IFaustNodeEmitter
{
    public string TypeKey => gate ? SignalToGateGenerator.NodeType : SignalToTriggerGenerator.NodeType;
    public ParameterRange? EstimateOutputRange(SignalRangeContext context) => new(0, 1);
    public string Emit(FaustNodeContext context) => gate
        ? FaustSchmittEdge.EmitGate(context.Input(RhythmicAdapter.InputKey))
        : FaustSchmittEdge.Emit(context.Input(RhythmicAdapter.InputKey));
}
