namespace Orbit.Application.Graphs.Commands;

/// <summary>Remove an existing connection from the graph.</summary>
public sealed record RemoveConnectionCommand(Guid GraphId, Guid ConnectionId) : IGraphCommand;
