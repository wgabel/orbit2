using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>Mono soft-clipping distortion with independent wet level and blend.</summary>
public sealed class DistortionProcessor : AudioEffectProcessor
{
    public const string NodeType = "processor.distortion";
    private static readonly ParameterDefinition DriveDefinition = new(
        "drive", "Drive", 2, 1, 16, ParameterUnit.Ratio, ParameterScale.Linear,
        CanModulate: true, Smoothing: ParameterSmoothing.Shape);
    private static readonly ParameterDefinition MixDefinition = new(
        "mix", "Wet mix", 0.5, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear,
        CanModulate: true, Smoothing: ParameterSmoothing.EffectMix);
    private static readonly ParameterDefinition GainDefinition = new(
        "gain", "Wet output gain", 1, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear,
        CanModulate: true, Smoothing: ParameterSmoothing.Amplitude);

    public DistortionProcessor(Guid id) : this(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) { }

    private DistortionProcessor(Guid id, Guid drive, Guid mix, Guid gain)
        : base(id, NodeType, "Distortion",
            [new Parameter(Guid.NewGuid(), DriveDefinition, drive),
             new Parameter(Guid.NewGuid(), MixDefinition, mix),
             new Parameter(Guid.NewGuid(), GainDefinition, gain)],
            [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input, SignalType.Audio),
             new PortDefinition(drive, "drive-modulation-in", "Drive", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(mix, "mix-modulation-in", "Wet mix", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(gain, "gain-modulation-in", "Wet output gain", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
