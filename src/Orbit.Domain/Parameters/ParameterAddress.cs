namespace Orbit.Domain.Parameters;

/// <summary>Identifies a parameter instance inside its node and owning graph.</summary>
public readonly record struct ParameterAddress(Guid NodeId, Guid ParameterId);
