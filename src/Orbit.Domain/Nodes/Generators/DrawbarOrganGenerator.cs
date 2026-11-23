using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Monophonic tonewheel-style additive organ, separate from a rotary cabinet.</summary>
public sealed class DrawbarOrganGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.drawbar-organ";
    public static IReadOnlyList<string> Drawbars { get; } = Array.AsReadOnly(new[] { "16", "5-1-3", "8", "4", "2-2-3", "2", "1-3-5", "1-1-3", "1" });
    private static readonly string[] Footages = ["16′", "5⅓′", "8′", "4′", "2⅔′", "2′", "1⅗′", "1⅓′", "1′"];
    private static readonly ParameterDefinition[] Definitions =
    [
        new("pitch", "Pitch (A4)", 0, -36, 24, ParameterUnit.Semitones, ParameterScale.Linear, true, ParameterSmoothing.Pitch),
        new("swell", "Swell", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude),
        new("key-click", "Key click", .2, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude),
        new("percussion", "Harmonic percussion", .15, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude),
        .. Drawbars.Select((key,i) => new ParameterDefinition("drawbar-" + key, Footages[i], i < 3 ? 8 : 0, 0, 8,
            ParameterUnit.Ratio, ParameterScale.Linear, true, ParameterSmoothing.Amplitude))
    ];
    public DrawbarOrganGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }
    private DrawbarOrganGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "Drawbar Organ", parameters,
        [new PortDefinition(Guid.NewGuid(), "gate-in", "Gate", PortDirection.Input, SignalType.Gate),
         new PortDefinition(Guid.NewGuid(), "trigger-in", "Trigger", PortDirection.Input, SignalType.Trigger),
         .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in", p.Definition.Name,
             PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
