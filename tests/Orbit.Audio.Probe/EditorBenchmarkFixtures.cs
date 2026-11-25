using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;

/// <summary>Reproducible editor workloads; kept separate from offline DSP fixtures.</summary>
internal static class EditorBenchmarkFixtures
{
    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var serializer = new PatchDocumentSerializer();
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var fixtures = new List<object>();
        foreach (var count in new[] { 50, 200, 500, 102 })
        {
            using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
            var graph = session.GraphId;
            var mixer = Add(MixerProcessor.NodeType);
            var outputGain = Add(GainProcessor.NodeType);
            var master = Add(MasterOutput.NodeType);
            Set(outputGain, "gain", 0.12);
            Audio(mixer, outputGain);
            Audio(outputGain, master);
            var lane = 0;
            while (session.Snapshot.Nodes.Count < count)
            {
                var carrier = Add(OscillatorGenerator.NodeType);
                Set(carrier, "frequency", 110 + (lane % 24) * 12);
                Set(carrier, "amplitude", 0.25);
                var last = carrier;
                if (session.Snapshot.Nodes.Count < count)
                {
                    var lfo = Add(OscillatorGenerator.NodeType);
                    Set(lfo, "frequency", 0.15 + (lane % 8) * 0.04);
                    Modulate(lfo, carrier, "frequency", 8);
                }
                if (session.Snapshot.Nodes.Count < count)
                {
                    var filter = Add(FilterProcessor.NodeType);
                    Set(filter, "cutoff", 800 + (lane % 8) * 150);
                    Audio(last, filter);
                    last = filter;
                }
                if (session.Snapshot.Nodes.Count < count)
                {
                    var gain = Add(GainProcessor.NodeType);
                    Set(gain, "gain", 0.5);
                    Audio(last, gain);
                    last = gain;
                }
                if (lane % 8 == 0 && session.Snapshot.Nodes.Count < count)
                {
                    var sequencer = Add(NoteSequencerGenerator.NodeType);
                    Set(sequencer, NoteSequencerGenerator.LoopKey, 1);
                    Set(sequencer, NoteSequencerGenerator.CountKey, 4);
                    Set(sequencer, NoteSequencerGenerator.StepKey(0), 58);
                    Set(sequencer, NoteSequencerGenerator.StepKey(2), 65);
                    Set(carrier, OscillatorGenerator.PitchTrackingKey, 1);
                    Modulate(sequencer, carrier, OscillatorGenerator.PitchKey, 1, "pitch-out");
                }
                Audio(last, mixer, 1d / count);
                lane++;
            }

            // Keep two root nodes outside the 100 authored leaf-node group.
            // Use the real grouping command so the generated fixture has the same
            // rack routing and validation as a user-created composite.
            GraphNodeSnapshot? wrapper = null;
            if (count == 102)
            {
                var selected = session.Snapshot.Nodes
                    .Where(node => node.Id != outputGain.Id && node.Id != master.Id)
                    .Select(node => node.Id).ToArray();
                if (selected.Length != 100)
                    throw new InvalidOperationException("Unexpected composite fixture size.");
                wrapper = session.Execute(new GroupNodesCommand(graph, selected, "Benchmark composite",
                    session.Snapshot.Revision)).ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
            }
            var root = session.Snapshot;
            var child = wrapper?.Composite?.Graph;
            if (wrapper is not null && (root.Nodes.Count != 3 || child?.Nodes.Count != 102))
                throw new InvalidOperationException("Composite fixture must have 100 authored nodes and two racks.");
            // Dense desktop layout at the minimum supported zoom. Mobile views
            // intentionally contain a smaller subset of the same world.
            var columns = count == 50 ? 10 : 20;
            SavedCanvas Layout(GraphSnapshot snapshot) => new(snapshot.Id, 0, 0, 0.25,
                snapshot.Nodes.Select((node, index) => new SavedNodePosition(node.Id,
                    80 + index % columns * 360, 80 + index / columns * 380)).ToArray());
            SavedCanvas[] layouts = child is null ? [Layout(root)] : [Layout(root), Layout(child)];
            var id = child is null ? $"editor-{count}" : "editor-composite-100";
            var patch = id + ".orbit2.json";
            var document = serializer.Capture(session, 120, graph, layouts,
                SavedPatchOverview.Create($"EDITOR BENCH v1 — {id}",
                    "Synthetic editor workload; not a musical composition.",
                    "Start with low output volume. Preserve this file for exact repeat measurements.", ["benchmark"]));
            var json = serializer.Serialize(document);
            var decoded = serializer.Deserialize(json);
            using var restored = serializer.Restore(decoded, BuiltInNodes.CreateEditorRegistry());
            if (decoded.CanvasLayouts.Length != layouts.Length)
                throw new InvalidOperationException("Editor fixture round trip changed canvas count.");
            foreach (var snapshot in child is null ? new[] { root } : new[] { root, child })
            {
                var restoredGraph = restored.Snapshot.GetGraph(snapshot.Id);
                var restoredLayout = decoded.CanvasLayouts.Single(canvas => canvas.GraphId == snapshot.Id);
                if (!restoredGraph.Nodes.Select(node => node.Id).ToHashSet().SetEquals(snapshot.Nodes.Select(node => node.Id)) ||
                    restoredGraph.Connections.Count != snapshot.Connections.Count ||
                    restoredLayout.Positions.Length != snapshot.Nodes.Count || restoredLayout.Zoom != 0.25)
                    throw new InvalidOperationException("Editor fixture round trip changed topology or layout.");
            }
            var program = compiler.Compile(root);
            var restoredProgram = compiler.Compile(restored.Snapshot);
            if (program.Source != restoredProgram.Source)
                throw new InvalidOperationException("Editor fixture round trip changed DSP source.");
            File.WriteAllText(Path.Combine(directory, patch), json);
            if (child is null)
                fixtures.Add(new { id, patch, nodes = count, connections = root.Connections.Count,
                    reachableLeafNodes = program.ReachableLeafNodes, columns, zoom = 0.25 });
            else
                fixtures.Add(new { id, patch, nodes = root.Nodes.Count, connections = root.Connections.Count,
                    reachableLeafNodes = program.ReachableLeafNodes, columns, zoom = 0.25,
                    composite = new { wrapperId = wrapper!.Id, name = wrapper.Name,
                        rootNodeIds = root.Nodes.Select(node => node.Id).ToArray(),
                        childNodeIds = child.Nodes.Select(node => node.Id).ToArray(),
                        childConnections = child.Connections.Count } });

            GraphNodeSnapshot Add(string type) =>
                session.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

            void Set(GraphNodeSnapshot node, string key, double value) =>
                session.Execute(new SetParameterValueCommand(graph,
                    new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id), value));

            void Audio(GraphNodeSnapshot source, GraphNodeSnapshot destination, double amount = 1) =>
                Connect(source, source.Outputs.Single().Id, destination,
                    destination.Inputs.Single(p => p.Key == "audio-in").Id, amount);

            void Modulate(GraphNodeSnapshot source, GraphNodeSnapshot destination, string parameter, double amount,
                string output = "signal-out") =>
                Connect(source, source.Outputs.Single(p => p.Key == output).Id, destination,
                    destination.Parameters.Single(p => p.Definition.Key == parameter).ModulationPortId!.Value, amount);

            void Connect(GraphNodeSnapshot source, Guid sourcePort, GraphNodeSnapshot destination, Guid destinationPort, double amount)
            {
                if (session.Execute(new ConnectNodesCommand(graph, new(source.Id, sourcePort),
                    new(destination.Id, destinationPort), amount)).ChangeSet is null)
                    throw new InvalidOperationException("Editor fixture connection rejected.");
            }
        }
        File.WriteAllText(Path.Combine(directory, "fixtures.json"),
            JsonSerializer.Serialize(new { schemaVersion = 1, fixtureVersion = 1, fixtures },
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
