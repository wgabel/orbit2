using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class RhythmicSourceFaustEmitter(string type) : IFaustNodeEmitter
{
    public string TypeKey => type;
    public ParameterRange? EstimateOutputRange(SignalRangeContext context) => new(0, 1);
    public string Emit(FaustNodeContext context) => type switch
    {
        ManualTriggerGenerator.NodeType => $"({context.RhythmicActionInput(false)} == 1)",
        GateGenerator.NodeType => FaustRhythmicSources.TimedGate(
            context.ConstantParameter(GateGenerator.SourceKey) == 0 ? context.RhythmicActionInput(true) : context.Input(GateGenerator.TriggerPortKey),
            context.Parameter(GateGenerator.DurationKey)),
        ClockGenerator.NodeType => context.ConstantParameter("timing") == 0
            ? FaustRhythmicSources.FreeClock(context.Parameter("frequency"))
            : FaustRhythmicSources.SynchronizedClock(context.TransportBeat, context.TransportReset, context.Parameter("division")),
        _ => throw new FaustCompilationException("Unknown rhythmic source.")
    };
}
