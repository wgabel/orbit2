namespace Orbit.Domain.Parameters;

/// <summary>Estimated lower and upper effective values, not editable base limits.</summary>
public readonly record struct ParameterRange(double Minimum, double Maximum);
