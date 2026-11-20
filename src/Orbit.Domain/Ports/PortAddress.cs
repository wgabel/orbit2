namespace Orbit.Domain.Ports;

/// <summary>Unambiguously identifies a port inside the owning graph.</summary>
public readonly record struct PortAddress(Guid NodeId, Guid PortId);
