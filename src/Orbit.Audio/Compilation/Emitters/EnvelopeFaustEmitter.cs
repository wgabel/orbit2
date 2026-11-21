using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class EnvelopeFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => EnvelopeGenerator.NodeType;

    public ParameterRange? EstimateOutputRange(SignalRangeContext context) => new(0, 1);

    public string Emit(FaustNodeContext context) => FaustEnvelopeSegments.EmitAd(
        context.ConstantParameter(EnvelopeGenerator.TriggerSourceKey) switch
        {
            1 => context.Input(EnvelopeGenerator.EventTriggerPortKey),
            2 => FaustSchmittEdge.Emit(context.Input(EnvelopeGenerator.GatePortKey)),
            _ => FaustSchmittEdge.Emit(context.Input(EnvelopeGenerator.TriggerPortKey))
        },
        context.Parameter(EnvelopeGenerator.AttackKey), context.Parameter(EnvelopeGenerator.DecayKey),
        context.Parameter(EnvelopeGenerator.AttackCurveKey), context.Parameter(EnvelopeGenerator.DecayCurveKey));
}
