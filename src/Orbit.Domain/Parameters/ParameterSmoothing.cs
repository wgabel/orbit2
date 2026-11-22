namespace Orbit.Domain.Parameters;

/// <summary>Semantic base-control smoothing. This is not a modulation filter,
/// envelope, performance glide, or a persisted user setting.</summary>
public enum ParameterSmoothing
{
    Unspecified,
    None,
    Pitch,
    Amplitude,
    Phase,
    Filter,
    EffectMix,
    Rate,
    Shape,
    DelayTime,
    Feedback,
    Decay,
    Timing
}
