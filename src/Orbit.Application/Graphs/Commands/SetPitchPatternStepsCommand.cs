namespace Orbit.Application.Graphs.Commands;

/// <summary>Replace all 64 slots in one validated, undoable graph edit.</summary>
public sealed record SetPitchPatternStepsCommand(Guid GraphId, Guid NodeId, IReadOnlyList<int> Steps) : IGraphCommand;
