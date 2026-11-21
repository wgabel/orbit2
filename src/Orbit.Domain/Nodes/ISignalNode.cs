using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes;

/// <summary>
/// Common graph-facing contract for every signal node. No references to the
/// canvas, connected neighbors, browser objects, DSP implementation or clock.
/// </summary>
public interface ISignalNode
{
    Guid Id { get; }
    string TypeKey { get; }
    string Name { get; }
    bool SupportsMute { get; }
    bool IsMuted { get; }
    string? BypassInputKey { get; }
    bool IsBypassed { get; }
    IReadOnlyList<Parameter> Parameters { get; }
    IReadOnlyList<PortDefinition> Inputs { get; }
    IReadOnlyList<PortDefinition> Outputs { get; }
}
