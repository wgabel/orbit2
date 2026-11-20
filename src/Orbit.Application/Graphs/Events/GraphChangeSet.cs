using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Events;

/// <summary>One successful edit, all of its facts, and the final consistent read model.
/// Whole-graph subscribers react once to this boundary, not once per removed edge.</summary>
public sealed record GraphChangeSet(
    Guid PatchId, GraphSnapshot Snapshot, IReadOnlyList<IGraphEvent> Events)
{
    public Guid GraphId => Snapshot.Id;
    public long Revision => Snapshot.Revision;
    public bool TopologyChanged => Events.Any(graphEvent => graphEvent is not ParameterChanged);
}
