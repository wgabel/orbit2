namespace Orbit.Application.Graphs.Commands;

public sealed record SetNodeMutedCommand(Guid GraphId, Guid NodeId, bool Muted) : IGraphCommand;
