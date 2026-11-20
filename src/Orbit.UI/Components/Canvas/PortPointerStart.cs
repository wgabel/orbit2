using Microsoft.AspNetCore.Components.Web;
using Orbit.Domain.Ports;

namespace Orbit.UI.Components.Canvas;

/// <summary>DOM pointer start attached to the actual typed port address.</summary>
public sealed record PortPointerStart(PortAddress Address, PointerEventArgs Pointer);
