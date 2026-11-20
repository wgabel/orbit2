using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Remove a mirrored socket pair and every incident edge on both sides.</summary>
public sealed record RemoveBoundaryPortCommand(Guid GraphId, PortDirection Direction, Guid PortId) : IGraphCommand;
