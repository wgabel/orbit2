using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

/// <summary>Pre-modulation static macro oracle: compare actual authored patches
/// with equivalent manually configured parameters on identical nested topology.</summary>
internal static class MacroBaselineFixtures
{
    public static void Write(string directory)
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Macro reference",
            [new ExposedPort(Guid.NewGuid(), "Unused", SignalType.Modulation)],
            [new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio)]));
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var outer = Add(root, CompositeNode.NodeType);
        var middle = outer.Composite!.Graph.Id;
        var inner = Add(middle, CompositeNode.NodeType);
        var child = inner.Composite!.Graph.Id;
        var osc = Add(child, OscillatorGenerator.NodeType);
        var gain = Add(child, GainProcessor.NodeType);
        var master = Add(root, MasterOutput.NodeType);
        Wire(child, osc.Id, osc.Outputs[0].Id, gain.Id, gain.Inputs.Single(p => p.Key == "audio-in").Id);
        Wire(child, gain.Id, gain.Outputs[0].Id, inner.Composite.OutputRackId, inner.Outputs[0].Id);
        Wire(middle, inner.Id, inner.Outputs[0].Id, outer.Composite.OutputRackId, outer.Outputs[0].Id);
        Wire(root, outer.Id, outer.Outputs[0].Id, master.Id, master.Inputs[0].Id);
        var frequency = Address(osc, "frequency");
        var amplitude = Address(osc, "amplitude");
        var width = Address(osc, OscillatorGenerator.PulseWidthKey);
        var waveform = Address(osc, "waveform");
        var level = Address(gain, "gain");
        var id = Guid.Parse("9014ef05-9bbb-42cd-9a9c-9b297608f1b1");
        var mapping = new CompositeMacroMapping([new(frequency, 220, 440, 2),
            new(amplitude, 0.6, 0.2), new(level, 0.5, 0.5), new(width, 0.2, 0.4)]);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var serializer = new PatchDocumentSerializer();
        var cases = new List<object>();
        string? sineSource = null;
        foreach (var (name, value, square, removed, expectedFrequency, expectedAmplitude, expectedWidth) in new[]
        {
            ("zero", 0.0, false, false, 220.0, 0.6, 0.2),
            ("middle", 0.5, false, false, 275.0, 0.4, 0.3),
            ("one", 1.0, false, false, 440.0, 0.2, 0.4),
            ("activated-square", 0.5, true, false, 275.0, 0.4, 0.3),
            ("removed", 0.5, false, true, 330.0, 0.25, 0.5)
        })
        {
            session.Execute(new ReplaceCompositeMacrosCommand(middle, inner.Id, []));
            Set(frequency, 330); Set(amplitude, 0.25); Set(level, 0.4); Set(width, 0.5);
            Set(waveform, (int)(square ? OscillatorWaveform.Square : OscillatorWaveform.Sine));
            if (!removed)
                session.Execute(new ReplaceCompositeMacrosCommand(middle, inner.Id,
                    [new(id, "Reference", 0.5, value, mapping)]));
            var authored = session.Snapshot;
            var expectedGain = removed ? 0.4 : 0.5;
            var effective = authored.WithEffectiveMacroBaseValues();
            Check(Base(authored, frequency) == 330 && Base(authored, amplitude) == 0.25 &&
                Base(authored, level) == 0.4 && Base(authored, width) == 0.5, "Manual base was overwritten.");
            Check(Close(Base(effective, frequency), expectedFrequency) &&
                Close(Base(effective, amplitude), expectedAmplitude) &&
                Close(Base(effective, level), expectedGain) &&
                Close(Base(effective, width), square ? expectedWidth : 0.5), "Unexpected effective macro base.");
            var candidate = compiler.Compile(authored);
            Check(candidate.Controls.Any(c => c.GraphId == child && c.Address == frequency &&
                Close(c.InitialValue, expectedFrequency)), "Nested frequency control origin/value was lost.");
            Check(candidate.Controls.Any(c => c.GraphId == child && c.Address == width) == square,
                "Dormant pulse width must not allocate a control.");
            var patch = serializer.Serialize(serializer.Capture(session, 120, root, []));
            using (var restored = serializer.Restore(serializer.Deserialize(patch), registry))
            {
                var restoredProgram = compiler.Compile(restored.Snapshot);
                Check(restoredProgram.Source == candidate.Source &&
                    EqualValues(restoredProgram, candidate), "Round-trip changed static macro audio.");
            }
            session.Execute(new ReplaceCompositeMacrosCommand(middle, inner.Id, []));
            Set(frequency, expectedFrequency); Set(amplitude, expectedAmplitude);
            Set(level, expectedGain); Set(width, square ? expectedWidth : 0.5);
            var reference = compiler.Compile(session.Snapshot);
            var referencePatch = serializer.Serialize(serializer.Capture(session, 120, root, []));
            Check(candidate.Source == reference.Source && EqualValues(candidate, reference),
                "Static mapping must equal the corresponding physical base controls.");
            if (!square)
            {
                sineSource ??= candidate.Source;
                Check(sineSource == candidate.Source, "Base-only edits must preserve generated source.");
            }
            cases.Add(new { name, value, square, removed, expectedFrequency, expectedAmplitude, expectedGain,
                patch, referencePatch, candidate = new { source = candidate.Source, values = candidate.InitialControlValues() },
                reference = new { source = reference.Source, values = reference.InitialControlValues() } });
        }
        File.WriteAllText(Path.Combine(directory, "static-macro-baseline.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, fixtureVersion = 1, blockSize = 128, frames = 32768,
            faustVersion = "0.19.0", cases
        }));

        GraphNodeSnapshot Add(Guid graph, string type) => session.Execute(new AddNodeCommand(graph, type))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        void Wire(Guid graph, Guid from, Guid output, Guid to, Guid input) =>
            session.Execute(new ConnectNodesCommand(graph, new(from, output), new(to, input)));
        void Set(ParameterAddress address, double value) =>
            session.Execute(new SetParameterValueCommand(child, address, value));
        double Base(GraphSnapshot snapshot, ParameterAddress address) => snapshot.GetGraph(child)
            .GetNode(address.NodeId).Parameters.Single(p => p.Id == address.ParameterId).BaseValue;
    }

    private static ParameterAddress Address(GraphNodeSnapshot node, string key) =>
        new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id);
    private static bool Close(double a, double b) => Math.Abs(a - b) < 1e-10;
    private static bool EqualValues(FaustGraphProgram a, FaustGraphProgram b)
    {
        var left = a.InitialControlValues();
        var right = b.InitialControlValues();
        return left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out var value) && Close(p.Value, value));
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
