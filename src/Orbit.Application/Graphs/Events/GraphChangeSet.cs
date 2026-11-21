using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Events;

/// <summary>One successful edit, its target graph identity and all of its facts.
/// The snapshot always represents the entire patch, not just the edited graph.</summary>
public sealed record GraphChangeSet(
    Guid PatchId, Guid GraphId, GraphSnapshot Snapshot, IReadOnlyList<IGraphEvent> Events)
{
    public GraphSnapshot ChangedGraph => Snapshot.GetGraph(GraphId);
    public long Revision => Snapshot.Revision;
    public bool TopologyChanged => Events.Any(graphEvent => graphEvent is not (ParameterChanged or NodeMutedChanged));
}
