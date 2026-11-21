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

    public static NodePreset ReadPortable(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadPreset(document.RootElement);
    }

    private static NodePreset ReadPreset(JsonElement element)
    {
        var properties = element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        if (properties.Keys.Any(key => key is not ("schemaVersion" or "id" or "nodeType" or "name" or "group" or "description" or "parameters")))
            throw new JsonException("Unknown node preset property.");

        JsonElement Required(string key) => properties.TryGetValue(key, out var value)
            ? value : throw new JsonException($"Missing node preset property '{key}'.");

        return new NodePreset(Required("schemaVersion").GetInt32(),
            RequiredText(Required("id")), RequiredText(Required("nodeType")),
            RequiredText(Required("name")), RequiredText(Required("group")),
            properties.TryGetValue("description", out var description) ? description.GetString() : null,
            Required("parameters").EnumerateObject().ToDictionary(property => property.Name,
                property => property.Value.GetDouble(), StringComparer.Ordinal));
    }

    private static string RequiredText(JsonElement element) => element.GetString() ?? throw new JsonException("Missing preset text.");
}
