using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Triggered mono physical string with optional independent audio excitation.</summary>
public sealed class PhysicalPluckGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.physical-pluck";
    private static readonly ParameterDefinition[] Definitions =
    [
        new("pitch", "Pitch (A4)", 0, -21, 15, ParameterUnit.Semitones, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("strength", "Strike strength", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("decay", "Decay (T60)", 1, .3, 2, ParameterUnit.Seconds, ParameterScale.Linear, true, ParameterSmoothing.Decay),
        new("output-gain", "Output gain", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude)
    ];

    public PhysicalPluckGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }
    private PhysicalPluckGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "Physical Pluck", parameters,
        [new PortDefinition(Guid.NewGuid(), "trigger-in", "Trigger", PortDirection.Input, SignalType.Trigger),
         new PortDefinition(Guid.NewGuid(), "excitation-in", "Excitation", PortDirection.Input, SignalType.Audio),
         .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in", p.Definition.Name,
             PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
