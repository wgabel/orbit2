using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

public enum PhysicalSustainedKind { Clarinet, Flute }

/// <summary>Gate-driven physical voice with continuous expression, pitch and model-specific character.</summary>
public sealed class PhysicalSustainedGenerator : SignalNode, IGenerator
{
    public const string ClarinetType = "generator.physical-clarinet";
    public const string FluteType = "generator.physical-flute";

    public static string TypeFor(PhysicalSustainedKind kind) => kind switch
    {
        PhysicalSustainedKind.Clarinet => ClarinetType,
        PhysicalSustainedKind.Flute => FluteType,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public static string NameFor(PhysicalSustainedKind kind) => kind switch
    {
        PhysicalSustainedKind.Clarinet => "Physical Clarinet",
        PhysicalSustainedKind.Flute => "Physical Flute",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static string CharacterName(PhysicalSustainedKind kind) => kind switch
    {
        PhysicalSustainedKind.Clarinet => "Reed stiffness",
        PhysicalSustainedKind.Flute => "Breath noise",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static ParameterDefinition[] Definitions(PhysicalSustainedKind kind) =>
    [
        new("pitch", "Pitch (A4)", 0, -21, 15, ParameterUnit.Semitones, ParameterScale.Linear, true, ParameterSmoothing.Pitch),
        new("expression", "Expression", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude),
        new("character", CharacterName(kind), .5, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("attack", "Attack", .055, .015, .4, ParameterUnit.Seconds, ParameterScale.Linear, true, ParameterSmoothing.Timing),
        new("release", "Release", .10, .02, .5, ParameterUnit.Seconds, ParameterScale.Linear, true, ParameterSmoothing.Timing),
        new("vibrato-depth", "Vibrato depth", 0, 0, 30, ParameterUnit.Cents, ParameterScale.Linear, true, ParameterSmoothing.Shape),
        new("vibrato-rate", "Vibrato rate", 5, 2, 8, ParameterUnit.Hertz, ParameterScale.Linear, true, ParameterSmoothing.Rate),
        new("output-gain", "Output gain", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude)
    ];

    public PhysicalSustainedGenerator(Guid id, PhysicalSustainedKind kind) : this(id, kind, Definitions(kind)
        .Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }

    private PhysicalSustainedGenerator(Guid id, PhysicalSustainedKind kind, Parameter[] parameters)
        : base(id, TypeFor(kind), NameFor(kind), parameters,
            [new PortDefinition(Guid.NewGuid(), "gate-in", "Gate", PortDirection.Input, SignalType.Gate),
             .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in", p.Definition.Name,
                 PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
            [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
