using Orbit.Domain.Parameters;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Change an editable base value, never an effective DSP sample.</summary>
public sealed record SetParameterValueCommand(Guid GraphId, ParameterAddress Address, double Value) : IGraphCommand;
