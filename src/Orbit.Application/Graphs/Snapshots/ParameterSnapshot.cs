using Orbit.Domain.Parameters;

namespace Orbit.Application.Graphs.Snapshots;

/// <summary>Immutable parameter state captured at a committed graph revision.</summary>
public sealed record ParameterSnapshot(
    Guid Id, ParameterDefinition Definition, double BaseValue, Guid? ModulationPortId)
{
    internal static ParameterSnapshot Capture(Parameter parameter) =>
        new(parameter.Id, parameter.Definition, parameter.BaseValue, parameter.ModulationPortId);
}
