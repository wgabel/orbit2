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
        var glide = context.Parameter(NoteSequencerGenerator.GlideKey);
        var meter = context.PitchMeter();
        var stepMeter = meter.Replace("pitch_", "step_", StringComparison.Ordinal);
        return $$"""
            attach((offset : hbargraph("{{meter}}", -60, 60)), step : hbargraph("{{stepMeter}}", 0, 64)) with {
                action = {{action}};
                code = int(action) % 256;
                stepCode = int(int(action) / 256);
                step = (select2(stepCode > 0, _, select2(stepCode <= 64, 0, stepCode))) ~ _;
                valid = (code >= 1) & (code <= 129);
                held = (select2(valid, _, select2(code == 129, code, 0))) ~ _;
                target = max(-60.0, min(60.0, select2(held > 0, {{initial}}, held - 1 - {{reference}})));
                offset = ramp(target, {{glide}});
                ramp(t, duration) = advance ~ (_,_,_,_) : _,!,!,! with {
                    changed = t != t';
                    samples = max(1, int(duration * ma.SR));
                    first = 1 - (1 : mem);
                    advance(previous, remaining, start, total) = value, count, origin, span with {
                        span = select2(changed, total, samples);
                        count = select2(first, select2(changed, max(0, remaining - 1), span), 0);
                        origin = select2(first, select2(changed, start, previous), t);
                        value = select2(count <= 1, origin + (t - origin) * float(span - count + 1) / max(1, span), t);
                    };
                };
            }
            """;
    }
}
