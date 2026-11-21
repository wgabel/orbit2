using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>
/// Describes one periodic mono generator with selectable waveform and timing mode.
/// Free frequency and musical division retain their independent stored values.
/// </summary>
public sealed class OscillatorGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.oscillator";

    private static readonly ParameterDefinition FrequencyDefinition = new(
        "frequency", "Frequency", 440, 0.01, 20_000,
        ParameterUnit.Hertz, ParameterScale.Linear, CanModulate: true)
    {
        DisplayScale = new(new(0, 0.01), new(0.20, 40), new(0.85, 1_000), new(1, 20_000)),
        ActiveWhen = new("timing", (int)OscillatorTimingMode.Free)
    };

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

    private static readonly ParameterDefinition TimingDefinition = new(
        "timing", "Timing mode", (int)OscillatorTimingMode.Free, 0, 1,
        ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: false)
    {
        RequiresRecompile = true,
        Choices = Array.AsReadOnly(new ParameterChoice[]
        {
            new((int)OscillatorTimingMode.Free, "Free (Hz)"),
            new((int)OscillatorTimingMode.Synchronized, "Synchronized (BPM)")
        })
    };

    private static readonly ParameterDefinition DivisionDefinition = MusicalDivisionParameter.Create("timing", (int)OscillatorTimingMode.Synchronized);

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
                new Parameter(Guid.NewGuid(), WaveformDefinition),
                new Parameter(Guid.NewGuid(), TimingDefinition),
                new Parameter(Guid.NewGuid(), DivisionDefinition)
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
