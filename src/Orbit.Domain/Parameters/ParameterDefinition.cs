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
    /// <summary>Optional discrete values. Choices are non-modulatable and not smoothed.</summary>
    public IReadOnlyList<ParameterChoice>? Choices { get; init; }

    /// <summary>Changing this value changes the compiled DSP, not a running numeric control.</summary>
    public bool RequiresRecompile { get; init; }

    /// <summary>Optional condition controlling whether this parameter participates in DSP.</summary>
    public ParameterActivation? ActiveWhen { get; init; }

    /// <summary>Checks the allowed finite range, including both endpoints.</summary>
    public bool IsValidBaseValue(double value) =>
        double.IsFinite(value) && value >= Minimum && value <= Maximum &&
        (Choices is null || Choices.Any(choice => choice.Value == value));
}
