namespace Orbit.Domain.Parameters;

/// <summary>
/// Declares how modulation should be interpreted by a future DSP compiler.
/// Phase 1 models this metadata without calculating real-time samples.
/// </summary>
public enum ParameterScale
{
    Linear,
    Logarithmic,
    PitchCents,
    Decibels
}
