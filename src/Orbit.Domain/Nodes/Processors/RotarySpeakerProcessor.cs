using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>Mono two-band rotating-speaker simulation with independent rotor inertia.</summary>
public sealed class RotarySpeakerProcessor : AudioEffectProcessor
{
    public const string NodeType = "processor.rotary-speaker";
    private static readonly ParameterDefinition[] Definitions =
    [
        new("speed", "Speed (0 brake, 1 slow, 2 fast)", 1, 0, 2, ParameterUnit.Ratio, ParameterScale.Linear, true, ParameterSmoothing.None),
        new("am-depth", "Amplitude depth", .6, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.Amplitude),
        new("doppler-depth", "Doppler depth", .8, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.DelayTime),
        new("mix", "Wet mix", 1, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, ParameterSmoothing.EffectMix)
    ];
    public RotarySpeakerProcessor(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }
    private RotarySpeakerProcessor(Guid id, Parameter[] parameters) : base(id, NodeType, "Rotary Speaker", parameters,
        [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input, SignalType.Audio),
         .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in", p.Definition.Name,
             PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
