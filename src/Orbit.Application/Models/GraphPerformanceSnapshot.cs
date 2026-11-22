namespace Orbit.Application.Models;

/// <summary>Application-side diagnostics for one logical root playback lifetime.
/// Prepared program counts remain available after Stop; they are not running worklets.</summary>
public sealed record GraphPerformanceSnapshot(Guid PatchId)
{
    public long RequestedRevision { get; init; }
    public long? ProgramRevision { get; init; }
    public long? AppliedRevision { get; init; }
    public string LastOperation { get; init; } = "preparing";
    public long SourceBuilds { get; init; }
    public long ReusedUpdates { get; init; }
    public long ControlUpdates { get; init; }
    public long AcceptedReplacements { get; init; }
    public long FailedOperations { get; init; }
    public long SupersededOperations { get; init; }
    public double? SourceGenerationMs { get; init; }
    public double? ControlUpdateMs { get; init; }
    public int? ReachableLeafNodes { get; init; }
    public int? BoundaryRoutes { get; init; }
    public int? ReachableEdges { get; init; }
    public int? ProgramControls { get; init; }
    public int? ProgramOutputChannels { get; init; }
}
