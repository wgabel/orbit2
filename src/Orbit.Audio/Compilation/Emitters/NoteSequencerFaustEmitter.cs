using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation.Emitters;

/// <summary>
/// One sample-coherent control tuple. The existing pitch latch remains continuous;
/// optional articulated notes use dedicated gate, one-sample trigger and velocity.
/// </summary>
public sealed class NoteSequencerFaustEmitter : IFaustMultiOutputEmitter
{
    public string TypeKey => NoteSequencerGenerator.NodeType;
    public ParameterRange? EstimateOutputRange(SignalRangeContext context, string portKey) =>
        portKey == "pitch-out" ? new(-NoteSequencerGenerator.MaximumOffset, NoteSequencerGenerator.MaximumOffset) : new(0, 1);

    public string Emit(FaustNodeContext context)
    {
        var action = context.PerformancePitchInput();
        var reference = context.Parameter(NoteSequencerGenerator.ReferenceKey);
        var initial = context.Parameter(NoteSequencerGenerator.InitialPitchKey, initializeAtTarget: true);
        var glide = context.Parameter(NoteSequencerGenerator.GlideKey);
        var notes = context.Parameter(NoteSequencerGenerator.ArticulationKey);
        var velocity = context.Parameter(NoteSequencerGenerator.VelocityKey);
        var meter = context.PitchMeter();
        var stepMeter = meter.Replace("pitch_", "step_", StringComparison.Ordinal);
        return $$"""
            (measured, attach(gate, measured), attach(trigger, measured), attach(velocityOut, measured)) with {
                action = {{action}};
                code = int(action) % 256;
                stepCode = int(int(action) / 256);
                step = (select2(stepCode > 0, _, select2(stepCode <= 64, 0, stepCode))) ~ _;
                valid = (code >= 1) & (code <= 128);
                release = (code == 129) | (code == 130);
                held = (select2(valid | (code == 129), _, select2(code == 129, code, select2(valid, code, 0)))) ~ _;
                target = max(-60.0, min(60.0, select2(held > 0, {{initial}}, held - 1 - {{reference}})));
                offset = ramp(target, {{glide}});
                measured = offset : hbargraph("{{meter}}", -60, 60);
                gateState = (select2(valid | release, _, valid)) ~ _;
                gate = gateState * {{notes}};
                trigger = valid * {{notes}};
                velocityOut = gateState * {{velocity}} * {{notes}};
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
