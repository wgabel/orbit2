namespace Orbit.Application.Models;

/// <summary>Stable identity of a macro within its owning graph and composite.</summary>
public readonly record struct GraphMacroAddress(Guid GraphId, Guid CompositeId, Guid MacroId);
