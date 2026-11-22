using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>A single damped mode excited by the audio input; silent until excited.</summary>
public sealed class ResonatorProcessor : AudioEffectProcessor
{
    public const string NodeType = "processor.resonator";
    public const string PitchKey = "pitch-offset";
    public const string PitchPortKey = "pitch-modulation-in";

    private static readonly ParameterDefinition Frequency = new("frequency", "Frequency", 220, 20, 20_000,
        ParameterUnit.Hertz, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Pitch)
    { DisplayScale = new(new(0, 20), new(.65, 1000), new(1, 20_000)) };
    private static readonly ParameterDefinition Pitch = new(PitchKey, "Pitch offset", 0, -60, 60,
        ParameterUnit.Semitones, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Pitch);
    private static readonly ParameterDefinition Decay = new("decay", "Decay (T60)", 1, .02, 20,
        ParameterUnit.Seconds, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Decay)
    { DisplayScale = new(new(0, .02), new(.5, 1), new(1, 20)) };
    private static readonly ParameterDefinition Excitation = new("excitation", "Excitation gain", 2, 0, 32,
        ParameterUnit.Ratio, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Amplitude);
    private static readonly ParameterDefinition OutputGain = new("output-gain", "Output gain", .5, 0, 1,
        ParameterUnit.Normalized, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Amplitude);
    private static readonly ParameterDefinition Mix = new("mix", "Wet mix", 1, 0, 1,
        ParameterUnit.Normalized, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.EffectMix);

    public ResonatorProcessor(Guid id) : this(id, new[] { Frequency, Pitch, Decay, Excitation, OutputGain, Mix }
        .Select(definition => new Parameter(Guid.NewGuid(), definition, Guid.NewGuid())).ToArray()) { }

    private ResonatorProcessor(Guid id, Parameter[] parameters)
        : base(id, NodeType, "Resonator", parameters,
            [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input, SignalType.Audio),
             .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value,
                 p.Definition.Key == PitchKey ? PitchPortKey : $"{p.Definition.Key}-modulation-in",
                 p.Definition.Key == PitchKey ? "Pitch" : p.Definition.Name,
                 PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
            [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
