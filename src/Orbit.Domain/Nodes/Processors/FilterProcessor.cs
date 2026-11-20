using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>A mono resonant filter with sample-rate cutoff and resonance modulation.</summary>
public sealed class FilterProcessor : SignalNode, IProcessor
{
    public const string NodeType = "processor.filter";

    private static readonly ParameterDefinition ModeDefinition = new(
        "mode", "Filter mode", (int)FilterMode.LowPass, 0, 3,
        ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: false)
    {
        RequiresRecompile = true,
        Choices = Array.AsReadOnly(new ParameterChoice[]
        {
            new((int)FilterMode.LowPass, "Low-pass"),
            new((int)FilterMode.HighPass, "High-pass"),
            new((int)FilterMode.BandPass, "Band-pass"),
            new((int)FilterMode.Notch, "Notch")
        })
    };

    private static readonly ParameterDefinition CutoffDefinition = new(
        "cutoff", "Cutoff", 1000, 20, 20000,
        ParameterUnit.Hertz, ParameterScale.Linear, CanModulate: true);

    private static readonly ParameterDefinition ResonanceDefinition = new(
        "resonance", "Resonance (Q)", 0.707, 0.5, 8,
        ParameterUnit.Ratio, ParameterScale.Linear, CanModulate: true);

    public FilterProcessor(Guid id) : this(id, Guid.NewGuid(), Guid.NewGuid()) { }

    private FilterProcessor(Guid id, Guid cutoffPortId, Guid resonancePortId)
        : base(id, NodeType, "Filter",
            [new Parameter(Guid.NewGuid(), ModeDefinition),
             new Parameter(Guid.NewGuid(), CutoffDefinition, cutoffPortId),
             new Parameter(Guid.NewGuid(), ResonanceDefinition, resonancePortId)],
            [new PortDefinition(Guid.NewGuid(), "audio-in", "Audio", PortDirection.Input, SignalType.Audio),
             new PortDefinition(cutoffPortId, "cutoff-modulation-in", "Cutoff", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum),
             new PortDefinition(resonancePortId, "resonance-modulation-in", "Resonance", PortDirection.Input, SignalType.Modulation, FanIn: FanInPolicy.Sum)],
            [new PortDefinition(Guid.NewGuid(), "audio-out", "Audio", PortDirection.Output, SignalType.Audio)]) { }
}
