using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Monophonic bow-driven acoustic waveguide; validation of all registers is pending.</summary>
public sealed class PhysicalViolinGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.physical-violin";
    private static readonly ParameterDefinition[] Definitions =
    [
        new("pitch", "Pitch (A4)", 0, -21, 15, ParameterUnit.Semitones, ParameterScale.Linear, true, ParameterSmoothing.Pitch),
        new("expression", "Bow expression", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude),
        new("bow-pressure", "Bow pressure", .5, .1, .9, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("bow-position", "Bow position", .7, .1, .9, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("attack", "Bow attack", .06, .015, .5, ParameterUnit.Seconds, ParameterScale.Linear, true, ParameterSmoothing.Timing),
        new("release", "Bow release", .12, .02, .5, ParameterUnit.Seconds, ParameterScale.Linear, true, ParameterSmoothing.Timing),
        new("vibrato-depth", "Vibrato depth", 0, 0, 30, ParameterUnit.Cents, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("vibrato-rate", "Vibrato rate", 5, 2, 8, ParameterUnit.Hertz, ParameterScale.Linear, true, ParameterSmoothing.Rate),
        new("output-gain", "Output gain", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude)
    ];
    public PhysicalViolinGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }
    private PhysicalViolinGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "Physical Violin (experimental)", parameters,
        [new PortDefinition(Guid.NewGuid(), "gate-in", "Gate", PortDirection.Input, SignalType.Gate),
         .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in",
             p.Definition.Name, PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
