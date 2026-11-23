using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Triggered monophonic steel-string pickup model with pluck position and damping.</summary>
public sealed class PhysicalElectricGuitarGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.physical-electric-guitar";
    private static readonly ParameterDefinition[] Definitions =
    [
        new("pitch", "Pitch (A4)", 0, -21, 15, ParameterUnit.Semitones, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("strength", "Pluck strength", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("pluck-position", "Pluck position", .35, .05, .95, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("mute", "String sustain", .999, .95, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Decay),
        new("output-gain", "Output gain", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude)
    ];
    public PhysicalElectricGuitarGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }
    private PhysicalElectricGuitarGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "Physical Electric Guitar", parameters,
        [new PortDefinition(Guid.NewGuid(), "trigger-in", "Trigger", PortDirection.Input, SignalType.Trigger),
         .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in",
             p.Definition.Name, PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
