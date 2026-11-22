using Orbit.Application.Graphs;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;

namespace Orbit.Presentation.Canvas;

/// <summary>Preflight layout and annotation transfer before the structural command commits.</summary>
internal sealed record CompositeExpansionLayout(Guid WrapperId, IReadOnlyDictionary<Guid, CanvasPoint> Positions,
    IReadOnlyDictionary<Guid, SavedNodeDescription> NodeDescriptions,
    IReadOnlyDictionary<Guid, SavedConnectionDescription> ConnectionDescriptions,
    IReadOnlyDictionary<Guid, NodeVisualizationSettings> Visualizations, IReadOnlyList<Guid> MovedConnectionIds)
{
    public static CompositeExpansionLayout Create(CanvasStateSnapshot parent, CanvasStateSnapshot child, CompositeUngrouping.Plan plan)
    {
        var wrapper = plan.Wrapper;
        var moved = plan.NodeIds.ToHashSet();
        var movedEdges = plan.MovedConnectionIds.ToHashSet();
        var retainedEdges = plan.Connections.Select(edge => edge.Id).ToHashSet();
        // Notes and groups have their own history, membership and bounded graph-local
        // identities. Refuse rather than silently discard or split them.
        if (child.Notes.Count > 0 || child.VisualGroups.Count > 0)
            throw new GraphValidationException("Move or remove the child canvas notes and visual groups before expanding the composite.");
        if (parent.VisualGroups.Any(group => group.NodeIds.Contains(wrapper.Id)))
            throw new GraphValidationException("Remove the composite from its visual group before expanding it.");
        if (parent.NodeDescriptions.ContainsKey(wrapper.Id) || child.NodeDescriptions.Keys.Any(id => !moved.Contains(id)))
            throw new GraphValidationException("Move composite and rack descriptions to internal nodes before expanding.");
        if (parent.ConnectionDescriptions.Keys.Any(id => !retainedEdges.Contains(id)) ||
            child.ConnectionDescriptions.Keys.Any(id => !movedEdges.Contains(id)))
            throw new GraphValidationException("A rack cable label would be lost. Move it to the receiving cable before expanding.");

        var anchor = CanvasNavigationService.GroupAnchor(child, moved);
        var target = parent.NodePositions[wrapper.Id];
        var positions = child.NodePositions.Where(pair => moved.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Subtract(anchor).Add(target).RequireFinite("Ungroup placement"));
        return new CompositeExpansionLayout(wrapper.Id, positions,
            child.NodeDescriptions.Where(pair => moved.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value),
            child.ConnectionDescriptions.Where(pair => movedEdges.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value),
            child.Visualizations.Where(pair => moved.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value),
            plan.MovedConnectionIds);
    }
}
