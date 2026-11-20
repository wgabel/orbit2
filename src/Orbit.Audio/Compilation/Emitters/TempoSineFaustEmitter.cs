using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Domain.Timing;

namespace Orbit.Audio.Compilation.Emitters;

/// <summary>Evaluates phase directly from a shared beat input, so every synchronized
/// oscillator reads the same musical position even across canvas/voice boundaries.</summary>
public sealed class TempoSineFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => TempoSineGenerator.NodeType;
    public bool RequiresTransport => true;

    public ParameterRange? EstimateOutputRange(SignalRangeContext context) =>
        SignalRangeContext.Multiply(new ParameterRange(-1, 1), context.Parameter("amplitude"));

    public string Emit(FaustNodeContext context)
    {
        var division = context.Parameter("division");
        var phase = context.Parameter("phase");
        var amplitude = context.Parameter("amplitude");
        // Map the choice index to the canonical musical cycle length; never
        // smooth an enum or interpolate between divisions.
        var beats = "1.0";
        foreach (var choice in Enum.GetValues<MusicalDivision>())
            beats = $"select2(int({division}) == {(int)choice}, {beats}, {FaustNumber.Format(MusicalTime.GetBeats(choice))})";
        return $"sin(2.0 * ma.PI * ({context.TransportBeat} / ({beats}) + {phase} / 360.0)) * {amplitude}";
    }
}
