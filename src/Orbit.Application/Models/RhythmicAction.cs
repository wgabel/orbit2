namespace Orbit.Application.Models;

/// <summary>A reachable manual source in a compiled patch, indexed by its input channel.</summary>
public sealed record RhythmicInput(Guid GraphId, Guid NodeId, bool AllowClose);

public enum RhythmicAction { Fire = 1, Close = 2 }
