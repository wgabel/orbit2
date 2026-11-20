using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Connect two typed ports with audio gain or modulation depth.</summary>
public sealed record ConnectNodesCommand(Guid GraphId, PortAddress Source, PortAddress Destination, double Amount = 1) : IGraphCommand;
