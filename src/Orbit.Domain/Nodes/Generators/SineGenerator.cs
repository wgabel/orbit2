using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>
/// Describes a mono sine generator. The same node serves as an audible oscillator
/// or an LFO, depending on its frequency and connections. Faust execution comes later.
/// </summary>
public sealed class SineGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.sine";

    private static readonly ParameterDefinition FrequencyDefinition = new(
        "frequency", "Frequency", 440, 0.01, 20_000,
        ParameterUnit.Hertz, ParameterScale.Linear, CanModulate: true);

    private static readonly ParameterDefinition AmplitudeDefinition = new(
        "amplitude", "Amplitude", 1, 0, 1,
        ParameterUnit.Normalized, ParameterScale.Linear, CanModulate: true);

    public SineGenerator(Guid id)
        : this(id, Guid.NewGuid(), Guid.NewGuid())
    {
    }

    private SineGenerator(Guid id, Guid frequencyPortId, Guid amplitudePortId)
        : base(id, NodeType, "Sine generator",
            [
                new Parameter(Guid.NewGuid(), FrequencyDefinition, frequencyPortId),
                new Parameter(Guid.NewGuid(), AmplitudeDefinition, amplitudePortId)
            ],
            [
                new PortDefinition(frequencyPortId, "frequency-modulation-in", "Frequency",
                    PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
                new PortDefinition(amplitudePortId, "amplitude-modulation-in", "Amplitude",
                    PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)
            ],
            [
                new PortDefinition(Guid.NewGuid(), "signal-out", "Signal",
                    PortDirection.Output, SignalType.Audio)
            ])
    {
    }
}
