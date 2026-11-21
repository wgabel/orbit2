using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Application.Presets;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class EnvelopeTests
{
    private static ParameterAddress Address(GraphNodeSnapshot node, string key) => new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id);

    [Fact]
    public void ModesRetainWiresAndDormantValuesAndOnlyCompileActiveDependencies()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var graph = patch.ChildId;
        var env = NestedFaustPatch.Add(session, graph, EnvelopeGenerator.NodeType);
        var gate = NestedFaustPatch.Add(session, graph, GateGenerator.NodeType);
        var trigger = NestedFaustPatch.Add(session, graph, ManualTriggerGenerator.NodeType);
        var modulator = NestedFaustPatch.Add(session, graph, OscillatorGenerator.NodeType);
        var carrier = session.Snapshot.GetGraph(graph).GetNode(patch.Frequency.NodeId);
        void Set(string key, double value) => session.Execute(new SetParameterValueCommand(graph, Address(env, key), value));
        void Wire(GraphNodeSnapshot from, GraphNodeSnapshot to, string key, double amount = 1) => session.Execute(new ConnectNodesCommand(graph,
            new(from.Id, from.Outputs.Single().Id), new(to.Id, to.Inputs.Single(p => p.Key == key).Id), amount));
        GraphNodeSnapshot Current() => session.Snapshot.GetGraph(graph).GetNode(env.Id);
        bool Active(string key) => Current().IsParameterActive(Current().Parameters.Single(p => p.Definition.Key == key));
        bool Socket(string key) => Current().IsInputActive(env.Inputs.Single(p => p.Key == key).Id);
        Wire(modulator, env, EnvelopeGenerator.TriggerPortKey);
        Wire(trigger, env, EnvelopeGenerator.EventTriggerPortKey);
        Wire(gate, env, EnvelopeGenerator.GatePortKey);
        Wire(modulator, env, "release-modulation-in", 100);
        Wire(env, carrier, "amplitude-modulation-in", 0.5);
        Set(EnvelopeGenerator.TriggerSourceKey, 1);
        var edges = session.Snapshot.GetGraph(graph).Connections.ToArray();
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var ad = compiler.Compile(session.Snapshot);
        Assert.Equal(trigger.Id, Assert.Single(ad.RhythmicInputs).NodeId);
        Assert.True(Active(EnvelopeGenerator.DecayKey)); Assert.False(Active(EnvelopeGenerator.ReleaseKey));
        Assert.True(Socket(EnvelopeGenerator.EventTriggerPortKey)); Assert.False(Socket(EnvelopeGenerator.GatePortKey));

        Set(EnvelopeGenerator.ModeKey, (int)EnvelopeMode.Ar);
        Assert.False(Active(EnvelopeGenerator.TriggerSourceKey)); Assert.False(Active(EnvelopeGenerator.DecayKey));
        Assert.True(Active(EnvelopeGenerator.ReleaseKey)); Assert.False(Active(EnvelopeGenerator.SustainKey));
        Assert.True(Socket(EnvelopeGenerator.GatePortKey)); Assert.False(Socket(EnvelopeGenerator.EventTriggerPortKey));
        Assert.True(Socket("release-modulation-in"));
        var ar = compiler.Compile(session.Snapshot);
        Assert.Equal(gate.Id, Assert.Single(ar.RhythmicInputs).NodeId);
        Assert.DoesNotContain(ar.Controls, c => c.Address == Address(env, EnvelopeGenerator.DecayKey));
        Set(EnvelopeGenerator.ReleaseKey, 1700);
        Assert.Equal(ar.Source, compiler.Compile(session.Snapshot).Source);
        Set(EnvelopeGenerator.ModeKey, (int)EnvelopeMode.Adsr);
        Assert.True(Active(EnvelopeGenerator.SustainKey)); Assert.True(Active(EnvelopeGenerator.DecayKey));
        var adsr = compiler.Compile(session.Snapshot);
        Assert.NotEqual(ar.Source, adsr.Source);
        Set(EnvelopeGenerator.SustainKey, 0.7);
        Assert.Equal(adsr.Source, compiler.Compile(session.Snapshot).Source);
        Set(EnvelopeGenerator.ModeKey, (int)EnvelopeMode.Loop);
        var loop = compiler.Compile(session.Snapshot);
        Assert.Empty(loop.RhythmicInputs);
        Assert.True(Active(EnvelopeGenerator.RestKey)); Assert.False(Active(EnvelopeGenerator.ReleaseKey));
        Assert.False(Socket(EnvelopeGenerator.GatePortKey)); Assert.False(Socket("release-modulation-in"));
        Assert.DoesNotContain(loop.Controls, c => c.Address == Address(env, EnvelopeGenerator.ReleaseKey));
        Assert.Equal(edges, session.Snapshot.GetGraph(graph).Connections.ToArray());
        session.Undo(); Assert.Equal(adsr.Source, compiler.Compile(session.Snapshot).Source);
        session.Redo(); Assert.Equal(loop.Source, compiler.Compile(session.Snapshot).Source);

        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, session.GraphId, []);
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(document)), BuiltInNodes.CreateEditorRegistry());
        Assert.Equal(loop.Source, compiler.Compile(restored.Snapshot).Source);
        var restoredEnvelope = restored.Snapshot.GetGraph(graph).GetNode(env.Id);
        Assert.Equal(1700, restoredEnvelope.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.ReleaseKey).BaseValue);
        Assert.Equal(0.7, restoredEnvelope.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.SustainKey).BaseValue);
        Set(EnvelopeGenerator.ModeKey, (int)EnvelopeMode.Ad);
        Assert.Equal(ad.Source, compiler.Compile(session.Snapshot).Source);
        Assert.Equal(edges, session.Snapshot.GetGraph(graph).Connections.ToArray());
    }

    [Fact]
    public void TypedLegacyAdDocumentAndPresetKeepTriggerChoiceAndOriginalIdentities()
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var env = NestedFaustPatch.Add(session, root, EnvelopeGenerator.NodeType);
        var gate = NestedFaustPatch.Add(session, root, GateGenerator.NodeType);
        session.Execute(new SetParameterValueCommand(root, Address(env, EnvelopeGenerator.TriggerSourceKey), 2));
        session.Execute(new ConnectNodesCommand(root, new(gate.Id, gate.Outputs[0].Id), new(env.Id, env.Inputs.Single(p => p.Key == EnvelopeGenerator.GatePortKey).Id)));
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, root, []);
        var saved = document.RootGraph.Nodes.Single(n => n.Id == env.Id);
        var legacy = saved with { Parameters = saved.Parameters.Where(p => EnvelopeGenerator.LegacyAdParameterKeys.Contains(p.Key)).ToArray(),
            Ports = saved.Ports.Where(p => !EnvelopeGenerator.ModeExtensionDefinitions.Any(d => p.Key == d.Key + "-modulation-in")).ToArray() };
        using var restored = serializer.Restore(document with { RootGraph = document.RootGraph with { Nodes = document.RootGraph.Nodes.Select(n => n.Id == env.Id ? legacy : n).ToArray() } }, registry);
        var current = restored.Snapshot.GetNode(env.Id);
        Assert.Equal(0, current.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.ModeKey).BaseValue);
        Assert.Equal(2, current.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.TriggerSourceKey).BaseValue);
        Assert.All(legacy.Ports, p => Assert.Contains(current.Inputs.Concat(current.Outputs), port => port.Id == p.Id));
        Assert.All(legacy.Parameters, p => Assert.Contains(current.Parameters, param => param.Id == p.Id && param.BaseValue == p.BaseValue));
        Assert.Equal(session.Snapshot.Connections, restored.Snapshot.Connections);
        var preset = new NodePreset(1, "legacy-typed-ad", EnvelopeGenerator.NodeType, "Legacy typed AD", "Custom", null, legacy.Parameters.ToDictionary(p => p.Key, p => p.BaseValue));
        new NodePresetValidator(registry).Validate(preset);
        Assert.Equal(0, preset.Parameters[EnvelopeGenerator.ModeKey]); Assert.Equal(2, preset.Parameters[EnvelopeGenerator.TriggerSourceKey]);
    }

    [Fact]
    public void TriggerAcceptsAudioAndModulationButOnlyOneSource()
    {
        var graph = new SignalGraph();
        var envelope = new EnvelopeGenerator(Guid.NewGuid());
        var osc = new OscillatorGenerator(Guid.NewGuid());
        var pitch = new PitchControlGenerator(Guid.NewGuid());
        graph.AddNode(envelope); graph.AddNode(osc); graph.AddNode(pitch);
        var trigger = envelope.Inputs.Single(p => p.Key == EnvelopeGenerator.TriggerPortKey);
        Assert.Equal(FanInPolicy.Single, trigger.FanIn);
        Assert.Equal(SignalType.Modulation, trigger.SignalType);
        Assert.DoesNotContain(envelope.Parameters, p => p.ModulationPortId == trigger.Id);
        Assert.All(envelope.Parameters.Where(p => p.Definition.CanModulate), p => { Assert.NotNull(p.ModulationPortId); Assert.False(p.Definition.RequiresRecompile); });
        var input = new PortAddress(envelope.Id, trigger.Id);
        var edge = graph.Connect(new PortAddress(osc.Id, osc.Outputs.Single().Id), input);
        Assert.Equal(1, edge.Amount);
        Assert.Throws<GraphValidationException>(() => graph.Connect(new PortAddress(pitch.Id, pitch.Outputs.Single().Id), input));
        graph.RemoveConnection(edge.Id);
        graph.Connect(new PortAddress(pitch.Id, pitch.Outputs.Single().Id), input, 0.5);
        Assert.Equal(SignalType.Modulation, envelope.Outputs.Single().SignalType);
    }

    [Fact]
    public void NestedImpactPersistsIndependentEnvelopesAndPredictsDestinationRanges()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var impact = NestedFaustPatch.Add(session, root, ImpactTemplate.PresetKey);
        var separateImpact = NestedFaustPatch.Add(session, root, ImpactTemplate.PresetKey);
        var master = NestedFaustPatch.Add(session, root, MasterOutput.NodeType);
        session.Execute(new ConnectNodesCommand(root, new PortAddress(impact.Id, impact.Outputs.Single().Id), new PortAddress(master.Id, master.Inputs.Single().Id)));
        var child = impact.Composite!.Graph;
        var envelope = child.Nodes.Where(n => n.TypeKey == EnvelopeGenerator.NodeType).ToArray();
        Assert.Equal(2, envelope.Length);
        Assert.Empty(child.Nodes.Select(n => n.Id).Intersect(separateImpact.Composite!.Graph.Nodes.Select(n => n.Id)));
        var voice = child.Nodes.Single(n => n.TypeKey == OscillatorGenerator.NodeType && n.Parameters.Single(p => p.Definition.Key == "frequency").BaseValue == 55);
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        var ranges = estimator.Estimate(session.Snapshot, child.Id);
        Assert.Equal(new ParameterRange(55, 355), ranges[Address(voice, "frequency")]);
        Assert.Equal(new ParameterRange(0, 0.8), ranges[Address(voice, "amplitude")]);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var before = compiler.Compile(session.Snapshot);
        var attack = Address(envelope[0], EnvelopeGenerator.AttackKey);
        session.Execute(new SetParameterValueCommand(child.Id, attack, 77));
        var edited = compiler.Compile(session.Snapshot);
        Assert.Equal(before.Source, edited.Source);
        Assert.Equal(77, edited.Controls.Single(c => c.Address == attack).InitialValue);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, root, []);
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(document)), registry);
        Assert.Equal(edited.Source, compiler.Compile(restored.Snapshot).Source);
        Assert.Equal(session.Snapshot.GetGraph(child.Id).Connections, restored.Snapshot.GetGraph(child.Id).Connections);
        Assert.Equal(77, compiler.Compile(restored.Snapshot).Controls.Single(c => c.Address == attack).InitialValue);
    }
}
