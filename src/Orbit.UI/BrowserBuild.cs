using System.Reflection;

namespace Orbit.UI;

/// <summary>Identity of the running editor, independent of patch and audio state.</summary>
public static class BrowserBuild
{
    private static string? Metadata(string key) => typeof(BrowserBuild).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == key)?.Value;

    public static string Commit { get; } = Metadata("OrbitBrowserAssetVersion") ?? "local";
    public static string Number { get; } = Metadata("OrbitBuildNumber") is { Length: > 0 } number ? number : "local";
    public static string Label => Number == "local" ? "dev" : $"b{Number}";
    public static string Description => Number == "local" ? "Local development build" :
        $"Build {Number} · commit {Commit}";

    public static string ModulePath(string file) =>
        Commit == "local" ? $"./js/{file}" : $"./js-{Commit}/{file}";
}
