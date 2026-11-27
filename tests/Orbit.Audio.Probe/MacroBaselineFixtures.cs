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

        // Slow square driver gives settled positive and negative plateaus.
        var driver = Add(middle, OscillatorGenerator.NodeType);
        void Driver(string key, double value) => session.Execute(new SetParameterValueCommand(middle, Address(driver, key), value));
        Driver("frequency", 1);
        Driver("amplitude", 1);
        Driver("waveform", (int)OscillatorWaveform.Square);
        Set(waveform, (int)OscillatorWaveform.Sine);
        var dynamicCases = new List<object>();
        foreach (var (name, depth, constantOnly, connected) in new[]
        {
            ("signed", 0.25, false, true),
            ("inverted", -0.25, false, true),
            ("clipped", 0.8, false, true),
            ("zero-depth", 0.0, false, true),
            ("downstream", 0.25, false, true),
            ("constant-only", 0.25, true, true),
            ("disconnected", 0.0, false, false)
        })
        {
            session.Execute(new ReplaceCompositeMacrosCommand(middle, inner.Id, []));
            Set(frequency, 330); Set(amplitude, 0.25); Set(level, 0.5);
            var activeMapping = constantOnly ? new CompositeMacroMapping([new(level, 0.5, 0.5)]) : mapping;
            session.Execute(new ReplaceCompositeMacrosCommand(middle, inner.Id,
                [new(id, "Reference", 0.5, 0.5, activeMapping, inner.Inputs[0].Id)]));
            if (connected)
                session.Execute(new ConnectNodesCommand(middle, new(driver.Id, driver.Outputs[0].Id),
                    new(inner.Id, inner.Inputs[0].Id), depth));
            var downstream = name == "downstream";
            if (downstream)
            {
                Set(Address(osc, OscillatorGenerator.PitchTrackingKey), 1);
                Set(Address(osc, OscillatorGenerator.PitchKey), 12);
                session.Execute(new ConnectNodesCommand(child,
                    new(inner.Composite.InputRackId, inner.Inputs[0].Id),
                    new(osc.Id, osc.Parameters.Single(p => p.Definition.Key == "frequency").ModulationPortId!.Value), 40));
            }
            var candidate = compiler.Compile(session.Snapshot, enableMacroModulation: true);
            var reference = compiler.Compile(session.Snapshot);
            Check(candidate.MacroControls.Count == (connected ? 1 : 0), "Unexpected dynamic manifest.");
            if (!connected)
                Check(candidate.Source == reference.Source && EqualValues(candidate, reference), "Disconnected macro changed source.");
            dynamicCases.Add(new { name, depth, constantOnly, connected, downstream,
                meter = candidate.MacroControls.SingleOrDefault()?.MeterName,
                macro = candidate.MacroControls.SingleOrDefault()?.Name,
                candidate = new { source = candidate.Source, values = candidate.InitialControlValues() },
                reference = new { source = reference.Source, values = reference.InitialControlValues() } });
            if (downstream)
            {
                var native = session.Snapshot.GetGraph(child).Connections.Single(e =>
                    e.Source.NodeId == inner.Composite.InputRackId);
                session.Execute(new RemoveConnectionCommand(child, native.Id));
                Set(Address(osc, OscillatorGenerator.PitchKey), 0);
                Set(Address(osc, OscillatorGenerator.PitchTrackingKey), 0);
            }
            if (connected)
            {
                var edge = session.Snapshot.GetGraph(middle).Connections.Single(e => e.Destination.NodeId == inner.Id);
                session.Execute(new RemoveConnectionCommand(middle, edge.Id));
            }
        }
        File.WriteAllText(Path.Combine(directory, "dynamic-macro-baseline.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, blockSize = 128, frames = 96000, faustVersion = "0.19.0", cases = dynamicCases
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
