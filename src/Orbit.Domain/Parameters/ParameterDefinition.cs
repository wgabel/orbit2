namespace Orbit.Domain.Parameters;

/// <summary>
/// Immutable description shared by parameters of the same kind. Limits refer
/// to base values; applying modulation and clamping the result belongs to DSP.
/// </summary>
public sealed record ParameterDefinition(
    string Key,
    string Name,
    double DefaultValue,
    double Minimum,
    double Maximum,
    ParameterUnit Unit,
    ParameterScale Scale,
    bool CanModulate)
{
    /// <summary>Checks the allowed finite range, including both endpoints.</summary>
    public bool IsValidBaseValue(double value) =>
        double.IsFinite(value) && value >= Minimum && value <= Maximum;
}
