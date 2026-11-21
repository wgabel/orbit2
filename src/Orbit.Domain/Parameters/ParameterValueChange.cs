namespace Orbit.Domain.Parameters;

/// <summary>A changed base value returned by a validated graph operation.</summary>
public sealed record ParameterValueChange(ParameterAddress Address, double PreviousValue, double CurrentValue);
