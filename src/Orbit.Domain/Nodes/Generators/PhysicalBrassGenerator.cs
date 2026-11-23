using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Monophonic brass lip/tube physical model with live pressure and mute controls.</summary>
public sealed class PhysicalBrassGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.physical-brass";
    private static readonly ParameterDefinition[] Definitions =
    [
        new("pitch", "Pitch (A4)", 0, -21, 15, ParameterUnit.Semitones, ParameterScale.Linear, true, ParameterSmoothing.Pitch),
        new("expression", "Breath expression", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude),
        new("lip-tension", "Lip tension", .5, .25, .75, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("mute-opening", "Mute opening", .5, .2, .85, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("attack", "Attack", .045, .015, .4, ParameterUnit.Seconds, ParameterScale.Linear, true, ParameterSmoothing.Timing),
        new("release", "Release", .12, .02, .5, ParameterUnit.Seconds, ParameterScale.Linear, true, ParameterSmoothing.Timing),
        new("vibrato-depth", "Vibrato depth", 0, 0, 30, ParameterUnit.Cents, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("vibrato-rate", "Vibrato rate", 5, 2, 8, ParameterUnit.Hertz, ParameterScale.Linear, true, ParameterSmoothing.Rate),
        new("output-gain", "Output gain", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude)
    ];
    public PhysicalBrassGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }
    private PhysicalBrassGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "Physical Brass", parameters,
        [new PortDefinition(Guid.NewGuid(), "gate-in", "Gate", PortDirection.Input, SignalType.Gate),
         .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in",
             p.Definition.Name, PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
