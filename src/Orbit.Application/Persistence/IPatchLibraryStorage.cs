namespace Orbit.Application.Persistence;

/// <summary>Named snapshots are separate from the continuously replaced recovery record.</summary>
public interface IPatchLibraryStorage
{
    Task<string[]> ListNamesAsync();
    Task<string?> ReadAsync(string name);
    Task WriteAsync(string name, string json, bool overwrite);
    Task DeleteAsync(string name);
    Task DownloadAsync(string name, string json);
}
