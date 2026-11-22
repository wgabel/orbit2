using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;

namespace Orbit.Presentation.Canvas;

/// <summary>Eligibility and geometry for graph-local reusable-module selection. Does not edit topology.</summary>
public static class ModuleSelection
{
    public static bool IsEligible(GraphNodeSnapshot node) => node.TypeKey is not
        (InputRack.NodeType or OutputRack.NodeType or MasterOutput.NodeType);

    public static IReadOnlyList<Guid> EligibleIds(GraphSnapshot graph) =>
        graph.Nodes.Where(IsEligible).Select(node => node.Id).ToArray();

    public static CanvasBounds Rectangle(CanvasPoint first, CanvasPoint second) =>
        new(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
            Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));

    public static IReadOnlyList<Guid> HitTest(GraphSnapshot graph, CanvasBounds rectangle,
        IReadOnlyDictionary<Guid, CanvasBounds> measuredBounds) => !rectangle.IsValid ? [] : graph.Nodes
        .Where(node => IsEligible(node) && measuredBounds.TryGetValue(node.Id, out var bounds) && bounds.IsValid &&
            bounds.Left < rectangle.Right && bounds.Right > rectangle.Left &&
            bounds.Top < rectangle.Bottom && bounds.Bottom > rectangle.Top)
        .Select(node => node.Id).ToArray();
}
