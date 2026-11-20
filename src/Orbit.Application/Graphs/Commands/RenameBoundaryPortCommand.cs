using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Rename a mirrored socket pair without changing its identity or routes.</summary>
public sealed record RenameBoundaryPortCommand(Guid GraphId, PortDirection Direction, Guid PortId, string Name) : IGraphCommand;
