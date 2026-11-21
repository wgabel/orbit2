using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Manual shared semitone offset; its output is modulation, not audible audio.</summary>
public sealed class PitchControlGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.pitch-control";
    public const string SemitonesKey = "semitones";
    public const string CentsKey = "cents";

    public PitchControlGenerator(Guid id) : base(id, NodeType, "Pitch Control",
        [new Parameter(Guid.NewGuid(), new ParameterDefinition(SemitonesKey, "Transpose", 0, -48, 48,
             ParameterUnit.Semitones, ParameterScale.Linear, CanModulate: false)),
         new Parameter(Guid.NewGuid(), new ParameterDefinition(CentsKey, "Fine tune", 0, -100, 100,
             ParameterUnit.Cents, ParameterScale.Linear, CanModulate: false))],
        [], [new PortDefinition(Guid.NewGuid(), "pitch-out", "Pitch", PortDirection.Output, SignalType.Modulation)]) { }
}
