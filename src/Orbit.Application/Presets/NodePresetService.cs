namespace Orbit.Application.Presets;

/// <summary>Validates shipped data once and serves deterministic, immutable usage groups.</summary>
public sealed class NodePresetService
{
    private readonly NodePresetValidator validator;
    private readonly Dictionary<string, NodePreset> byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<NodePresetGroup>> byType = new(StringComparer.Ordinal);
    private static readonly IReadOnlyList<NodePresetGroup> EmptyCustom = Array.AsReadOnly(
        new[] { new NodePresetGroup(NodePreset.CustomGroup, Array.Empty<NodePreset>()) });

    public NodePresetService(INodePresetCatalog catalog, NodePresetValidator validator)
    {
        this.validator = validator;
        foreach (var preset in catalog.LoadBuiltInPresets())
        {
            validator.Validate(preset);
            if (string.Equals(preset.Group, NodePreset.CustomGroup, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The Custom group is reserved for user presets.", nameof(catalog));
            if (!byId.TryAdd(preset.Id, preset))
                throw new ArgumentException($"Duplicate preset ID '{preset.Id}'.", nameof(catalog));
        }

        foreach (var type in byId.Values.GroupBy(preset => preset.NodeType, StringComparer.Ordinal))
        {
            var groups = type.GroupBy(preset => preset.Group, StringComparer.OrdinalIgnoreCase)
                .Select(group => new NodePresetGroup(group.Select(preset => preset.Group).Order(StringComparer.Ordinal).First(),
                    Array.AsReadOnly(group.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(preset => preset.Id, StringComparer.Ordinal).ToArray())))
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .Concat(EmptyCustom).ToArray();
            byType.Add(type.Key, Array.AsReadOnly(groups));
        }
    }

    public bool Supports(string nodeType) => validator.Supports(nodeType);

    public IReadOnlyList<NodePresetGroup> GetGroups(string nodeType) =>
        byType.TryGetValue(nodeType, out var groups) ? groups : Supports(nodeType) ? EmptyCustom : Array.Empty<NodePresetGroup>();

    public NodePreset GetPreset(string id) => byId[id];
}
