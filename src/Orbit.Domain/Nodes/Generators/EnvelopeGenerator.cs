using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Oscillator-triggered, unipolar attack/decay modulation source.</summary>
public sealed class EnvelopeGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.envelope";
    public const string TriggerPortKey = "signal-trigger-in";
    public const string AttackKey = "attack";
    public const string DecayKey = "decay";
    public const string AttackCurveKey = "attack-curve";
    public const string DecayCurveKey = "decay-curve";

    private static readonly ParameterDefinition[] Definitions =
    [
        new(AttackKey, "Attack", 8, 1, 10000, ParameterUnit.Milliseconds, ParameterScale.Linear, true)
        { DisplayScale = new(new(0, 1), new(0.5, 100), new(1, 10000)) },
        new(DecayKey, "Decay", 500, 1, 20000, ParameterUnit.Milliseconds, ParameterScale.Linear, true)
        { DisplayScale = new(new(0, 1), new(0.5, 500), new(1, 20000)) },
        new(AttackCurveKey, "Attack curve", 1, 0.25, 8, ParameterUnit.Ratio, ParameterScale.Linear, true),
        new(DecayCurveKey, "Decay curve", 3, 0.25, 8, ParameterUnit.Ratio, ParameterScale.Linear, true)
    ];

    public EnvelopeGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, Guid.NewGuid())).ToArray()) { }

    private EnvelopeGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "AD Envelope", parameters,
        [new PortDefinition(Guid.NewGuid(), TriggerPortKey, "Signal trigger", PortDirection.Input, SignalType.Modulation),
         .. parameters.Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in",
             p.Definition.Name, PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "envelope-out", "Envelope", PortDirection.Output, SignalType.Modulation)]) { }
}
