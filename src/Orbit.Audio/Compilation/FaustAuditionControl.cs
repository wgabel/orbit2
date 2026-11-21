using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes;

namespace Orbit.Audio.Compilation;

/// <summary>Host-owned audition switch, shared by all applicable outputs of an editable node.</summary>
public sealed record FaustAuditionControl(Guid GraphId, Guid NodeId, NodeAuditionKind Kind, string Name, double InitialValue)
{
    internal static double Target(NodeAuditionKind kind, GraphNodeSnapshot node) => kind switch
    {
        NodeAuditionKind.Mute => node.IsMuted ? 0 : 1,
        NodeAuditionKind.Bypass => node.IsBypassed ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
