using System.Diagnostics;
using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

/// <summary>Versioned graph recipes for the opt-in offline and live benchmarks.</summary>
internal static class BenchmarkFixtures
{
    public static void Write(string directory, bool large)
    {
        Directory.CreateDirectory(directory);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var serializer = new PatchDocumentSerializer();
        var fixtures = new List<object>();
        foreach (var count in large ? new[] { 1, 10, 50, 100, 200 } : new[] { 1, 10, 50 })
        {
            using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
            var oscillators = Bank(session, count, false, false);
            var id = $"sine-bank-{count}";
            Export(session, id, count, 0, oscillators, false);
            if (count == 10)
            {
                Export(session, "sine-bank-10-taps-idle", count, 0, oscillators, true, id);
                Export(session, "sine-bank-10-taps-one", count, 1, oscillators, true, id);
                Export(session, "sine-bank-10-taps-eight", count, 8, oscillators, true, id);
            }
        }
        using (var session = new PatchSession(BuiltInNodes.CreateEditorRegistry()))
        {
            var oscillators = Bank(session, 10, true, false);
            Export(session, "modulated-mixed-10", 20, 0, oscillators, false);
        }
        using (var session = new PatchSession(BuiltInNodes.CreateEditorRegistry()))
        {
            var oscillators = Bank(session, 10, false, true);
            Export(session, "sine-bank-10-fx", 10, 0, oscillators, false);
        }
        using (var session = new PatchSession(BuiltInNodes.CreateEditorRegistry()))
        {
            var root = session.GraphId;
            var mixer = Add(session, root, MixerProcessor.NodeType);
            var master = Add(session, root, MasterOutput.NodeType);
            for (var i = 0; i < 4; i++)
            {
                var drone = Add(session, root, DroneTemplate.LayeredPresetKey);
                var child = drone.Composite!.Graph;
                foreach (var node in child.Nodes.Where(n => n.TypeKey == OscillatorGenerator.NodeType))
                    Set(session, child.Id, node, "frequency",
                        node.Parameters.Single(p => p.Definition.Key == "frequency").BaseValue + 3 * i);
                Connect(session, root, drone, mixer, 0.25);
            }
            Connect(session, root, mixer, master);
            Export(session, "nested-layered-4", 12, 0, [], false);
        }
        File.WriteAllText(Path.Combine(directory, "fixtures.json"),
            JsonSerializer.Serialize(new { schemaVersion = 1, fixtureVersion = 1, large, fixtures },
                new JsonSerializerOptions { WriteIndented = true }));

        void Export(PatchSession session, string id, int oscillators, int taps,
            IReadOnlyList<GraphNodeSnapshot> sources, bool dynamic, string? pairedMono = null)
        {
            var root = session.Snapshot;
            var auxiliary = dynamic ? SignalTapManifest.MaxAuxiliaryChannels : 0;
            // Warm source generation separately from the three measured builds.
            _ = compiler.Compile(root, auxiliary, dynamicTaps: dynamic);
            var times = new double[3];
            FaustGraphProgram program = null!;
            for (var i = 0; i < times.Length; i++)
            {
                var start = Stopwatch.GetTimestamp();
                program = compiler.Compile(root, auxiliary, dynamicTaps: dynamic);
                times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            if (program.RequiresTransport || program.RhythmicInputs.Count != 0)
                throw new InvalidOperationException("Baseline workloads must not need external signal inputs.");
            var selections = new Dictionary<string, double>();
            for (var i = 0; i < taps; i++)
            {
                var selector = program.SignalTaps.ObservationIds[new(root.Id, root.Id,
                    sources[i].Id, sources[i].Outputs.Single().Id, PortDirection.Output)];
                if (selector <= 0) throw new InvalidOperationException("Expected distinct auxiliary source.");
                selections.Add($"orbit_tap_select_{i + 1}", (i + 1) * (program.SignalTaps.SelectorCount + 1) + selector);
            }
            var patch = id + ".orbit2.json";
            var document = serializer.Capture(session, 120, root.Id, [],
                SavedPatchOverview.Create($"BENCH v1 — {id}", "Synthetic DSP performance fixture.",
                    "Start at a low output volume; compare identical settings and sample rates. No musical intent.", ["benchmark"]));
            var json = serializer.Serialize(document);
            // Prove the exact exported patch restores through the normal app importer.
            using var restored = serializer.Restore(serializer.Deserialize(json), BuiltInNodes.CreateEditorRegistry());
            var restoredProgram = compiler.Compile(restored.Snapshot, auxiliary, dynamicTaps: dynamic);
            if (restoredProgram.Source != program.Source ||
                restoredProgram.ReachableLeafNodes != program.ReachableLeafNodes)
                throw new InvalidOperationException("Benchmark patch restoration changed DSP.");
            File.WriteAllText(Path.Combine(directory, patch), json);
            fixtures.Add(new
            {
                id, patch, oscillators, activeTapSlots = taps, pairedMono,
                source = program.Source, values = program.InitialControlValues(), selections,
                sourceGenerationMs = times, expectedChannels = program.SignalTaps.ChannelCount,
                reachableLeafNodes = program.ReachableLeafNodes, boundaryRoutes = program.BoundaryRoutes,
                reachableEdges = program.ReachableEdges, applicationControls = program.Controls.Count + program.AuditionControls.Count,
                masterPeakLimit = 0.55
            });
        }
    }

    private static List<GraphNodeSnapshot> Bank(PatchSession session, int count, bool mixed, bool effects)
    {
        var graph = session.GraphId;
        var master = Add(session, graph, MasterOutput.NodeType);
        var gain = Add(session, graph, GainProcessor.NodeType);
        Set(session, graph, gain, "gain", 0.4);
        var mixer = count > 1 ? Add(session, graph, MixerProcessor.NodeType) : gain;
        var sources = new List<GraphNodeSnapshot>();
        var waveforms = new[] { OscillatorWaveform.Sine, OscillatorWaveform.Triangle, OscillatorWaveform.Sawtooth, OscillatorWaveform.Square };
        for (var i = 0; i < count; i++)
        {
            var source = Add(session, graph, OscillatorGenerator.NodeType);
            Set(session, graph, source, "frequency", 110 + 7 * i);
            Set(session, graph, source, "amplitude", 0.5);
            if (mixed)
            {
                Set(session, graph, source, "waveform", (int)waveforms[i % waveforms.Length]);
                var lfo = Add(session, graph, OscillatorGenerator.NodeType);
                Set(session, graph, lfo, "frequency", .1 + .03 * i);
                var frequency = source.Parameters.Single(p => p.Definition.Key == "frequency");
                session.Execute(new ConnectNodesCommand(graph, new(lfo.Id, lfo.Outputs.Single().Id),
                    new(source.Id, frequency.ModulationPortId!.Value), 15));
            }
            Connect(session, graph, source, mixer, 1.0 / count);
            sources.Add(source);
        }
        if (count > 1) Connect(session, graph, mixer, gain);
        var last = gain;
        if (effects)
        {
            foreach (var type in new[] { FilterProcessor.NodeType, ParametricEqProcessor.NodeType,
                ChorusProcessor.NodeType, DelayProcessor.NodeType, ReverbProcessor.NodeType })
            {
                var effect = Add(session, graph, type);
                Connect(session, graph, last, effect);
                last = effect;
            }
            var finalGain = Add(session, graph, GainProcessor.NodeType);
            Set(session, graph, finalGain, "gain", 0.25);
            Connect(session, graph, last, finalGain);
            last = finalGain;
        }
        Connect(session, graph, last, master);
        return sources;
    }

    private static GraphNodeSnapshot Add(PatchSession session, Guid graph, string type) =>
        session.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    private static void Set(PatchSession session, Guid graph, GraphNodeSnapshot node, string key, double value) =>
        session.Execute(new SetParameterValueCommand(graph,
            new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id), value));

    private static void Connect(PatchSession session, Guid graph, GraphNodeSnapshot source,
        GraphNodeSnapshot destination, double amount = 1) =>
        session.Execute(new ConnectNodesCommand(graph, new(source.Id, source.Outputs.Single().Id),
            new(destination.Id, destination.Inputs.Single(p => p.Key == "audio-in").Id), amount));
}
