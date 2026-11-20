namespace Orbit.Domain.Parameters;

/// <summary>One named, exact value of a discrete parameter.</summary>
public sealed record ParameterChoice(double Value, string Label);
