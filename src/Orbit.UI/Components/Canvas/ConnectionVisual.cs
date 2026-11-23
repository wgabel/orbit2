using Orbit.Domain.Ports;

namespace Orbit.UI.Components.Canvas;

/// <summary>Rendered wire data derived from graph endpoints and measured node anchors.</summary>
public sealed record ConnectionVisual(Guid Id, string Path, SignalType Kind, bool Selected, bool Dormant = false, double Amount = 1, string Label = "", string Description = "", double BadgeX = 0, double BadgeY = 0, string AuthoredLabel = "", string RigidPath = "")
{
    public bool Zero => Amount == 0;
}
