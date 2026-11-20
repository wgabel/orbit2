using System.Reflection;

namespace Orbit.Infrastructure;

/// <summary>Both interop adapters must import assets from the same deployed commit.</summary>
internal static class BrowserModulePaths
{
    private static readonly string? Version = typeof(BrowserModulePaths).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "OrbitBrowserAssetVersion")?.Value;

    public static string GetPath(string directory, string file) =>
        $"./_content/Orbit.Infrastructure/{(string.IsNullOrEmpty(Version) ? directory : $"{directory}-{Version}")}/{file}";
}
