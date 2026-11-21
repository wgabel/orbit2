using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Generators;

/// <summary>Explicit conversion from weighted continuous signals to binary rhythmic streams.</summary>
public abstract class RhythmicAdapter : SignalNode, IGenerator
{
    public const string InputKey = "signal-in";
    protected RhythmicAdapter(Guid id, string type, string name, SignalType output) : base(id, type, name, [],
        [new PortDefinition(Guid.NewGuid(), InputKey, "Signal", PortDirection.Input, SignalType.Modulation)],
        [new PortDefinition(Guid.NewGuid(), "event-out", output.ToString(), PortDirection.Output, output)]) { }
}

public sealed class SignalToTriggerGenerator(Guid id) : RhythmicAdapter(id, NodeType, "Signal to Trigger", SignalType.Trigger)
{
    public const string NodeType = "generator.signal-to-trigger";
}

public sealed class SignalToGateGenerator(Guid id) : RhythmicAdapter(id, NodeType, "Signal to Gate", SignalType.Gate)
{
    public const string NodeType = "generator.signal-to-gate";
}
