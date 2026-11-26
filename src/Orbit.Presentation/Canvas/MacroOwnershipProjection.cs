using System.Collections.ObjectModel;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Parameters;

namespace Orbit.Presentation.Canvas;

public sealed record MacroOwnership(ParameterAddress Address, string ParameterName, ParameterUnit Unit,
    Guid MacroId, string MacroName, double ManualBase, double MappedBase, bool IsActive)
{
    public double EffectiveBase => IsActive ? MappedBase : ManualBase;
}

/// <summary>Authored ownership of the active canvas only. Equal node-local readouts
/// retain their reference across unrelated edits; telemetry/layout never rebuilds them.</summary>
public sealed class MacroOwnershipProjection
{
    public static IReadOnlyDictionary<Guid, IReadOnlyList<MacroOwnership>> Empty { get; } =
        new ReadOnlyDictionary<Guid, IReadOnlyList<MacroOwnership>>(new Dictionary<Guid, IReadOnlyList<MacroOwnership>>());
    private GraphSnapshot? previousRoot;
    private Guid previousGraph;
    private Guid? previousParent;
    private IReadOnlyDictionary<Guid, IReadOnlyList<MacroOwnership>> current = Empty;

    public IReadOnlyDictionary<Guid, IReadOnlyList<MacroOwnership>> Update(GraphSnapshot root, Guid graphId, Guid? parentGraphId)
    {
        if (ReferenceEquals(previousRoot, root) && previousGraph == graphId && previousParent == parentGraphId)
            return current;

        var prior = previousGraph == graphId && previousParent == parentGraphId ? current : Empty;
        previousRoot = root;
        previousGraph = graphId;
        previousParent = parentGraphId;
        var owner = parentGraphId is Guid parent
            ? root.FindGraph(parent)?.Nodes.FirstOrDefault(node => node.Composite?.Graph.Id == graphId)?.Composite
            : null;
        if (owner is null || owner.Macros.Count == 0) return current = Empty;

        var targets = owner.Graph.Nodes.ToDictionary(node => node.Id);
        var rows = owner.Macros.SelectMany(macro => macro.ResolveBaseValues().Select(mapped =>
        {
            var node = targets[mapped.Target.NodeId];
            var parameter = node.Parameters.Single(p => p.Id == mapped.Target.ParameterId);
            return new MacroOwnership(mapped.Target, parameter.Definition.Name, parameter.Definition.Unit,
                macro.Id, macro.Name, parameter.BaseValue, mapped.Value, node.IsParameterActive(parameter));
        }));
        var next = new Dictionary<Guid, IReadOnlyList<MacroOwnership>>();
        foreach (var group in rows.GroupBy(row => row.Address.NodeId))
        {
            var values = group.ToArray();
            next.Add(group.Key, prior.TryGetValue(group.Key, out var old) && old.SequenceEqual(values)
                ? old : Array.AsReadOnly(values));
        }
        return current = new ReadOnlyDictionary<Guid, IReadOnlyList<MacroOwnership>>(next);
    }
}
