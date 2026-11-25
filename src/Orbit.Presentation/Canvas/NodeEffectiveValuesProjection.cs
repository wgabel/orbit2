using System.Collections.ObjectModel;
using Orbit.Domain.Parameters;

namespace Orbit.Presentation.Canvas;

/// <summary>Presentation-only effective values partitioned by owning node.
/// Input frames are immutable after publication. Equal values retain their
/// dictionary identity; absence clears prior values rather than freezing them.</summary>
public sealed class NodeEffectiveValuesProjection
{
    private static readonly IReadOnlyDictionary<ParameterAddress, double> Empty =
        new ReadOnlyDictionary<ParameterAddress, double>(new Dictionary<ParameterAddress, double>());
    private IReadOnlyDictionary<ParameterAddress, double>? previous;
    private Dictionary<Guid, IReadOnlyDictionary<ParameterAddress, double>> nodes = [];

    public void Update(IReadOnlyDictionary<ParameterAddress, double> values)
    {
        if (ReferenceEquals(previous, values)) return;
        var next = new Dictionary<Guid, IReadOnlyDictionary<ParameterAddress, double>>();
        foreach (var group in values.GroupBy(pair => pair.Key.NodeId))
        {
            if (nodes.TryGetValue(group.Key, out var old) && EqualValues(old, group))
                next.Add(group.Key, old);
            else
                next.Add(group.Key, new ReadOnlyDictionary<ParameterAddress, double>(
                    group.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }
        nodes = next;
        previous = values;
    }

    // Folded frames receive the same immutable empty dictionary on every tick.
    // Keep the latest active-canvas projection for immediate rebind on expansion.
    public IReadOnlyDictionary<ParameterAddress, double> ForNode(Guid nodeId, bool consumesTelemetry = true) =>
        consumesTelemetry ? nodes.GetValueOrDefault(nodeId, Empty) : Empty;

    public void Reset()
    {
        previous = null;
        nodes.Clear();
    }

    private static bool EqualValues(IReadOnlyDictionary<ParameterAddress, double> old,
        IEnumerable<KeyValuePair<ParameterAddress, double>> current)
    {
        var count = 0;
        foreach (var pair in current)
        {
            if (!old.TryGetValue(pair.Key, out var value) || !value.Equals(pair.Value)) return false;
            count++;
        }
        return count == old.Count;
    }
}
