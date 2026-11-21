using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Presets;

/// <summary>Validates shipped data once and serves deterministic, immutable usage groups.</summary>
public sealed class NodePresetService
{
    private readonly NodePresetValidator validator;
    private readonly Dictionary<string, NodePreset> byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<NodePresetGroup>> byType = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, NodePreset> custom = new Dictionary<string, NodePreset>(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<NodePresetGroup>> mergedGroups = new(StringComparer.Ordinal);
    public long Revision { get; private set; }
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
                .ToArray();
            byType.Add(type.Key, Array.AsReadOnly(groups));
        }
    }

    public bool Supports(string nodeType) => validator.Supports(nodeType);

    public IReadOnlyList<NodePresetGroup> GetGroups(string nodeType)
    {
        if (!Supports(nodeType)) return Array.Empty<NodePresetGroup>();
        if (mergedGroups.TryGetValue(nodeType, out var cached)) return cached;
        var presets = custom.Values.Where(preset => preset.NodeType == nodeType)
            .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase).ThenBy(preset => preset.Id, StringComparer.Ordinal).ToArray();
        var groups = byType.GetValueOrDefault(nodeType) ?? [];
        var merged = Array.AsReadOnly(groups.Concat(presets.Length == 0 ? EmptyCustom :
            [new NodePresetGroup(NodePreset.CustomGroup, Array.AsReadOnly(presets))]).ToArray());
        mergedGroups.Add(nodeType, merged);
        return merged;
    }

    public NodePreset GetPreset(string id) => byId.TryGetValue(id, out var builtIn) ? builtIn : custom[id];

    public IReadOnlyList<NodePreset> GetCustomPresets(string nodeType) => GetGroups(nodeType).LastOrDefault()?.Presets ?? [];

    internal IReadOnlyList<NodePreset> CustomPresets => Array.AsReadOnly(custom.Values.ToArray());

    internal void ValidateCustomPresets(IReadOnlyList<NodePreset> presets)
    {
        var ids = new HashSet<string>(byId.Keys, StringComparer.Ordinal);
        var names = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var preset in byId.Values.Concat(presets))
        {
            if (!names.TryGetValue(preset.NodeType, out var typeNames))
                names.Add(preset.NodeType, typeNames = new(StringComparer.OrdinalIgnoreCase));
            if (!typeNames.Add(preset.Name)) throw new ArgumentException($"A preset named '{preset.Name}' already exists for this node type.");
        }
        foreach (var preset in presets)
        {
            ValidateCustomPreset(preset);
            if (!ids.Add(preset.Id)) throw new ArgumentException($"Duplicate preset ID '{preset.Id}'.");
        }
    }

    internal void ValidateCustomPreset(NodePreset preset)
    {
        validator.Validate(preset);
        if (preset.Group != NodePreset.CustomGroup || !Guid.TryParseExact(preset.Id, "D", out var id) ||
            id == Guid.Empty || id.ToString("D") != preset.Id || byId.ContainsKey(preset.Id))
            throw new ArgumentException("Invalid custom preset identity or group.");
        if (preset.Name.Length > 80) throw new ArgumentException("Enter a preset name of 1–80 characters.");
    }

    internal void ReplaceCustomPresets(IReadOnlyList<NodePreset> presets)
    {
        ValidateCustomPresets(presets);
        custom = presets.ToDictionary(preset => preset.Id, StringComparer.Ordinal);
        mergedGroups.Clear();
        Revision++;
    }

    /// <summary>Resolve an exact configuration match using stored base values, including inactive values.</summary>
    public NodePreset? FindMatchingPreset(GraphNodeSnapshot node) => GetGroups(node.TypeKey)
        .SelectMany(group => group.Presets)
        .FirstOrDefault(preset => preset.Parameters.Count == node.Parameters.Count &&
            node.Parameters.All(parameter => preset.Parameters.TryGetValue(parameter.Definition.Key, out var value) &&
                value == parameter.BaseValue));
}
