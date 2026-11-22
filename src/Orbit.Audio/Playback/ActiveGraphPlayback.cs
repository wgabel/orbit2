using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Audio.Compilation;

namespace Orbit.Audio.Playback;

/// <summary>One logical playback lifetime: the last accepted topology, program
/// and control values. Serializes updates and keeps only the latest requested revision.</summary>
internal sealed class ActiveGraphPlayback(GraphSnapshot snapshot, FaustGraphProgram program, GraphPerformanceTracker? diagnostics)
{
    public GraphPerformanceTracker? Diagnostics { get; set; } = diagnostics;
    public GraphSnapshot Topology { get; private set; } = snapshot;
    public sealed record AcceptedProgram(FaustGraphProgram Program, Guid Generation);
    public AcceptedProgram Accepted { get; private set; } = new(program, Guid.NewGuid());
    public FaustGraphProgram Program => Accepted.Program;
    public Guid Generation => Accepted.Generation;
    public IReadOnlyList<FaustParameterControl> Controls => Program.Controls;
    public Dictionary<string, double> Values { get; } = program.InitialControlValues();
    public SemaphoreSlim UpdateLock { get; } = new(1);
    // Access only under UpdateLock. Accepted transient input is carried across
    // compatible voice replacements, never into saved graph parameters.
    public Dictionary<(Guid GraphId, Guid NodeId), int> LatchedPitchActions { get; } = [];
    private long requestedRevision = snapshot.Revision;
    public long RequestedRevision => Interlocked.Read(ref requestedRevision);
    public GraphPlaybackUpdate Result { get; private set; } = new(snapshot.Revision, true);
    public bool Stopping { get; set; }
    public bool HasStarted { get; set; }

    public bool Request(long revision)
    {
        long previous;
        do
        {
            previous = RequestedRevision;
            if (revision <= previous) return false;
        } while (Interlocked.CompareExchange(ref requestedRevision, revision, previous) != previous);
        return true;
    }

    public GraphPlaybackUpdate Accept(GraphSnapshot graph, FaustGraphProgram compiled, IReadOnlyDictionary<string, double> values,
        Guid? generation = null)
    {
        Topology = graph;
        Accepted = new(compiled, generation ?? Generation);
        Values.Clear();
        foreach (var value in values) Values.Add(value.Key, value.Value);
        return Result = new(graph.Revision, true);
    }
}
