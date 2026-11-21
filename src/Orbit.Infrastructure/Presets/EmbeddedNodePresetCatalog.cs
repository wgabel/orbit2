using Orbit.Application.Presets;

namespace Orbit.Infrastructure.Presets;

/// <summary>Loads the immutable JSON resources shipped in the infrastructure assembly.</summary>
public sealed class EmbeddedNodePresetCatalog : INodePresetCatalog
{
    private const string ResourcePrefix = "Orbit.Infrastructure.Presets.BuiltIn.";

    public IReadOnlyList<NodePreset> LoadBuiltInPresets()
    {
        var assembly = typeof(EmbeddedNodePresetCatalog).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        if (resources.Length == 0)
            throw new InvalidOperationException("No built-in node preset resources were included.");

        var presets = new List<NodePreset>();
        foreach (var name in resources)
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing preset resource '{name}'.");
            presets.AddRange(NodePresetJsonReader.ReadCatalog(stream));
        }
        return presets.AsReadOnly();
    }
}
