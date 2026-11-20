namespace Orbit.Domain.Parameters;

/// <summary>A conditional parameter is active only when another local structural
/// choice has this value. Its stored base value and modulation socket remain intact.</summary>
public sealed record ParameterActivation(string SelectorKey, double EqualsValue);
