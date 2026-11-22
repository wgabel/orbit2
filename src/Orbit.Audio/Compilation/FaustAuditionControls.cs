using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes;
using Orbit.Domain.Ports;

namespace Orbit.Audio.Compilation;

/// <summary>Allocates live audition controls once per graph-qualified node and kind.
/// Bypass blends dry/wet first; mute always gates the final result.</summary>
internal sealed class FaustAuditionControls(List<string> definitions)
{
    private readonly Dictionary<(Guid, Guid, NodeAuditionKind), FaustAuditionControl> controls = new();

    public IReadOnlyList<FaustAuditionControl> Manifest => Array.AsReadOnly(controls.Values.ToArray());

    public string Apply(GraphNodeSnapshot node, GraphFlattener.NodeOrigin origin, FaustNodeContext context, string wet, SignalType outputType)
    {
        var expression = wet;
        if (node.BypassInputKey is { } key)
        {
            var blend = $"orbit_audition_ramp({GetControl(NodeAuditionKind.Bypass, node, origin)})";
            // Multiplication keeps the wet computation running even at full bypass.
            expression = $"((1.0 - {blend}) * ({wet}) + {blend} * {context.Input(key)})";
        }
        if (node.SupportsMute)
        {
            var mute = GetControl(NodeAuditionKind.Mute, node, origin);
            // A mixed-output composite shares one raw control but only continuous routes slew.
            expression = outputType.IsEvent()
                ? $"({expression}) * ({mute} >= 0.5)"
                : $"({expression}) * orbit_audition_ramp({mute})";
        }
        return expression;
    }

    private string GetControl(NodeAuditionKind kind, GraphNodeSnapshot node, GraphFlattener.NodeOrigin origin)
    {
        var address = (origin.GraphId, origin.NodeId, kind);
        if (controls.TryGetValue(address, out var existing)) return existing.Name;
        var name = $"{kind.ToString().ToLowerInvariant()}_{origin.GraphId:N}_{origin.NodeId:N}";
        var defaultValue = kind == NodeAuditionKind.Mute ? 1 : 0;
        controls.Add(address, new(origin.GraphId, origin.NodeId, kind, name, FaustAuditionControl.Target(kind, node)));
        definitions.Add($"{name} = hslider(\"{name}\", {defaultValue}, 0, 1, 1);");
        return name;
    }

    public void AddRampDefinition()
    {
        // Start at the host-initialized target, then reach subsequent targets in 10 ms.
        if (controls.Count > 0)
            definitions.Insert(0, "orbit_audition_ramp(target) = step ~ _ with { delta = 1.0 / (0.01 * ma.SR); step(previous) = select2(1 - (1 : mem), previous + max(-delta, min(delta, target - previous)), target); };");
    }
}
