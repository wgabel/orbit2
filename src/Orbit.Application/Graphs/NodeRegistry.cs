using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;

namespace Orbit.Application.Graphs;

/// <summary>
/// Registry of node factories keyed by stable node type identifiers. It owns
/// creation, not graph placement, DSP execution or visual representation.
/// </summary>
public sealed class NodeRegistry
{
    private readonly Dictionary<string, (string NodeType, string Label, Func<Guid, ISignalNode> Create)> factories =
        new(StringComparer.Ordinal);

    public IReadOnlyList<string> RegisteredTypes =>
        Array.AsReadOnly(factories.Keys.Order(StringComparer.Ordinal).ToArray());

    public void Register(string typeKey, Func<Guid, ISignalNode> factory, string? label = null) =>
        RegisterFactory(typeKey, typeKey, label ?? typeKey, factory);

    public void RegisterCompositePreset(string presetKey, string label, Func<Guid, CompositeNode> factory) =>
        RegisterFactory(presetKey, CompositeNode.NodeType, label, factory);

    public string Describe(string catalogKey) => factories[catalogKey].Label;

    private void RegisterFactory(string typeKey, string nodeType, string label, Func<Guid, ISignalNode> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeKey);
        ArgumentNullException.ThrowIfNull(factory);

        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!factories.TryAdd(typeKey, (nodeType, label, factory)))
            throw new InvalidOperationException($"Node type '{typeKey}' is already registered.");
    }

    public ISignalNode Create(string typeKey) => Create(typeKey, Guid.NewGuid());

    internal ISignalNode Create(string typeKey, Guid id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeKey);

        if (!factories.TryGetValue(typeKey, out var factory))
            throw new KeyNotFoundException($"Node type '{typeKey}' is not registered.");

        var node = factory.Create(id);

        if (node.Id != id || !string.Equals(node.TypeKey, factory.NodeType, StringComparison.Ordinal))
            throw new InvalidOperationException("The node factory returned an unexpected identity or type.");

        return node;
    }
}
