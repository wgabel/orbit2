using System.Text;
using System.Text.Json;

namespace Orbit.Application.Presets;

/// <summary>Writes the same versioned configuration schema accepted by the strict catalog reader.</summary>
public static class NodePresetJsonWriter
{
    public static string WriteCatalog(IEnumerable<NodePreset> presets) => Write(writer =>
        {
            writer.WriteStartArray();
            foreach (var preset in presets.OrderBy(preset => preset.Id, StringComparer.Ordinal))
                WritePreset(writer, preset);
            writer.WriteEndArray();
        });

    public static string WritePortable(NodePreset preset) => Write(writer => WritePreset(writer, preset));

    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WritePreset(Utf8JsonWriter writer, NodePreset preset)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", preset.SchemaVersion);
        writer.WriteString("id", preset.Id);
        writer.WriteString("nodeType", preset.NodeType);
        writer.WriteString("name", preset.Name);
        writer.WriteString("group", preset.Group);
        if (preset.Description is not null) writer.WriteString("description", preset.Description);
        writer.WriteStartObject("parameters");
        foreach (var pair in preset.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            writer.WriteNumber(pair.Key, pair.Value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
