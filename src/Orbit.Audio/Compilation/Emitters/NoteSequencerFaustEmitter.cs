using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class NoteSequencerFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => NoteSequencerGenerator.NodeType;
    public ParameterRange? EstimateOutputRange(SignalRangeContext context) => new(-NoteSequencerGenerator.MaximumOffset, NoteSequencerGenerator.MaximumOffset);

    public string Emit(FaustNodeContext context)
    {
        var action = context.PerformancePitchInput();
        var reference = context.Parameter(NoteSequencerGenerator.ReferenceKey);
        var initial = context.Parameter(NoteSequencerGenerator.InitialPitchKey, initializeAtTarget: true);
        var meter = context.PitchMeter();
        return $$"""
            (offset : hbargraph("{{meter}}", -60, 60)) with {
                action = {{action}};
                valid = (action >= 1) & (action <= 129);
                held = (select2(valid, _, select2(action == 129, action, 0))) ~ _;
                offset = max(-60.0, min(60.0, select2(held > 0, {{initial}}, held - 1 - {{reference}})));
            }
            """;
    }
}
