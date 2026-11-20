using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Move a mirrored socket pair to a zero-based index in its direction.</summary>
public sealed record MoveBoundaryPortCommand(Guid GraphId, PortDirection Direction, Guid PortId, int Index) : IGraphCommand;
