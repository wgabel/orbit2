using Orbit.Domain.Nodes;

namespace Orbit.UI.Components.Canvas;

public sealed record NodeAuditionRequest(Guid NodeId, NodeAuditionKind Kind, bool Active);
