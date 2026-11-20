using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>A sine locked to the patch's shared beat signal instead of a free frequency.
/// Musical division is a discrete base setting; amplitude and phase remain modulatable.</summary>
public sealed class TempoSineGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.tempo-sine";

    private static readonly ParameterDefinition DivisionDefinition = new(
        "division", "Musical division", (int)MusicalDivision.QuarterNote, 0, 6,
        ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: false)
    {
        Choices = Array.AsReadOnly(new ParameterChoice[]
        {
            new((int)MusicalDivision.SixteenthNote, "1/16 note"),
            new((int)MusicalDivision.EighthNote, "1/8 note"),
            new((int)MusicalDivision.QuarterNote, "1/4 note"),
            new((int)MusicalDivision.HalfNote, "1/2 note"),
            new((int)MusicalDivision.WholeNote, "1 bar (4/4)"),
            new((int)MusicalDivision.QuarterNoteTriplet, "1/4 triplet"),
            new((int)MusicalDivision.DottedQuarterNote, "Dotted 1/4 note")
        })
    };
    private static readonly ParameterDefinition PhaseDefinition = new(
        "phase", "Phase offset", 0, 0, 360, ParameterUnit.Degrees, ParameterScale.Linear, true);
    private static readonly ParameterDefinition AmplitudeDefinition = new(
        "amplitude", "Amplitude", 1, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true);

    public TempoSineGenerator(Guid id) : this(id, Guid.NewGuid(), Guid.NewGuid()) { }

    private TempoSineGenerator(Guid id, Guid phasePort, Guid amplitudePort)
        : base(id, NodeType, "Tempo sine LFO",
            [new Parameter(Guid.NewGuid(), DivisionDefinition),
             new Parameter(Guid.NewGuid(), PhaseDefinition, phasePort),
             new Parameter(Guid.NewGuid(), AmplitudeDefinition, amplitudePort)],
            [new PortDefinition(phasePort, "phase-modulation-in", "Phase offset", PortDirection.Input,
                SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(amplitudePort, "amplitude-modulation-in", "Amplitude", PortDirection.Input,
                SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new PortDefinition(Guid.NewGuid(), "signal-out", "Signal", PortDirection.Output, SignalType.Audio)])
    { }
}
