namespace Orbit.Application.Graphs.Commands;

/// <summary>Expand exactly one composite level using its current boundary routing.</summary>
public sealed record UngroupCompositeCommand(Guid GraphId, Guid CompositeId, long ExpectedRevision) : IGraphCommand;
