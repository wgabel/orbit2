using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Audio.Compilation;

namespace Orbit.Audio.Playback;

/// <summary>One playback lifetime: immutable topology and compiler control manifest,
/// plus the last successfully applied base values. Serializes concurrent edits.</summary>
internal sealed class ActiveGraphPlayback(GraphSnapshot snapshot, FaustGraphProgram program)
{
    public GraphSnapshot Topology { get; } = snapshot;
    public IReadOnlyList<FaustParameterControl> Controls { get; } = program.Controls;
    public Dictionary<string, double> Values { get; } =
        program.Controls.ToDictionary(control => control.Name, control => control.InitialValue);
    public SemaphoreSlim UpdateLock { get; } = new(1);
    public long ExaminedRevision { get; set; } = snapshot.Revision;
    public GraphPlaybackUpdate Result { get; set; } = new(snapshot.Revision, false, true);
    public bool Stopping { get; set; }
}
