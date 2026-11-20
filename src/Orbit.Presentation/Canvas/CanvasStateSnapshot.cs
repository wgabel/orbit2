using Orbit.Application.Graphs.Snapshots;

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
    public Guid GraphId => Graph.Id;
    public long GraphRevision => Graph.Revision;
    /// <summary>False once the owning composite disappears. The last graph is retained read-only.</summary>
    public bool IsAvailable { get; init; } = true;
}
