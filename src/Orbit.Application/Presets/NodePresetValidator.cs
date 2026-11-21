using Orbit.Application.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;

namespace Orbit.Application.Presets;

/// <summary>Uses the registered node definitions as the sole parameter compatibility authority.</summary>
public sealed class NodePresetValidator
{
    private readonly Dictionary<string, IReadOnlyList<ParameterDefinition>> definitions = new(StringComparer.Ordinal);

    public NodePresetValidator(NodeRegistry registry)
    {
        foreach (var key in registry.RegisteredTypes)
        {
            var node = registry.Create(key);
            if (node.TypeKey == key && node is not ICompositeNode &&
                (node is IGenerator or IProcessor) && node.Parameters.Count > 0)
                definitions.Add(key, Array.AsReadOnly(node.Parameters.Select(parameter => parameter.Definition).ToArray()));
        }
    }

    public bool Supports(string nodeType) => definitions.ContainsKey(nodeType);

    public void Validate(NodePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (!definitions.TryGetValue(preset.NodeType, out var parameters))
            throw new ArgumentException($"Node type '{preset.NodeType}' does not support individual presets.", nameof(preset));
        if (preset.Parameters.Count != parameters.Count)
            throw new ArgumentException($"Preset '{preset.Id}' must contain all parameter keys.", nameof(preset));
        foreach (var definition in parameters)
            if (!preset.Parameters.TryGetValue(definition.Key, out var value) || !definition.IsValidBaseValue(value))
                throw new ArgumentException($"Preset '{preset.Id}' has a missing or invalid '{definition.Key}' value.", nameof(preset));
    }
}
