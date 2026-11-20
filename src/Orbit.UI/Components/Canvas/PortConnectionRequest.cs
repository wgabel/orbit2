using Orbit.Domain.Ports;

namespace Orbit.UI.Components.Canvas;

/// <summary>Completed user routing gesture; the workspace turns it into a graph command.</summary>
public sealed record PortConnectionRequest(PortAddress Source, PortAddress Destination);
