using Orbit.Domain.Parameters;

namespace Orbit.Application.Graphs.Events;

/// <summary>A base parameter value was committed, not an instantaneous modulated value.</summary>
public sealed record ParameterChanged(Guid GraphId, ParameterAddress Address, double PreviousValue, double CurrentValue) : IGraphEvent;
