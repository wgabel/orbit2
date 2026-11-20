namespace Orbit.Application.Graphs.Commands;

/// <summary>Remove a node and all of its incident connections atomically.</summary>
public sealed record RemoveNodeCommand(Guid GraphId, Guid NodeId) : IGraphCommand;
