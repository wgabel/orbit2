using Orbit.Application.Models;
using Orbit.Audio.Compilation;

namespace Orbit.Audio.Playback;

/// <summary>Constant-size immutable diagnostic samples for one playback lifetime.</summary>
internal sealed class GraphPerformanceTracker(Guid patchId, long revision)
{
    private readonly object gate = new();
    private GraphPerformanceSnapshot snapshot = new(patchId) { RequestedRevision = revision };
    private long lastBuildRevision = -1;
    private volatile bool collecting = true;
    public bool Collecting => collecting;
    public void Stop() { lock (gate) collecting = false; }
    public GraphPerformanceSnapshot Snapshot { get { lock (gate) return snapshot; } }

    public void Request(long revision)
    {
        lock (gate)
            if (collecting && revision > snapshot.RequestedRevision)
                snapshot = snapshot with { RequestedRevision = revision, LastOperation = "pending" };
    }

    public void Build(long revision, double elapsedMs)
    {
        lock (gate)
        {
            if (!collecting) return;
            snapshot = snapshot with { SourceBuilds = snapshot.SourceBuilds + 1 };
            if (revision >= lastBuildRevision)
            {
                lastBuildRevision = revision;
                snapshot = snapshot with { SourceGenerationMs = elapsedMs };
            }
        }
    }

    public void Prepare(FaustGraphProgram program)
    {
        lock (gate) if (collecting) snapshot = WithProgram(snapshot, program) with { LastOperation = "prepared" };
    }

    public void Playing(long appliedRevision)
    {
        lock (gate)
            if (collecting && snapshot.AppliedRevision is null)
                snapshot = snapshot with { AppliedRevision = appliedRevision,
                    LastOperation = snapshot.RequestedRevision == appliedRevision ? "playing" : snapshot.LastOperation };
    }

    public void ObserveCurrent(long requestedRevision, long appliedRevision, FaustGraphProgram program)
    {
        lock (gate)
        {
            if (!collecting) return;
            // An operation already in flight when monitoring began can change
            // current state, but its timing and outcome were not observed.
            if (snapshot.ProgramRevision != program.Revision)
                snapshot = WithProgram(snapshot, program);
            if (snapshot.RequestedRevision != requestedRevision ||
                (snapshot.AppliedRevision is not null && snapshot.AppliedRevision != appliedRevision))
                snapshot = snapshot with { RequestedRevision = requestedRevision,
                    AppliedRevision = snapshot.AppliedRevision is null ? null : appliedRevision };
        }
    }

    public void Applied(long revision, FaustGraphProgram program, string operation, double? controlMs = null)
    {
        lock (gate)
        {
            if (!collecting) return;
            snapshot = WithProgram(snapshot, program) with
            {
                AppliedRevision = revision,
                ReusedUpdates = snapshot.ReusedUpdates + (operation == "reused" ? 1 : 0),
                ControlUpdates = snapshot.ControlUpdates + (controlMs.HasValue ? 1 : 0),
                AcceptedReplacements = snapshot.AcceptedReplacements + (operation == "replacement" ? 1 : 0),
                ControlUpdateMs = controlMs ?? snapshot.ControlUpdateMs,
                LastOperation = revision == snapshot.RequestedRevision ? operation : snapshot.LastOperation
            };
        }
    }

    public void Failed(long revision)
    {
        lock (gate) if (collecting) snapshot = snapshot with { FailedOperations = snapshot.FailedOperations + 1,
            LastOperation = revision == snapshot.RequestedRevision ? "failed" : snapshot.LastOperation };
    }

    public void Skipped(long revision)
    {
        lock (gate) if (collecting) snapshot = snapshot with { SupersededOperations = snapshot.SupersededOperations + 1,
            LastOperation = revision == snapshot.RequestedRevision ? "not applied" : snapshot.LastOperation };
    }

    private static GraphPerformanceSnapshot WithProgram(GraphPerformanceSnapshot value, FaustGraphProgram program) =>
        value with { ProgramRevision = program.Revision, ReachableLeafNodes = program.ReachableLeafNodes,
            BoundaryRoutes = program.BoundaryRoutes, ReachableEdges = program.ReachableEdges,
            ProgramControls = program.Controls.Count + program.AuditionControls.Count,
            ProgramOutputChannels = program.SignalTaps.ChannelCount };
}
