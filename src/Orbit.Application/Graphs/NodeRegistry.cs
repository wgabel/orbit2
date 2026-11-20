using Orbit.Domain.Nodes;

namespace Orbit.Application.Graphs;

/// <summary>
/// Registry of node factories keyed by stable node type identifiers. It owns
/// creation, not graph placement, DSP execution or visual representation.
/// </summary>
public sealed class NodeRegistry
{
    private readonly Dictionary<string, Func<Guid, ISignalNode>> factories =
        new(StringComparer.Ordinal);

    public IReadOnlyList<string> RegisteredTypes =>
        Array.AsReadOnly(factories.Keys.Order(StringComparer.Ordinal).ToArray());

    public void Register(string typeKey, Func<Guid, ISignalNode> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeKey);
        ArgumentNullException.ThrowIfNull(factory);

        if (!factories.TryAdd(typeKey, factory))
            throw new InvalidOperationException($"Node type '{typeKey}' is already registered.");
    }

    public ISignalNode Create(string typeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeKey);

        if (!factories.TryGetValue(typeKey, out var factory))
            throw new KeyNotFoundException($"Node type '{typeKey}' is not registered.");

        var id = Guid.NewGuid();
        var node = factory(id);

        if (node.Id != id || !string.Equals(node.TypeKey, typeKey, StringComparison.Ordinal))
            throw new InvalidOperationException("The node factory returned an unexpected identity or type.");

        return node;
    }
}
