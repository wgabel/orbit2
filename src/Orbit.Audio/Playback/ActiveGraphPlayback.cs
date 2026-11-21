using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Audio.Compilation;

namespace Orbit.Audio.Playback;

/// <summary>One logical playback lifetime: the last accepted topology, program
/// and control values. Serializes updates and keeps only the latest requested revision.</summary>
internal sealed class ActiveGraphPlayback(GraphSnapshot snapshot, FaustGraphProgram program)
{
    public GraphSnapshot Topology { get; private set; } = snapshot;
    public FaustGraphProgram Program { get; private set; } = program;
    public Guid Generation { get; private set; } = Guid.NewGuid();
    public IReadOnlyList<FaustParameterControl> Controls => Program.Controls;
    public Dictionary<string, double> Values { get; } = program.InitialControlValues();
    public SemaphoreSlim UpdateLock { get; } = new(1);
    private long requestedRevision = snapshot.Revision;
    public long RequestedRevision => Interlocked.Read(ref requestedRevision);
    public GraphPlaybackUpdate Result { get; private set; } = new(snapshot.Revision, true);
    public bool Stopping { get; set; }

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
        Program = compiled;
        if (generation is Guid next) Generation = next;
        Values.Clear();
        foreach (var value in values) Values.Add(value.Key, value.Value);
        return Result = new(graph.Revision, true);
    }
}
