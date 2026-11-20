using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>
/// Builds the fixed Phase 2 graph through the same commands future canvases
/// use. Its final committed snapshot maps to the precompiled ModulatedSine
/// program; dynamic compilation remains a separate Phase 4 concern.
/// </summary>
public sealed class ModulationDemoPatch
{
    public const string SoundId = "modulation-demo";
    public const string ProcessorName = "ModulatedSine";

    private ModulationDemoPatch(GraphSnapshot graph, PlaybackRequest playback)
    {
        Graph = graph;
        Playback = playback;
    }

    public GraphSnapshot Graph { get; }
    public PlaybackRequest Playback { get; }

    public static ModulationDemoPatch Create(ModulationDemoSettings settings)
    {
        using var session = OpenSession(settings);
        // This mapping is intentionally specific to the seed topology.
        var graph = session.Snapshot;
        var generators = graph.Nodes.Where(node => node.TypeKey == OscillatorGenerator.NodeType).ToArray();
        var lfo = generators[0];
        var oscillator = generators[1];
        var gain = graph.Nodes.Single(node => node.TypeKey == GainProcessor.NodeType);
        var baseFrequency = GetParameter(oscillator, "frequency");
        var route = graph.Connections.Single(connection =>
            connection.Destination == new PortAddress(oscillator.Id, baseFrequency.ModulationPortId!.Value));
        var playback = new PlaybackRequest(SoundId, ProcessorName,
            Volume: GetParameter(graph.GetNode(gain.Id), "gain").BaseValue,
            Parameters: new Dictionary<string, double>
            {
                ["baseFrequency"] = GetParameter(graph.GetNode(oscillator.Id), "frequency").BaseValue,
                ["lfoRate"] = GetParameter(graph.GetNode(lfo.Id), "frequency").BaseValue,
                ["depthHz"] = route.Amount
            });

        return new ModulationDemoPatch(graph, playback);
    }

    /// <summary>Seed an editable session with the same initial topology as the fixed audio proof.</summary>
    public static IPatchSession OpenSession(ModulationDemoSettings settings, NodeRegistry? registry = null)
    {
        settings.Validate();
        var session = new PatchSession(registry ?? BuiltInNodes.CreateRegistry());
        try
        {
            var graphId = session.GraphId;
            var lfo = AddNode(session, OscillatorGenerator.NodeType);
            var oscillator = AddNode(session, OscillatorGenerator.NodeType);
            var gain = AddNode(session, GainProcessor.NodeType);
            var output = AddNode(session, MasterOutput.NodeType);

            var lfoFrequency = GetParameter(lfo, "frequency");
            var baseFrequency = GetParameter(oscillator, "frequency");
            var volume = GetParameter(gain, "gain");

            session.Execute(new SetParameterValueCommand(graphId,
                new ParameterAddress(lfo.Id, lfoFrequency.Id), settings.LfoFrequencyHertz));
            session.Execute(new SetParameterValueCommand(graphId,
                new ParameterAddress(oscillator.Id, baseFrequency.Id), settings.BaseFrequencyHertz));
            session.Execute(new SetParameterValueCommand(graphId,
                new ParameterAddress(gain.Id, volume.Id), 0.1));

            session.Execute(new ConnectNodesCommand(graphId,
                new PortAddress(lfo.Id, lfo.Outputs.Single().Id),
                new PortAddress(oscillator.Id, baseFrequency.ModulationPortId!.Value),
                settings.DepthHertz));
            session.Execute(new ConnectNodesCommand(graphId,
                new PortAddress(oscillator.Id, oscillator.Outputs.Single().Id),
                new PortAddress(gain.Id, gain.Inputs.Single(port => port.Key == "audio-in").Id)));
            session.Execute(new ConnectNodesCommand(graphId,
                new PortAddress(gain.Id, gain.Outputs.Single().Id),
                new PortAddress(output.Id, output.Inputs.Single().Id)));
            session.ClearHistory();
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private static GraphNodeSnapshot AddNode(IPatchSession session, string type)
    {
        var result = session.Execute(new AddNodeCommand(session.GraphId, type));
        return result.ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
    }

    private static ParameterSnapshot GetParameter(GraphNodeSnapshot node, string key) =>
        node.Parameters.Single(parameter => parameter.Definition.Key == key);
}
