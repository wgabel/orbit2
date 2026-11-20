using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation;

/// <summary>Stable mapping from a parameter to its base control and optional passive effective-value meter.</summary>
public sealed record FaustParameterControl(ParameterAddress Address, string Name, double InitialValue, string? MeterName = null);
