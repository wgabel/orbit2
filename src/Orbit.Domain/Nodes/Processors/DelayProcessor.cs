using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>Mono feedback delay with independent free and tempo-synchronized timing.</summary>
public sealed class DelayProcessor : SignalNode, IProcessor
{
    public const string NodeType = "processor.delay";
    private static readonly ParameterDefinition TimeDefinition = new(
        "time", "Delay time", 0.3, 0.01, 2, ParameterUnit.Seconds, ParameterScale.Linear, CanModulate: true)
        { ActiveWhen = new("timing", (int)DelayTimingMode.Free) };
    private static readonly ParameterDefinition FeedbackDefinition = new(
        "feedback", "Feedback", 0.35, 0, 0.9, ParameterUnit.Normalized, ParameterScale.Linear, CanModulate: true);
    private static readonly ParameterDefinition MixDefinition = new(
        "mix", "Wet mix", 0.35, 0, 1, ParameterUnit.Normalized, ParameterScale.Linear, CanModulate: true);
    private static readonly ParameterDefinition TimingDefinition = new(
        "timing", "Timing mode", (int)DelayTimingMode.Free, 0, 1, ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: false)
        {
            RequiresRecompile = true,
            Choices = Array.AsReadOnly(new ParameterChoice[] { new(0, "Free (seconds)"), new(1, "Synchronized (BPM)") })
        };
    private static readonly ParameterDefinition DivisionDefinition = MusicalDivisionParameter.Create("timing", (int)DelayTimingMode.Synchronized);

    public DelayProcessor(Guid id) : this(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()) { }

    private DelayProcessor(Guid id, Guid time, Guid feedback, Guid mix)
        : base(id, NodeType, "Delay",
            [new Parameter(Guid.NewGuid(), TimeDefinition, time), new Parameter(Guid.NewGuid(), FeedbackDefinition, feedback),
             new Parameter(Guid.NewGuid(), MixDefinition, mix), new Parameter(Guid.NewGuid(), TimingDefinition), new Parameter(Guid.NewGuid(), DivisionDefinition)],
            [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input, SignalType.Audio),
             new PortDefinition(time, "time-modulation-in", "Delay time", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(feedback, "feedback-modulation-in", "Feedback", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(mix, "mix-modulation-in", "Wet mix", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
