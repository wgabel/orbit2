using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Triggered pitched modal bar, using the Faust marimba model's modal data.</summary>
public sealed class PhysicalMarimbaGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.physical-marimba";
    private static readonly ParameterDefinition[] Definitions =
    [
        new("pitch", "Pitch (A4)", 0, -21, 15, ParameterUnit.Semitones, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("strength", "Strike strength", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("hardness", "Mallet hardness", .45, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("position", "Strike position", 2, 0, 4, ParameterUnit.Ratio, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("decay", "Decay (T60)", .55, .15, 1.5, ParameterUnit.Seconds, ParameterScale.Linear, true, ParameterSmoothing.Decay),
        new("output-gain", "Output gain", .9, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude)
    ];
    public PhysicalMarimbaGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }
    private PhysicalMarimbaGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "Physical Marimba", parameters,
        [new PortDefinition(Guid.NewGuid(), "trigger-in", "Trigger", PortDirection.Input, SignalType.Trigger),
         .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in", p.Definition.Name,
             PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
