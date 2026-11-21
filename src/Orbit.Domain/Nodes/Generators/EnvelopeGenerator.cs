using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Oscillator-triggered, unipolar attack/decay modulation source.</summary>
public sealed class EnvelopeGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.envelope";
    public const string TriggerPortKey = "signal-trigger-in";
    public const string EventTriggerPortKey = "event-trigger-in";
    public const string GatePortKey = "gate-in";
    public const string TriggerSourceKey = "trigger-source";
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
        new(DecayCurveKey, "Decay curve", 3, 0.25, 8, ParameterUnit.Ratio, ParameterScale.Linear, true),
        new(TriggerSourceKey, "Trigger source", 0, 0, 2, ParameterUnit.Ratio, ParameterScale.Linear, false)
        { RequiresRecompile = true, Choices = Array.AsReadOnly(new ParameterChoice[] { new(0, "Continuous signal"), new(1, "Trigger / clock"), new(2, "Gate rising edge") }) }
    ];

    public EnvelopeGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, d.CanModulate ? Guid.NewGuid() : null)).ToArray()) { }

    private EnvelopeGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "AD Envelope", parameters,
        [new PortDefinition(Guid.NewGuid(), TriggerPortKey, "Signal trigger", PortDirection.Input, SignalType.Modulation) { ActiveWhen = new(TriggerSourceKey, 0) },
         new PortDefinition(Guid.NewGuid(), EventTriggerPortKey, "Trigger", PortDirection.Input, SignalType.Trigger) { ActiveWhen = new(TriggerSourceKey, 1) },
         new PortDefinition(Guid.NewGuid(), GatePortKey, "Gate", PortDirection.Input, SignalType.Gate) { ActiveWhen = new(TriggerSourceKey, 2) },
         .. parameters.Where(p => p.ModulationPortId.HasValue).Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in",
             p.Definition.Name, PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "envelope-out", "Envelope", PortDirection.Output, SignalType.Modulation)]) { }
}
