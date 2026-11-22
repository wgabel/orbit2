using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;

namespace Orbit.Application.Persistence;

/// <summary>Fresh portable worked examples assembled from existing registered graphs, never the active session.</summary>
public static class PatchExampleCatalog
{
    public const string ModulationKey = "modulation";
    public const string ImpactKey = "impact";

    public static PatchDocument Create(string key, PatchDocumentSerializer serializer) => key switch
    {
        ModulationKey => CreateModulation(serializer),
        ImpactKey => CreateImpact(serializer),
        _ => throw new ArgumentException("Unknown example patch.", nameof(key))
    };

    private static PatchDocument CreateModulation(PatchDocumentSerializer serializer)
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(), BuiltInNodes.CreateEditorRegistry());
        var graph = session.Snapshot;
        var layout = Annotate(graph, "Frequency modulation",
            "The slow control oscillator changes the voice frequency. The connection amount is the deviation in Hz. The gain stage reduces the output level.",
            ["LFO", "Voice", "Output gain", "Master"]);
        var overview = SavedPatchOverview.Create("Modulation study",
            "A simple frequency-modulated oscillator for learning signal routing, not a finished musical piece.",
            "Start at a low listening level. Change the LFO rate and frequency connection amount. Observe the carrier frequency and output on the scope.",
            ["Tutorial", "Modulation", "Oscillator"]);
        return serializer.Capture(session, MusicalTransport.DefaultTempo, graph.Id, [layout], overview);
    }

    private static PatchDocument CreateImpact(PatchDocumentSerializer serializer)
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var impact = session.Execute(new AddNodeCommand(session.GraphId, ImpactTemplate.PresetKey))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var output = session.Execute(new AddNodeCommand(session.GraphId, MasterOutput.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        session.Execute(new ConnectNodesCommand(session.GraphId, new PortAddress(impact.Id, impact.Outputs.Single().Id),
            new PortAddress(output.Id, output.Inputs.Single().Id)));
        var child = impact.Composite!.Graph;
        var rootLayout = Annotate(session.Snapshot, "Impact voice",
            "Open the composite to inspect the independent pitch and amplitude envelopes. The composite output is routed to the master.",
            ["AD percussion", "Master"]);
        var childLayout = Annotate(child, "Transient and tail",
            "A slow square oscillator triggers both envelopes. Pitch drops from a short sweep toward the base 55 Hz; the separate amplitude envelope shapes the decay. Experiment with both decay times.",
            ["Trigger", "Pitch envelope", "Amplitude envelope", "Voice"]);
        var overview = SavedPatchOverview.Create("Impact — AD percussion",
            "Repeated synthesized impacts using independent pitch and amplitude envelopes inside an editable composite.",
            "Keep listening volume low. Start playback, open the composite and adjust pitch decay, amplitude decay and trigger rate. This is a synthesis recipe, not an acoustic instrument recording.",
            ["Tutorial", "Percussion", "Envelope", "Composite"]);
        return serializer.Capture(session, MusicalTransport.DefaultTempo, session.GraphId, [rootLayout, childLayout], overview);
    }

    private static SavedCanvas Annotate(GraphSnapshot graph, string title, string body, string[] aliases)
    {
        var nodes = graph.Nodes.Where(node => node.TypeKey is not (InputRack.NodeType or OutputRack.NodeType)).ToArray();
        var note = new SavedNote(Guid.NewGuid(), title, body, NoteAccent.Instruction, 0, 0, 520, 220, false);
        // Leave room above the nodes for the note, keeping rack placement explicit when present.
        return new SavedCanvas(graph.Id, 0, 0, 0.75,
            graph.Nodes.Select((node, i) => new SavedNodePosition(node.Id, i * 340, 280)).ToArray())
        {
            Notes = [note],
            NodeDescriptions = nodes.Select((node, i) => new SavedNodeDescription(node.Id, aliases[i], "")).ToArray()
        };
    }
}
