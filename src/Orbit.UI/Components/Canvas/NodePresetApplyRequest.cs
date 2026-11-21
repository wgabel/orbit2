using Orbit.Application.Presets;

namespace Orbit.UI.Components.Canvas;

public sealed record NodePresetApplyRequest(Guid NodeId, NodePreset Preset);
