namespace Orbit.Application.Presets;

public sealed record NodePresetGroup(string Name, IReadOnlyList<NodePreset> Presets);
