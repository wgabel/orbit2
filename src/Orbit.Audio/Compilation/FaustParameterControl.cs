using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation;

/// <summary>Stable mapping from a model parameter to a Faust slider label.</summary>
public sealed record FaustParameterControl(ParameterAddress Address, string Name, double InitialValue);
