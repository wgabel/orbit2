namespace Orbit.Application.Graphs.Commands;

/// <summary>Update one local connection coefficient without disconnecting its sockets.</summary>
public sealed record SetConnectionAmountCommand(Guid GraphId, Guid ConnectionId, double Amount) : IGraphCommand;
