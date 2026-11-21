using System.Text.Json;

namespace Orbit.Application.Presets;

/// <summary>Strict, reflection-free reader for versioned scalar configurations.</summary>
public static class NodePresetJsonReader
{
    public static IReadOnlyList<NodePreset> ReadCatalog(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        return Array.AsReadOnly(document.RootElement.EnumerateArray().Select(ReadPreset).ToArray());
    }

    private static NodePreset ReadPreset(JsonElement element)
    {
        var properties = element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        if (properties.Keys.Any(key => key is not ("schemaVersion" or "id" or "nodeType" or "name" or "group" or "description" or "parameters")))
            throw new JsonException("Unknown node preset property.");

        return new NodePreset(properties["schemaVersion"].GetInt32(),
            RequiredText(properties["id"]), RequiredText(properties["nodeType"]),
            RequiredText(properties["name"]), RequiredText(properties["group"]),
            properties.TryGetValue("description", out var description) ? description.GetString() : null,
            properties["parameters"].EnumerateObject().ToDictionary(property => property.Name,
                property => property.Value.GetDouble(), StringComparer.Ordinal));
    }

    private static string RequiredText(JsonElement element) => element.GetString() ?? throw new JsonException("Missing preset text.");
}
