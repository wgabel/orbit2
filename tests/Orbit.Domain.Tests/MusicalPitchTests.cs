using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Application.Presets;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class MusicalPitchTests
{
    private static ParameterAddress Address(GraphNodeSnapshot node, string key) => new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id);
    private static GraphNodeSnapshot Add(PatchSession session, Guid graph, string type) => NestedFaustPatch.Add(session, graph, type);
    private static void Set(PatchSession session, Guid graph, GraphNodeSnapshot node, string key, double value) =>
        session.Execute(new SetParameterValueCommand(graph, Address(node, key), value));
    private static void Wire(PatchSession session, Guid graph, GraphNodeSnapshot source, GraphNodeSnapshot target, string input, double amount = 1) =>
        session.Execute(new ConnectNodesCommand(graph, new PortAddress(source.Id, source.Outputs.Single().Id),
            new PortAddress(target.Id, target.Inputs.Single(p => p.Key == input).Id), amount));

    [Fact]
    public void EqualTemperamentOctavesFifthsCentsAndMidiRoundTrip()
    {
        Assert.Equal(440, MusicalPitch.FromMidiNote(69));
        Assert.Equal(220, MusicalPitch.FromMidiNote(57));
        Assert.Equal(81, MusicalPitch.ToMidiNote(880));
        Assert.Equal(2, MusicalPitch.Ratio(12));
        Assert.Equal(0.5, MusicalPitch.Ratio(-12));
        Assert.Equal(1.4983070769, MusicalPitch.Ratio(7), 9);
        Assert.Equal(7.5, MusicalPitch.Semitones(7, 50));
        Assert.Equal(225, MusicalPitch.Frequency(110, 12, 5, 0.01, 20_000));
        Assert.Equal(20_000, MusicalPitch.Frequency(19_000, 12, 0, 0.01, 20_000));
        Assert.Equal(0.01, MusicalPitch.Frequency(110, -24, -100, 0.01, 20_000));
    }

    [Fact]
    public void SharedPitchPreservesRatiosAndLiveEditsKeepProgramAndIdentities()
    {
        using var session = new PatchSession(BuiltInNodes.CreateRegistry());
        var root = session.GraphId;
        var source = Add(session, root, PitchControlGenerator.NodeType);
        var independent = Add(session, root, PitchControlGenerator.NodeType);
        var mixer = Add(session, root, MixerProcessor.NodeType);
        var master = Add(session, root, MasterOutput.NodeType);
        var oscillators = new[] { 110d, 165d, 220d }.Select(frequency =>
        {
            var node = Add(session, root, OscillatorGenerator.NodeType);
            Set(session, root, node, "frequency", frequency);
            Set(session, root, node, OscillatorGenerator.PitchTrackingKey, 1);
            Wire(session, root, source, node, OscillatorGenerator.PitchPortKey);
            Wire(session, root, node, mixer, "audio-in");
            return node;
        }).ToArray();
        Wire(session, root, mixer, master, "audio-in");
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var before = compiler.Compile(session.Snapshot);
        Set(session, root, source, PitchControlGenerator.SemitonesKey, 12);
        Assert.Equal(0, session.Snapshot.GetNode(independent.Id).Parameters[0].BaseValue);
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        var ranges = estimator.Estimate(session.Snapshot);
        Assert.Equal(new[] { 220d, 330d, 440d }, oscillators.Select(node => ranges[Address(node, "frequency")]!.Value.Minimum));
        Assert.All(oscillators, node => Assert.Equal(new ParameterRange(12, 12), ranges[Address(node, OscillatorGenerator.PitchKey)]));
        var after = compiler.Compile(session.Snapshot);
        Assert.Equal(before.Source, after.Source);
        Assert.Equal(12, after.Controls.Single(c => c.Address == Address(source, PitchControlGenerator.SemitonesKey)).InitialValue);
        Assert.All(oscillators, node => Assert.NotNull(after.Controls.Single(c => c.Address == Address(node, "frequency")).MeterName));
        Set(session, root, source, PitchControlGenerator.CentsKey, 50);
        var fine = estimator.Estimate(session.Snapshot);
        Assert.Equal(110 * MusicalPitch.Ratio(12.5), fine[Address(oscillators[0], "frequency")]!.Value.Maximum, 9);
        session.Undo();
        Assert.Equal(220, estimator.Estimate(session.Snapshot)[Address(oscillators[0], "frequency")]!.Value.Minimum);
        session.Redo();
        Assert.Equal(after.Source, compiler.Compile(session.Snapshot).Source);
    }

    [Fact]
    public void DormantSocketRetainsWireAndHzModulationIsAppliedAfterPitch()
    {
        using var session = new PatchSession(BuiltInNodes.CreateRegistry());
        var root = session.GraphId;
        var source = Add(session, root, PitchControlGenerator.NodeType);
        var oscillator = Add(session, root, OscillatorGenerator.NodeType);
        var master = Add(session, root, MasterOutput.NodeType);
        Set(session, root, source, PitchControlGenerator.SemitonesKey, 12);
        Set(session, root, oscillator, "frequency", 110);
        // Same numeric signal, different receiving units: 6 additive Hz, not semitones.
        Wire(session, root, source, oscillator, "frequency-modulation-in", 0.5);
        Wire(session, root, source, oscillator, OscillatorGenerator.PitchPortKey);
        Wire(session, root, oscillator, master, "audio-in");
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        var frequency = Address(oscillator, "frequency");
        Assert.Equal(new ParameterRange(116, 116), estimator.Estimate(session.Snapshot)[frequency]);
        Assert.False(session.Snapshot.GetNode(oscillator.Id).IsInputActive(oscillator.Inputs.Last().Id));
        Set(session, root, oscillator, OscillatorGenerator.PitchTrackingKey, 1);
        Assert.Equal(new ParameterRange(226, 226), estimator.Estimate(session.Snapshot)[frequency]);
        Set(session, root, oscillator, "timing", (int)OscillatorTimingMode.Synchronized);
        Assert.False(session.Snapshot.GetNode(oscillator.Id).IsInputActive(oscillator.Inputs.Last().Id));
        Assert.DoesNotContain(frequency, estimator.Estimate(session.Snapshot).Keys);
        Assert.Equal(3, session.Snapshot.Connections.Count);
        Set(session, root, oscillator, "timing", (int)OscillatorTimingMode.Free);
        Assert.Equal(new ParameterRange(226, 226), estimator.Estimate(session.Snapshot)[frequency]);
        session.Execute(new SetNodeMutedCommand(root, source.Id, true));
        Assert.Equal(new ParameterRange(110, 110), estimator.Estimate(session.Snapshot)[frequency]);
    }

    [Fact]
    public void PitchControlCrossesCompositeRackAndPersistsWithLegacyDefaults()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Bank",
            [new ExposedPort(Guid.NewGuid(), "Pitch", SignalType.Modulation)], [new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio)]));
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var source = Add(session, root, PitchControlGenerator.NodeType);
        var bank = Add(session, root, CompositeNode.NodeType);
        var master = Add(session, root, MasterOutput.NodeType);
        var child = bank.Composite!.Graph.Id;
        var oscillator = Add(session, child, OscillatorGenerator.NodeType);
        Set(session, root, source, PitchControlGenerator.SemitonesKey, -12);
        Set(session, child, oscillator, OscillatorGenerator.PitchTrackingKey, 1);
        Wire(session, root, source, bank, bank.Inputs.Single().Key);
        var rack = bank.Composite.Graph.GetNode(bank.Composite.InputRackId);
        Wire(session, child, rack, oscillator, OscillatorGenerator.PitchPortKey);
        Wire(session, child, oscillator, bank.Composite.Graph.GetNode(bank.Composite.OutputRackId), bank.Composite.Graph.GetNode(bank.Composite.OutputRackId).Inputs.Single().Key);
        Wire(session, root, bank, master, "audio-in");
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        var frequency = Address(oscillator, "frequency");
        Assert.Equal(new ParameterRange(220, 220), estimator.Estimate(session.Snapshot, child)[frequency]);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, root, []);
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(document)), registry);
        Assert.Equal(new ParameterRange(220, 220), estimator.Estimate(restored.Snapshot, child)[frequency]);
        Assert.Equal(FaustGraphCompiler.CreateBuiltIn().Compile(session.Snapshot).Source, FaustGraphCompiler.CreateBuiltIn().Compile(restored.Snapshot).Source);
    }

    [Fact]
    public void PrePitchPatchAndPresetMigrateWithoutChangingExistingIdentities()
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var oscillator = Add(session, session.GraphId, OscillatorGenerator.NodeType);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, session.GraphId, []);
        var saved = document.RootGraph.Nodes.Single();
        var legacy = saved with { Parameters = saved.Parameters.Where(p => p.Key is not (OscillatorGenerator.PitchTrackingKey or OscillatorGenerator.PitchKey)).ToArray(),
            Ports = saved.Ports.Where(p => p.Key != OscillatorGenerator.PitchPortKey).ToArray() };
        using var restored = serializer.Restore(document with { RootGraph = document.RootGraph with { Nodes = [legacy] } }, registry);
        var node = restored.Snapshot.GetNode(oscillator.Id);
        Assert.All(legacy.Parameters, p => Assert.Contains(node.Parameters, current => current.Id == p.Id));
        Assert.All(legacy.Ports, p => Assert.Contains(node.Inputs.Concat(node.Outputs), current => current.Id == p.Id));
        Assert.False(node.IsInputActive(node.Inputs.Last().Id));
        var preset = new NodePreset(1, "old", oscillator.TypeKey, "Old", "Custom", null, legacy.Parameters.ToDictionary(p => p.Key, p => p.BaseValue));
        Assert.Equal(0, preset.Parameters[OscillatorGenerator.PitchTrackingKey]);
        Assert.Equal(0, preset.Parameters[OscillatorGenerator.PitchKey]);
        new NodePresetValidator(registry).Validate(preset);
    }
}
