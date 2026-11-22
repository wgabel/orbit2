using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

public enum EnvelopeMode { Ad = 0, Ar = 1, Adsr = 2, Loop = 3 }

/// <summary>Unipolar triggered, gated or repeating envelope modulation source.</summary>
public sealed class EnvelopeGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.envelope";
    public const string TriggerPortKey = "signal-trigger-in";
    public const string EventTriggerPortKey = "event-trigger-in";
    public const string GatePortKey = "gate-in";
    public const string TriggerSourceKey = "trigger-source";
    public const string ModeKey = "mode";
    public const string AttackKey = "attack";
    public const string DecayKey = "decay";
    public const string AttackCurveKey = "attack-curve";
    public const string DecayCurveKey = "decay-curve";
    public const string ReleaseKey = "release";
    public const string ReleaseCurveKey = "release-curve";
    public const string SustainKey = "sustain";
    public const string RestKey = "rest";

    private static ParameterActivation Mode(EnvelopeMode mode, ParameterActivation? and = null, ParameterActivation? or = null) => new(ModeKey, (int)mode, and, or);
    private static ParameterActivation Decays => Mode(EnvelopeMode.Ad, or: Mode(EnvelopeMode.Adsr, or: Mode(EnvelopeMode.Loop)));
    private static ParameterActivation Releases => Mode(EnvelopeMode.Ar, or: Mode(EnvelopeMode.Adsr));

    private static readonly ParameterDefinition[] Definitions =
    [
        new(AttackKey, "Attack", 8, 1, 10000, ParameterUnit.Milliseconds, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Timing)
        { DisplayScale = new(new(0, 1), new(0.5, 100), new(1, 10000)) },
        new(DecayKey, "Decay", 500, 1, 20000, ParameterUnit.Milliseconds, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Timing)
        { ActiveWhen = Decays, DisplayScale = new(new(0, 1), new(0.5, 500), new(1, 20000)) },
        new(AttackCurveKey, "Attack curve", 1, 0.25, 8, ParameterUnit.Ratio, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Shape),
        new(DecayCurveKey, "Decay curve", 3, 0.25, 8, ParameterUnit.Ratio, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Shape) { ActiveWhen = Decays },
        new(ModeKey, "Envelope mode", 0, 0, 3, ParameterUnit.Ratio, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None)
        { RequiresRecompile = true, Choices = Array.AsReadOnly(new ParameterChoice[] { new(0, "AD"), new(1, "AR"), new(2, "ADSR"), new(3, "Loop") }) },
        new(TriggerSourceKey, "Trigger source", 0, 0, 2, ParameterUnit.Ratio, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.None)
        { ActiveWhen = Mode(EnvelopeMode.Ad), RequiresRecompile = true, Choices = Array.AsReadOnly(new ParameterChoice[] { new(0, "Continuous signal"), new(1, "Trigger / clock"), new(2, "Gate rising edge") }) },
        new(ReleaseKey, "Release", 500, 1, 20000, ParameterUnit.Milliseconds, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Timing)
        { ActiveWhen = Releases, DisplayScale = new(new(0, 1), new(0.5, 500), new(1, 20000)) },
        new(ReleaseCurveKey, "Release curve", 3, 0.25, 8, ParameterUnit.Ratio, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Shape) { ActiveWhen = Releases },
        new(SustainKey, "Sustain", 0.5, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Amplitude) { ActiveWhen = Mode(EnvelopeMode.Adsr) },
        new(RestKey, "Rest", 0, 0, 20000, ParameterUnit.Milliseconds, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Timing)
        { ActiveWhen = Mode(EnvelopeMode.Loop) }
    ];

    public static IReadOnlyList<string> LegacyAdParameterKeys { get; } = Array.AsReadOnly(new[] { AttackKey, DecayKey, AttackCurveKey, DecayCurveKey, TriggerSourceKey });
    public static IEnumerable<ParameterDefinition> ModeExtensionDefinitions => Definitions.Where(d => !LegacyAdParameterKeys.Contains(d.Key));

    public EnvelopeGenerator(Guid id) : this(id, Definitions.Select(d => new Parameter(Guid.NewGuid(), d, d.CanModulate ? Guid.NewGuid() : null)).ToArray()) { }

    private EnvelopeGenerator(Guid id, Parameter[] parameters) : base(id, NodeType, "Envelope", parameters,
        [new PortDefinition(Guid.NewGuid(), TriggerPortKey, "Signal trigger", PortDirection.Input, SignalType.Modulation) { ActiveWhen = Mode(EnvelopeMode.Ad, new(TriggerSourceKey, 0)) },
         new PortDefinition(Guid.NewGuid(), EventTriggerPortKey, "Trigger", PortDirection.Input, SignalType.Trigger) { ActiveWhen = Mode(EnvelopeMode.Ad, new(TriggerSourceKey, 1)) },
         new PortDefinition(Guid.NewGuid(), GatePortKey, "Gate", PortDirection.Input, SignalType.Gate) { ActiveWhen = Mode(EnvelopeMode.Ad, new(TriggerSourceKey, 2), Releases) },
         .. parameters.Where(p => p.ModulationPortId.HasValue).Select(p => new PortDefinition(p.ModulationPortId!.Value, p.Definition.Key + "-modulation-in",
             p.Definition.Name, PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum))],
        [new PortDefinition(Guid.NewGuid(), "envelope-out", "Envelope", PortDirection.Output, SignalType.Modulation)]) { }
}
