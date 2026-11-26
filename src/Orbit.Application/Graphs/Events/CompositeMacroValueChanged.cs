namespace Orbit.Application.Graphs.Events;

/// <summary>A normalized macro base changed; mappings and manual targets are unchanged.</summary>
public sealed record CompositeMacroValueChanged(Guid GraphId, Guid NodeId, Guid MacroId, double PreviousValue, double CurrentValue) : IGraphEvent;
