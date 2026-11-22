using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class NoteSourceFaustEmitter : IFaustMultiOutputEmitter
{
    public string TypeKey => NoteSourceGenerator.NodeType;
    public ParameterRange? EstimateOutputRange(SignalRangeContext context, string portKey) =>
        portKey == "pitch-out" ? new(-60, 60) : new(0, 1);

    public string Emit(FaustNodeContext context)
    {
        var action = context.PerformanceNoteInput();
        var reference = context.Parameter(NoteSequencerGenerator.ReferenceKey);
        var meter = context.PitchMeter();
        // The action worklet resolves the held-key stack. One tuple shares the decoded
        // action, note latch and gate state; fallback retunes without a strike.
        return $$"""
            (measured, attach(gate, measured), attach(trigger, measured), attach(velocity, measured)) with {
                action = int({{action}});
                raw = action % 131072;
                note = raw % 512;
                valid = (note >= 1) & (note <= 128);
                held = (select2(valid, _, note)) ~ _;
                pitch = select2(held > 0, 0.0, max(-60.0, min(60.0, held - 1 - {{reference}})));
                measured = pitch : hbargraph("{{meter}}", -60, 60);
                gate = (select2(valid | (raw == 129), _, valid)) ~ _;
                trigger = valid & (action >= 131072);
                velocity = (select2(valid, _, float(int(raw / 512)) / 127.0)) ~ _;
            }
            """;
    }
}
