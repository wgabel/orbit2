namespace Orbit.Application.Graphs.Commands;

/// <summary>Edit a single persistent macro's normalized base value.</summary>
public sealed record SetCompositeMacroValueCommand(Guid GraphId, Guid NodeId, Guid MacroId, double Value) : IGraphCommand;
