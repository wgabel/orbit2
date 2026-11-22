using System.Collections.ObjectModel;
using Orbit.Application.Persistence;

namespace Orbit.Presentation.Canvas;

public sealed partial class CanvasStateService
{
    private readonly Dictionary<Guid, SavedNodeDescription> removedNodeDescriptions = [];
    private readonly Dictionary<Guid, SavedConnectionDescription> removedConnectionDescriptions = [];

    public CanvasChangeResult SetNodeDescription(SavedNodeDescription description)
    {
        EnsureEditable();
        DescriptionLimits.Validate(description);
        description = description with { Alias = description.Alias.Trim() };
        var id = description.NodeId;
        _ = snapshot.Graph.GetNode(id);
        var before = snapshot.NodeDescriptions.GetValueOrDefault(id);
        var after = description.IsEmpty ? null : description;
        if (before == after) return NoChange();
        return CommitAnnotation(new AnnotationEdit(CanvasChangeKind.DescriptionsChanged, forward => snapshot with
        {
            NodeDescriptions = ApplyDescription(snapshot.NodeDescriptions, removedNodeDescriptions, id,
                forward ? after : before, snapshot.Graph.Nodes.Any(node => node.Id == id))
        }));
    }

    public CanvasChangeResult SetConnectionDescription(SavedConnectionDescription description)
    {
        EnsureEditable();
        DescriptionLimits.Validate(description);
        description = description with { Label = description.Label.Trim() };
        var id = description.ConnectionId;
        if (!snapshot.Graph.Connections.Any(edge => edge.Id == id)) throw new KeyNotFoundException("Connection not found.");
        var before = snapshot.ConnectionDescriptions.GetValueOrDefault(id);
        var after = description.IsEmpty ? null : description;
        if (before == after) return NoChange();
        return CommitAnnotation(new AnnotationEdit(CanvasChangeKind.DescriptionsChanged, forward => snapshot with
        {
            ConnectionDescriptions = ApplyDescription(snapshot.ConnectionDescriptions, removedConnectionDescriptions, id,
                forward ? after : before, snapshot.Graph.Connections.Any(edge => edge.Id == id))
        }));
    }

    private static IReadOnlyDictionary<Guid, T> ApplyDescription<T>(IReadOnlyDictionary<Guid, T> active,
        Dictionary<Guid, T> removed, Guid id, T? value, bool exists) where T : class
    {
        if (!exists)
        {
            if (value is null) removed.Remove(id);
            else removed[id] = value;
            TrimRemoved(removed);
            return active;
        }
        var next = new Dictionary<Guid, T>(active);
        if (value is null) next.Remove(id);
        else next[id] = value;
        return FreezeDescriptions(next);
    }

    private static IReadOnlyDictionary<Guid, T> ReconcileDescriptions<T>(IReadOnlyDictionary<Guid, T> active,
        Dictionary<Guid, T> removed, HashSet<Guid> ids) where T : class
    {
        if (active.Keys.All(ids.Contains) && !removed.Keys.Any(ids.Contains)) return active;
        var next = new Dictionary<Guid, T>(active);
        foreach (var (id, value) in active)
            if (!ids.Contains(id))
            {
                next.Remove(id);
                removed[id] = value;
            }
        foreach (var id in ids)
            if (removed.Remove(id, out var value)) next[id] = value;
        TrimRemoved(removed);
        return FreezeDescriptions(next);
    }

    private static void TrimRemoved<T>(Dictionary<Guid, T> removed)
    {
        while (removed.Count > 512) removed.Remove(removed.Keys.First());
    }

    private static IReadOnlyDictionary<Guid, T> FreezeDescriptions<T>(Dictionary<Guid, T> values) =>
        new ReadOnlyDictionary<Guid, T>(values);
}
