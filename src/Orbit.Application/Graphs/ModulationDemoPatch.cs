using Orbit.Application.Models;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>
/// Creates the actual Phase 1 domain graph and maps its three settings to the
/// precompiled ModulatedSine Faust program. This explicit fixed mapping is
/// intentionally not a general-purpose graph compiler (planned for Phase 4).
/// </summary>
public sealed class ModulationDemoPatch
{
    public const string SoundId = "modulation-demo";
    public const string ProcessorName = "ModulatedSine";

    private ModulationDemoPatch(SignalGraph graph, PlaybackRequest playback)
    {
        Graph = graph;
        Playback = playback;
    }

    public SignalGraph Graph { get; }
    public PlaybackRequest Playback { get; }

    public static ModulationDemoPatch Create(ModulationDemoSettings settings)
    {
        settings.Validate();
        var registry = BuiltInNodes.CreateRegistry();
        var graph = new SignalGraph();
        var lfo = registry.Create(SineGenerator.NodeType);
        var oscillator = registry.Create(SineGenerator.NodeType);
        var gain = registry.Create(GainProcessor.NodeType);
        var output = registry.Create(MasterOutput.NodeType);

        foreach (var node in new[] { lfo, oscillator, gain, output })
            graph.AddNode(node);

        var lfoFrequency = GetParameter(lfo, "frequency");
        var baseFrequency = GetParameter(oscillator, "frequency");
        var volume = GetParameter(gain, "gain");

        graph.SetParameterValue(new ParameterAddress(lfo.Id, lfoFrequency.Id), settings.LfoFrequencyHertz);
        graph.SetParameterValue(new ParameterAddress(oscillator.Id, baseFrequency.Id), settings.BaseFrequencyHertz);
        graph.SetParameterValue(new ParameterAddress(gain.Id, volume.Id), 0.1);

        var route = graph.Connect(
            new PortAddress(lfo.Id, lfo.Outputs.Single().Id),
            new PortAddress(oscillator.Id, baseFrequency.ModulationPortId!.Value),
            settings.DepthHertz);
        graph.Connect(new PortAddress(oscillator.Id, oscillator.Outputs.Single().Id),
            new PortAddress(gain.Id, gain.Inputs.Single(port => port.Key == "audio-in").Id));
        graph.Connect(new PortAddress(gain.Id, gain.Outputs.Single().Id),
            new PortAddress(output.Id, output.Inputs.Single().Id));

        // The gain node maps to the existing audio-clock-scheduled voice envelope.
        // The Faust worklet itself generates the LFO and audible carrier.
        var playback = new PlaybackRequest(SoundId, ProcessorName,
            Volume: volume.BaseValue, Parameters: new Dictionary<string, double>
            {
                ["baseFrequency"] = baseFrequency.BaseValue,
                ["lfoRate"] = lfoFrequency.BaseValue,
                ["depthHz"] = route.Amount
            });

        return new ModulationDemoPatch(graph, playback);
    }

    private static Parameter GetParameter(ISignalNode node, string key) =>
        node.Parameters.Single(parameter => parameter.Definition.Key == key);
}
