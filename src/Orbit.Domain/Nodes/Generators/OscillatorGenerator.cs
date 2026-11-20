using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>
/// Describes one periodic mono generator. Its waveform is a structural choice;
/// the same frequency, amplitude, phase and ports serve both audio and LFO use.
/// </summary>
public sealed class OscillatorGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.oscillator";

    private static readonly ParameterDefinition FrequencyDefinition = new(
        "frequency", "Frequency", 440, 0.01, 20_000,
        ParameterUnit.Hertz, ParameterScale.Linear, CanModulate: true);

    private static readonly ParameterDefinition AmplitudeDefinition = new(
        "amplitude", "Amplitude", 1, 0, 1,
        ParameterUnit.Normalized, ParameterScale.Linear, CanModulate: true);

    private static readonly ParameterDefinition PhaseDefinition = new(
        "phase", "Phase offset", 0, 0, 360,
        ParameterUnit.Degrees, ParameterScale.Linear, CanModulate: true);

    private static readonly ParameterDefinition WaveformDefinition = new(
        "waveform", "Waveform", (int)OscillatorWaveform.Sine, 0, 3,
        ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: false)
    {
        RequiresRecompile = true,
        Choices = Array.AsReadOnly(Enum.GetValues<OscillatorWaveform>()
            .Select(waveform => new ParameterChoice((int)waveform, waveform.ToString())).ToArray())
    };

    public OscillatorGenerator(Guid id)
        : this(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
    {
    }

    private OscillatorGenerator(Guid id, Guid frequencyPortId, Guid amplitudePortId, Guid phasePortId)
        : base(id, NodeType, "Oscillator",
            [
                new Parameter(Guid.NewGuid(), FrequencyDefinition, frequencyPortId),
                new Parameter(Guid.NewGuid(), AmplitudeDefinition, amplitudePortId),
                new Parameter(Guid.NewGuid(), PhaseDefinition, phasePortId),
                new Parameter(Guid.NewGuid(), WaveformDefinition)
            ],
            [
                new PortDefinition(frequencyPortId, "frequency-modulation-in", "Frequency",
                    PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
                new PortDefinition(amplitudePortId, "amplitude-modulation-in", "Amplitude",
                    PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
                new PortDefinition(phasePortId, "phase-modulation-in", "Phase offset",
                    PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)
            ],
            [
                new PortDefinition(Guid.NewGuid(), "signal-out", "Signal",
                    PortDirection.Output, SignalType.Audio)
            ])
    {
    }
}
