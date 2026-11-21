using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Domain.Nodes.Generators;

public sealed class ManualTriggerGenerator(Guid id) : SignalNode(id, NodeType, "Manual Trigger", [], [],
    [new PortDefinition(Guid.NewGuid(), "trigger-out", "Trigger", PortDirection.Output, SignalType.Trigger)]), IGenerator
{
    public const string NodeType = "generator.manual-trigger";
}

/// <summary>Timed gate, opened by a manual action or a typed trigger/clock pulse.</summary>
public sealed class GateGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.gate";
    public const string SourceKey = "source";
    public const string DurationKey = "duration";
    public const string TriggerPortKey = "trigger-in";
    public GateGenerator(Guid id) : this(id, Guid.NewGuid()) { }
    private GateGenerator(Guid id, Guid duration) : base(id, NodeType, "Gate",
        [new Parameter(Guid.NewGuid(), new(SourceKey, "Gate source", 0, 0, 1, ParameterUnit.Ratio, ParameterScale.Linear, false)
        { RequiresRecompile = true, Choices = Array.AsReadOnly(new ParameterChoice[] { new(0, "Manual"), new(1, "Trigger / clock") }) }),
         new Parameter(Guid.NewGuid(), new(DurationKey, "Gate duration", 100, 1, 20000, ParameterUnit.Milliseconds, ParameterScale.Linear, true)
        { DisplayScale = new(new(0, 1), new(0.5, 200), new(1, 20000)) }, duration)],
        [new PortDefinition(Guid.NewGuid(), TriggerPortKey, "Trigger", PortDirection.Input, SignalType.Trigger) { ActiveWhen = new(SourceKey, 1) },
         new PortDefinition(duration, "duration-modulation-in", "Gate duration", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
        [new PortDefinition(Guid.NewGuid(), "gate-out", "Gate", PortDirection.Output, SignalType.Gate)]) { }
}

public sealed class ClockGenerator : SignalNode, IGenerator
{
    public const string NodeType = "generator.clock";
    public ClockGenerator(Guid id) : this(id, Guid.NewGuid()) { }
    private ClockGenerator(Guid id, Guid rate) : base(id, NodeType, "Clock",
        [new Parameter(Guid.NewGuid(), new("timing", "Timing mode", 1, 0, 1, ParameterUnit.Ratio, ParameterScale.Linear, false)
        { RequiresRecompile = true, Choices = Array.AsReadOnly(new ParameterChoice[] { new(0, "Free (Hz)"), new(1, "Synchronized (BPM)") }) }),
         new Parameter(Guid.NewGuid(), new("frequency", "Rate", 2, 0.01, 100, ParameterUnit.Hertz, ParameterScale.Linear, true)
        { ActiveWhen = new("timing", 0), DisplayScale = new(new(0, 0.01), new(0.5, 1), new(1, 100)) }, rate),
         new Parameter(Guid.NewGuid(), MusicalDivisionParameter.Create("timing", 1))],
        [new PortDefinition(rate, "frequency-modulation-in", "Rate", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
        [new PortDefinition(Guid.NewGuid(), "clock-out", "Clock", PortDirection.Output, SignalType.Clock)]) { }
}
