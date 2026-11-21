namespace Orbit.Application.Presets;

/// <summary>Independent, atomically replaced custom preset catalog. Null represents no saved catalog.</summary>
public interface INodePresetStorage
{
    Task<string?> LoadCustomAsync();
    Task<bool> ReplaceCustomAsync(string? expectedJson, string json);
    Task DownloadAsync(string name, string json);
}
