namespace Orbit.Application.Models;

// Numeric stage identities match the Faust state machine. Progress is actual
// elapsed/latched duration, not a prediction from editable base parameters.
public enum EnvelopeStage { Idle, Attack, Decay, Hold, Release, Rest }

public sealed record EnvelopePlayhead(EnvelopeStage Stage, double Progress)
{
    public static EnvelopePlayhead? Decode(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > 5.5) return null;
        var stage = (int)Math.Floor(value);
        var progress = (value - stage) * 2;
        return progress <= 1 ? new((EnvelopeStage)stage, progress) : null;
    }
}
