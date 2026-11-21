using System.Collections.ObjectModel;

namespace Orbit.Application.Presets;

/// <summary>A versioned, independent configuration of a single registered node type.</summary>
public sealed class NodePreset
{
    public const int CurrentVersion = 1;
    public const string CustomGroup = "Custom";

    public NodePreset(int schemaVersion, string id, string nodeType, string name, string group,
        string? description, IReadOnlyDictionary<string, double> parameters)
    {
        if (schemaVersion != CurrentVersion)
            throw new NotSupportedException($"Unsupported node preset schema {schemaVersion}.");
        ValidateText(id, nameof(id));
        ValidateText(nodeType, nameof(nodeType));
        ValidateText(name, nameof(name));
        ValidateText(group, nameof(group));
        if (description is not null) ValidateText(description, nameof(description));
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || !double.IsFinite(pair.Value)))
            throw new ArgumentException("Preset parameter keys and values must be valid and finite.", nameof(parameters));

        SchemaVersion = schemaVersion;
        Id = id;
        NodeType = nodeType;
        Name = name;
        Group = group;
        Description = description;
        Parameters = new ReadOnlyDictionary<string, double>(
            parameters.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    public int SchemaVersion { get; }
    public string Id { get; }
    public string NodeType { get; }
    public string Name { get; }
    public string Group { get; }
    public string? Description { get; }
    public IReadOnlyDictionary<string, double> Parameters { get; }

    private static void ValidateText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Any(char.IsControl))
            throw new ArgumentException("Preset metadata must be nonempty, trimmed and free of control characters.", parameterName);
    }
}
