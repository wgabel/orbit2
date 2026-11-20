using Orbit.Domain.Ports;

namespace Orbit.Domain.Graphs;

/// <summary>
/// An immutable graph edge. Amount is a gain for audio routing or a modulation
/// depth interpreted according to the target parameter's scale and unit.
/// </summary>
public sealed record SignalConnection(
    Guid Id,
    PortAddress Source,
    PortAddress Destination,
    double Amount = 1);
