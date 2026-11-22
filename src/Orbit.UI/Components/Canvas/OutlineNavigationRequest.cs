using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

public sealed record OutlineNavigationRequest(OutlineTarget Target, bool OpenChild = false);
public sealed record OutlineFocusRequest(OutlineTarget Target, Guid Token);
