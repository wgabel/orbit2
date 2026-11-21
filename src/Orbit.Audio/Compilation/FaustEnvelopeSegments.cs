using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Compilation;

/// <summary>One sample-rate state machine for triggered, gated and looping curves.</summary>
public static class FaustEnvelopeSegments
{
    public static string EmitAd(string trigger, string attack, string decay, string attackCurve, string decayCurve) =>
        Emit(EnvelopeMode.Ad, trigger, attack, decay, attackCurve, decayCurve);

    public static string Emit(EnvelopeMode mode, string signal, string attack, string decay, string attackCurve, string decayCurve,
        string release = "1.0", string releaseCurve = "1.0", string sustain = "0.0", string rest = "0.0", string? meter = null) => $$"""
        ((step ~ (_, _, _, _, _, _, _)) : {{(meter is null ? "!, !, !, !, !, !, _" : "report")}}) with {
            {{(meter is null ? "" : $"report(stage, elapsed, start, target, duration, curve, level) = attach(level, (float(stage) + 0.5 * min(1.0, float(elapsed) / max(1, duration))) : hbargraph(\"{meter}\", 0, 5.5));")}}
            mode = {{(int)mode}};
            isAR = mode == 1;
            isADSR = mode == 2;
            gated = isAR | isADSR;
            looping = mode == 3;
            gate = ({{signal}}) > 0.5;
            rise = gate & (1 - (gate : mem));
            fall = (1 - gate) & (gate : mem);
            attackSamples = max(1, int(floor(({{attack}}) * ma.SR / 1000.0 + 0.5)));
            decaySamples = max(1, int(floor(({{decay}}) * ma.SR / 1000.0 + 0.5)));
            releaseSamples = max(1, int(floor(({{release}}) * ma.SR / 1000.0 + 0.5)));
            restSamples = max(0, int(floor(({{rest}}) * ma.SR / 1000.0 + 0.5)));
            heldTarget = select2(isAR, {{sustain}}, 1.0);
            slew = 1.0 / max(1.0, 0.01 * ma.SR);
            // Stages: idle 0, attack 1, decay 2, hold 3, release 4, rest 5.
            step(stage, elapsed, start, target, duration, curve, level) = nextStage, nextElapsed, nextStart, nextTarget, nextDuration, nextCurve, nextLevel with {
                timed = (stage == 1) | (stage == 2) | (stage == 4) | (stage == 5);
                tick = elapsed + 1;
                finished = timed & (tick >= duration);
                progress = min(1.0, float(tick) / max(1, duration));
                exponent = max(0.25, curve);
                rising = start + (target - start) * pow(progress, exponent);
                falling = target + (start - target) * pow(1.0 - progress, exponent);
                segment = select2(finished, select2(stage == 1, falling, rising), target);
                held = level + max(-slew, min(slew, heldTarget - level));
                ordinary = select2(timed, select2(stage == 3, 0.0, held), segment);

                eventStart = ((mode == 0) & gate) | (gated & rise) | (looping & (stage == 0));
                eventRelease = gated & fall & (stage != 0) & (stage != 4);
                cycleStart = looping & finished & (((stage == 2) & (restSamples == 0)) | (stage == 5));
                toDecay = finished & (stage == 1) & (1 - isAR);
                toHold = finished & (((stage == 1) & isAR) | ((stage == 2) & isADSR));
                toRest = finished & looping & (stage == 2) & (restSamples > 0);
                toIdle = finished & (((stage == 2) & (1 - isADSR) & (1 - looping)) | (stage == 4));
                automatic = select2(cycleStart, select2(toDecay, select2(toHold, select2(toRest, select2(toIdle, stage, 0), 5), 3), 2), 1);
                nextStage = select2(eventStart, select2(eventRelease, automatic, 4), 1);
                entering = eventStart | eventRelease | cycleStart | toDecay | toHold | toRest | toIdle;
                // Events preserve the instantaneous level and beat completion.
                // Automatic transitions latch controls on the endpoint sample.
                nextLevel = select2(eventStart | eventRelease, ordinary, level);
                nextElapsed = select2(entering | (1 - timed), tick, 0);
                nextStart = select2(entering, start, nextLevel);
                entryTarget = select2(nextStage == 1, select2((nextStage == 2) & isADSR, 0.0, {{sustain}}), 1.0);
                nextTarget = select2(entering, target, entryTarget);
                entryDuration = select2(nextStage == 1, select2(nextStage == 2, select2(nextStage == 4, restSamples, releaseSamples), decaySamples), attackSamples);
                nextDuration = select2(entering, duration, entryDuration);
                entryCurve = select2(nextStage == 1, select2(nextStage == 2, select2(nextStage == 4, 1.0, {{releaseCurve}}), {{decayCurve}}), {{attackCurve}});
                nextCurve = select2(entering, curve, entryCurve);
            };
        }
        """;
}
