namespace Orbit.Application.Presets;

/// <summary>Read-only source of shipped presets; independent of node factory registration.</summary>
public interface INodePresetCatalog
{
    IReadOnlyList<NodePreset> LoadBuiltInPresets();
}
