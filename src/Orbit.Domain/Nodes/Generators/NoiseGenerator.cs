using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

public sealed class NoiseGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.noise";

    private static readonly ParameterDefinition ColorDefinition = new(
        "color", "Noise color", (int)NoiseColor.White, 0, 2,
        ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: false, Smoothing: ParameterSmoothing.None)
    {
        RequiresRecompile = true,
        Choices = Array.AsReadOnly(Enum.GetValues<NoiseColor>()
            .Select(color => new ParameterChoice((int)color, color.ToString())).ToArray())
    };

    private static readonly ParameterDefinition AmplitudeDefinition = new(
        "amplitude", "Amplitude", 0.2, 0, 1,
        ParameterUnit.Normalized, ParameterScale.Linear, CanModulate: true, Smoothing: ParameterSmoothing.Amplitude);

    public NoiseGenerator(Guid id) : this(id, Guid.NewGuid()) { }

    private NoiseGenerator(Guid id, Guid amplitudePortId)
        : base(id, NodeType, "Noise",
            [new Parameter(Guid.NewGuid(), ColorDefinition),
             new Parameter(Guid.NewGuid(), AmplitudeDefinition, amplitudePortId)],
            [new PortDefinition(amplitudePortId, "amplitude-modulation-in", "Amplitude",
                PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new PortDefinition(Guid.NewGuid(), "signal-out", "Signal", PortDirection.Output, SignalType.Audio)]) { }
}
