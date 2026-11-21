namespace Orbit.Application.Graphs.Commands;

public sealed record SetNodeBypassedCommand(Guid GraphId, Guid NodeId, bool Bypassed) : IGraphCommand;
