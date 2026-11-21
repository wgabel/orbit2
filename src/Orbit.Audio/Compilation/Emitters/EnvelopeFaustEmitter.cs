using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class EnvelopeFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => EnvelopeGenerator.NodeType;

    public ParameterRange? EstimateOutputRange(SignalRangeContext context) => new(0, 1);

    public string Emit(FaustNodeContext context)
    {
        var mode = (EnvelopeMode)context.ConstantParameter(EnvelopeGenerator.ModeKey);
        string signal = mode switch
        {
            EnvelopeMode.Ad => context.ConstantParameter(EnvelopeGenerator.TriggerSourceKey) switch
            {
                1 => context.Input(EnvelopeGenerator.EventTriggerPortKey),
                2 => FaustSchmittEdge.Emit(context.Input(EnvelopeGenerator.GatePortKey)),
                _ => FaustSchmittEdge.Emit(context.Input(EnvelopeGenerator.TriggerPortKey))
            },
            EnvelopeMode.Ar or EnvelopeMode.Adsr => context.Input(EnvelopeGenerator.GatePortKey),
            _ => "0.0"
        };
        string Control(string key, string dormant = "0.0") => context.Node.IsParameterActive(context.Node.Parameters.Single(p => p.Definition.Key == key))
            ? context.Parameter(key, initializeAtTarget: mode != EnvelopeMode.Ad) : dormant;
        return FaustEnvelopeSegments.Emit(mode, signal, Control(EnvelopeGenerator.AttackKey), Control(EnvelopeGenerator.DecayKey),
            Control(EnvelopeGenerator.AttackCurveKey), Control(EnvelopeGenerator.DecayCurveKey), Control(EnvelopeGenerator.ReleaseKey),
            Control(EnvelopeGenerator.ReleaseCurveKey), Control(EnvelopeGenerator.SustainKey), Control(EnvelopeGenerator.RestKey));
    }
}
