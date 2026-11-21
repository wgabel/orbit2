using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Application.Presets;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class RhythmicPortsTests
{
    [Fact]
    public void EventRoutesAreMonoUnitySingleSourceAndDisallowImplicitContinuousConversion()
    {
        var graph = new SignalGraph();
        var pulse = new SignalToTriggerGenerator(Guid.NewGuid());
        var second = new SignalToTriggerGenerator(Guid.NewGuid());
        var gate = new SignalToGateGenerator(Guid.NewGuid());
        var oscillator = new OscillatorGenerator(Guid.NewGuid());
        var envelope = new EnvelopeGenerator(Guid.NewGuid());
        var other = new EnvelopeGenerator(Guid.NewGuid());
        var clock = new CompositeNode(Guid.NewGuid(), "Clock source", [], [new(Guid.NewGuid(), "Clock", SignalType.Clock)]);
        foreach (var node in new Orbit.Domain.Nodes.ISignalNode[] { pulse, second, gate, oscillator, envelope, other, clock }) graph.AddNode(node);
        var trigger = new PortAddress(envelope.Id, envelope.Inputs.Single(p => p.Key == EnvelopeGenerator.EventTriggerPortKey).Id);
        var otherTrigger = new PortAddress(other.Id, other.Inputs.Single(p => p.Key == EnvelopeGenerator.EventTriggerPortKey).Id);
        var output = new PortAddress(pulse.Id, pulse.Outputs.Single().Id);
        Assert.Throws<GraphValidationException>(() => graph.Connect(output, trigger, 0.5));
        Assert.Throws<GraphValidationException>(() => graph.Connect(new(oscillator.Id, oscillator.Outputs[0].Id), trigger));
        Assert.Throws<GraphValidationException>(() => graph.Connect(new(gate.Id, gate.Outputs[0].Id), trigger));
        Assert.Throws<GraphValidationException>(() => graph.Connect(output, new(envelope.Id, envelope.Inputs.Single(p => p.Key == EnvelopeGenerator.TriggerPortKey).Id)));
        var edge = graph.Connect(output, trigger);
        graph.Connect(output, otherTrigger); // fan-out, no fan-in
        Assert.Throws<GraphValidationException>(() => graph.SetConnectionAmount(edge.Id, 0));
        Assert.Throws<GraphValidationException>(() => graph.SetConnectionAmount(edge.Id, -1));
        Assert.Throws<GraphValidationException>(() => graph.Connect(new(second.Id, second.Outputs[0].Id), trigger));
        graph.RemoveConnection(edge.Id);
        graph.Connect(new(clock.Id, clock.Outputs[0].Id), trigger); // a clock pulse intentionally satisfies Trigger
        Assert.Throws<GraphValidationException>(() => new CompositeNode(Guid.NewGuid(), "Bad", [new(Guid.NewGuid(), "Sum", SignalType.Gate, FanIn: FanInPolicy.Sum)], []));
        Assert.Throws<GraphValidationException>(() => new CompositeNode(Guid.NewGuid(), "Bad", [], [new(Guid.NewGuid(), "Stereo", SignalType.Clock, 2)]));
    }

    [Fact]
    public void DormantRoutesAreRetainedAndLegacyEnvelopeDocumentsAndPresetsDefaultToContinuous()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var osc = NestedFaustPatch.Add(session, root, OscillatorGenerator.NodeType);
        var pulse = NestedFaustPatch.Add(session, root, SignalToTriggerGenerator.NodeType);
        var envelope = NestedFaustPatch.Add(session, root, EnvelopeGenerator.NodeType);
        var carrier = NestedFaustPatch.Add(session, root, OscillatorGenerator.NodeType);
        var master = NestedFaustPatch.Add(session, root, MasterOutput.NodeType);
        void Wire(GraphNodeSnapshot from, GraphNodeSnapshot to, string key) => session.Execute(new ConnectNodesCommand(root,
            new(from.Id, from.Outputs.Single().Id), new(to.Id, to.Inputs.Single(p => p.Key == key).Id)));
        Wire(osc, pulse, RhythmicAdapter.InputKey);
        Wire(osc, envelope, EnvelopeGenerator.TriggerPortKey);
        Wire(pulse, envelope, EnvelopeGenerator.EventTriggerPortKey);
        Wire(envelope, carrier, "amplitude-modulation-in"); Wire(carrier, master, "audio-in");
        var selector = new ParameterAddress(envelope.Id, envelope.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.TriggerSourceKey).Id);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var direct = compiler.Compile(session.Snapshot);
        Assert.DoesNotContain($"n_{pulse.Id:N} =", direct.Source);
        session.Execute(new SetParameterValueCommand(root, selector, 1));
        var typed = compiler.Compile(session.Snapshot);
        Assert.Contains($"n_{pulse.Id:N} =", typed.Source);
        Assert.Equal(5, session.Snapshot.Connections.Count);
        Assert.False(session.Snapshot.GetNode(envelope.Id).IsInputActive(envelope.Inputs.Single(p => p.Key == EnvelopeGenerator.TriggerPortKey).Id));
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, root, []);
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(document)), registry);
        Assert.Equal(typed.Source, compiler.Compile(restored.Snapshot).Source);
        session.Undo(); Assert.Equal(direct.Source, compiler.Compile(session.Snapshot).Source);
        var saved = document.RootGraph.Nodes.Single(n => n.Id == envelope.Id);
        var legacy = saved with { Parameters = saved.Parameters.Where(p => p.Key != EnvelopeGenerator.TriggerSourceKey).ToArray(),
            Ports = saved.Ports.Where(p => p.Key is not (EnvelopeGenerator.EventTriggerPortKey or EnvelopeGenerator.GatePortKey)).ToArray() };
        var legacyRoot = document.RootGraph with { Nodes = document.RootGraph.Nodes.Select(n => n.Id == envelope.Id ? legacy : n).ToArray(),
            Connections = document.RootGraph.Connections.Where(e => e.Source.NodeId != pulse.Id).ToArray() };
        using var migrated = serializer.Restore(document with { RootGraph = legacyRoot }, registry);
        var current = migrated.Snapshot.GetNode(envelope.Id);
        Assert.Equal(0, current.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.TriggerSourceKey).BaseValue);
        Assert.All(legacy.Ports, p => Assert.Contains(current.Inputs.Concat(current.Outputs), port => port.Id == p.Id));
        Assert.All(legacy.Parameters, p => Assert.Contains(current.Parameters, param => param.Id == p.Id));
        var oldPreset = new NodePreset(1, "old-ad", EnvelopeGenerator.NodeType, "Old AD", "Custom", null,
            legacy.Parameters.ToDictionary(p => p.Key, p => p.BaseValue));
        new NodePresetValidator(registry).Validate(oldPreset);
        Assert.Equal(0, oldPreset.Parameters[EnvelopeGenerator.TriggerSourceKey]);
    }

    [Fact]
    public void TypedRackEditsMirrorAndRoundTripRoutesWithUnityAmounts()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Rhythm", [], []));
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var composite = NestedFaustPatch.Add(session, root, CompositeNode.NodeType);
        var child = composite.Composite!.Graph.Id;
        session.Execute(new AddBoundaryPortCommand(child, PortDirection.Input, "Pulse", SignalType.Trigger));
        session.Execute(new AddBoundaryPortCommand(child, PortDirection.Output, "Pulse", SignalType.Trigger));
        composite = session.Snapshot.GetNode(composite.Id);
        var input = composite.Inputs.Single(); var output = composite.Outputs.Single();
        var source = NestedFaustPatch.Add(session, root, SignalToTriggerGenerator.NodeType);
        var target = NestedFaustPatch.Add(session, root, EnvelopeGenerator.NodeType);
        session.Execute(new ConnectNodesCommand(root, new(source.Id, source.Outputs[0].Id), new(composite.Id, input.Id)));
        session.Execute(new ConnectNodesCommand(child, new(composite.Composite!.InputRackId, input.Id), new(composite.Composite.OutputRackId, output.Id)));
        session.Execute(new ConnectNodesCommand(root, new(composite.Id, output.Id), new(target.Id, target.Inputs.Single(p => p.Key == EnvelopeGenerator.EventTriggerPortKey).Id)));
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, root, []);
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(document)), registry);
        var restoredComposite = restored.Snapshot.GetNode(composite.Id);
        Assert.Equal(input, restoredComposite.Inputs.Single()); Assert.Equal(output, restoredComposite.Outputs.Single());
        Assert.Equal(session.Snapshot.Connections, restored.Snapshot.Connections);
        Assert.Equal(session.Snapshot.GetGraph(child).Connections, restored.Snapshot.GetGraph(child).Connections);
        Assert.Equal(SignalType.Trigger, restored.Snapshot.GetGraph(child).GetNode(composite.Composite.OutputRackId).Inputs.Single().SignalType);
    }
}
