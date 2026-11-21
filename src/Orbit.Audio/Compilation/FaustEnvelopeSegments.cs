namespace Orbit.Audio.Compilation;

/// <summary>Sample-rate envelope state and shared power-shaped segment evaluation.</summary>
public static class FaustEnvelopeSegments
{
    // The event sample preserves the previous output; N subsequent samples reach the
    // endpoint. At that endpoint the next stage latches its own controls. Retrigger
    // wins over completion, and every stage has at least one sample.
    public static string EmitAd(string trigger, string attack, string decay, string attackCurve, string decayCurve) => $$"""
        ((step ~ (_, _, _, _, _, _)) : !, !, !, !, !, _) with {
            event = {{trigger}};
            attackSamples = max(1, int(floor(({{attack}}) * ma.SR / 1000.0 + 0.5)));
            decaySamples = max(1, int(floor(({{decay}}) * ma.SR / 1000.0 + 0.5)));
            attackShape = {{attackCurve}};
            decayShape = {{decayCurve}};
            step(stage, elapsed, start, duration, curve, level) = nextStage, nextElapsed, nextStart, nextDuration, nextCurve, nextLevel with {
                tick = elapsed + 1;
                finished = tick >= duration;
                attacking = stage == 1;
                progress = min(1.0, float(tick) / max(1, duration));
                exponent = max(0.25, curve);
                nextStage = select2(event, select2(stage == 0, select2(finished, stage, select2(attacking, 0, 2)), 0), 1);
                nextElapsed = select2(event | finished | (stage == 0), tick, 0);
                nextStart = select2(event, start, level);
                nextDuration = select2(event, select2(attacking & finished, duration, decaySamples), attackSamples);
                nextCurve = select2(event, select2(attacking & finished, curve, decayShape), attackShape);
                rising = select2(finished, start + (1.0 - start) * pow(progress, exponent), 1.0);
                falling = select2(finished, pow(1.0 - progress, exponent), 0.0);
                nextLevel = select2(event, select2(stage == 0, select2(attacking, falling, rising), 0.0), level);
            };
        }
        """;
}
