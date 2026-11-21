using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using System.Collections.ObjectModel;

namespace Orbit.Presentation.Canvas;

/// <summary>Immutable canvas projection. Graph and layout revisions are independent.
/// A previously published snapshot is unaffected by later edits or navigation.</summary>
public sealed record CanvasStateSnapshot(
    GraphSnapshot Graph,
    long LayoutRevision,
    CanvasViewport Viewport,
    IReadOnlyDictionary<Guid, CanvasPoint> NodePositions,
    IReadOnlyList<Guid> SelectedNodeIds,
    IReadOnlyList<Guid> SelectedConnectionIds)
{
    public ConnectionLabelMode ConnectionLabels { get; init; } = ConnectionLabelMode.All;
    public Guid GraphId => Graph.Id;
    public long GraphRevision => Graph.Revision;
    public IReadOnlyDictionary<Guid, NodeVisualizationSettings> Visualizations { get; init; } =
        new ReadOnlyDictionary<Guid, NodeVisualizationSettings>(new Dictionary<Guid, NodeVisualizationSettings>());
    public Guid? ExpandedVisualizationNodeId { get; init; }
    public NodeVisualizationSettings VisualizationFor(Guid nodeId) =>
        Visualizations.GetValueOrDefault(nodeId, NodeVisualizationSettings.Default);
    /// <summary>False once the owning composite disappears. The last graph is retained read-only.</summary>
    public bool IsAvailable { get; init; } = true;
}
