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
    public AnnotationVisibility AnnotationVisibility { get; init; } = AnnotationVisibility.Show;
    public AnnotationDetail ZoomAnnotationDetail { get; init; } = AnnotationDetail.Full;
    public AnnotationDetail NoteDetail(Guid id) => AnnotationDetailPolicy.Resolve(AnnotationVisibility, ZoomAnnotationDetail, SelectedNoteId == id);
    public AnnotationDetail GroupDetail(Guid id) => AnnotationDetailPolicy.Resolve(AnnotationVisibility, ZoomAnnotationDetail, SelectedGroupId == id);
    public bool ShowSecondaryAnnotations => AnnotationVisibility == AnnotationVisibility.Show && ZoomAnnotationDetail == AnnotationDetail.Full;
    public Guid GraphId => Graph.Id;
    public long GraphRevision => Graph.Revision;
    public IReadOnlyDictionary<Guid, NodeVisualizationSettings> Visualizations { get; init; } =
        new ReadOnlyDictionary<Guid, NodeVisualizationSettings>(new Dictionary<Guid, NodeVisualizationSettings>());
    public Guid? ExpandedVisualizationNodeId { get; init; }
    public IReadOnlyList<SavedNote> Notes { get; init; } = [];
    public Guid? SelectedNoteId { get; init; }
    public IReadOnlyList<SavedVisualGroup> VisualGroups { get; init; } = [];
    public Guid? SelectedGroupId { get; init; }
    public bool CanUndoAnnotations { get; init; }
    public bool CanRedoAnnotations { get; init; }
    public IReadOnlyDictionary<Guid, SavedNodeDescription> NodeDescriptions { get; init; } =
        new ReadOnlyDictionary<Guid, SavedNodeDescription>(new Dictionary<Guid, SavedNodeDescription>());
    public IReadOnlyDictionary<Guid, SavedConnectionDescription> ConnectionDescriptions { get; init; } =
        new ReadOnlyDictionary<Guid, SavedConnectionDescription>(new Dictionary<Guid, SavedConnectionDescription>());
    public string DisplayName(Guid nodeId) => NodeDescriptions.GetValueOrDefault(nodeId) is { } annotation &&
        !string.IsNullOrWhiteSpace(annotation.Alias) ? annotation.Alias : Graph.GetNode(nodeId).Name;
    public NodeVisualizationSettings VisualizationFor(Guid nodeId) =>
        Visualizations.GetValueOrDefault(nodeId, NodeVisualizationSettings.Default);
    /// <summary>False once the owning composite disappears. The last graph is retained read-only.</summary>
    public bool IsAvailable { get; init; } = true;
}
