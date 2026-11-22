using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class CompositeGroupingTests
{
    [Fact]
    public void GroupPreservesIdentityWeightedAdaptersDormantEdgesAndHistory()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var s = new PatchSession(registry);
        var lfo = Add(s, OscillatorGenerator.NodeType);
        var carrier = Add(s, OscillatorGenerator.NodeType);
        var gain = Add(s, GainProcessor.NodeType);
        var output = Add(s, MasterOutput.NodeType);
        var incoming = Wire(s, lfo, carrier, "frequency-modulation-in", -25);
        var dormant = Wire(s, lfo, carrier, "pitch-modulation-in", 0);
        var internalEdge = Wire(s, carrier, gain, "audio-in", .7);
        var outgoing = Wire(s, gain, output, "audio-in", .4);
        var before = s.Snapshot;
        var history = new List<GraphChangeSet>();
        using var listener = s.Subscribe(history.Add);
        var change = s.Execute(new GroupNodesCommand(s.GraphId, [carrier.Id, gain.Id], "Voice", before.Revision));
        Assert.Empty(change.SubscriberErrors);
        Assert.Single(history);
        Assert.Equal(before.Revision + 1, s.Snapshot.Revision);
        var composite = change.ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
        var child = composite.Composite!.Graph;
        Assert.Equal("Voice", composite.Name);
        Assert.Equal(2, composite.Inputs.Count);
        Assert.All(composite.Inputs, port => Assert.Equal(SignalType.Audio, port.SignalType));
        Assert.Single(composite.Outputs);
        Assert.Equal(carrier.Parameters, child.GetNode(carrier.Id).Parameters);
        Assert.Equal(gain.Inputs, child.GetNode(gain.Id).Inputs);
        Assert.Equal(internalEdge, child.Connections.Single(e => e.Id == internalEdge.Id));
        Assert.Equal(incoming.Amount, child.Connections.Single(e => e.Id == incoming.Id).Amount);
        Assert.Equal(dormant.Amount, child.Connections.Single(e => e.Id == dormant.Id).Amount);
        Assert.Equal(outgoing.Amount, s.Snapshot.Connections.Single(e => e.Id == outgoing.Id).Amount);
        Assert.All(s.Snapshot.Connections.Where(e => e.Destination.NodeId == composite.Id), e => Assert.Equal(1, e.Amount));
        Assert.Equal(1, child.Connections.Single(e => e.Destination.NodeId == composite.Composite.OutputRackId).Amount);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var program = compiler.Compile(s.Snapshot);
        Assert.Contains(" * -25.0", program.Source);
        Assert.Contains(" * 0.4", program.Source);
        Assert.DoesNotContain(" * 0.0", program.Source);
        var serializer = new PatchDocumentSerializer();
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(serializer.Capture(s, 120, child.Id, []))), registry);
        Assert.Equal(program.Source, compiler.Compile(restored.Snapshot).Source);
        s.Undo();
        Assert.Equal(before.Connections, s.Snapshot.Connections);
        Assert.Equal(compiler.Compile(before).Source, compiler.Compile(s.Snapshot).Source);
        s.Redo();
        Assert.Equal(program.Source, compiler.Compile(s.Snapshot).Source);
    }

    [Fact]
    public void GroupRejectsNoncontiguousCycleRackMasterAndStaleRevisionAtomically()
    {
        using var s = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var a = Add(s, OscillatorGenerator.NodeType);
        var b = Add(s, GainProcessor.NodeType);
        var c = Add(s, GainProcessor.NodeType);
        var master = Add(s, MasterOutput.NodeType);
        Wire(s, a, b, "audio-in"); Wire(s, b, c, "audio-in"); Wire(s, c, master, "audio-in");
        var before = s.Snapshot;
        var notifications = 0;
        using var listener = s.Subscribe(_ => notifications++);
        Assert.Throws<GraphValidationException>(() => s.Execute(new GroupNodesCommand(s.GraphId, [a.Id, c.Id], "Cycle", before.Revision)));
        Assert.Throws<GraphValidationException>(() => s.Execute(new GroupNodesCommand(s.GraphId, [master.Id], "Master", before.Revision)));
        Assert.Throws<GraphValidationException>(() => s.Execute(new GroupNodesCommand(s.GraphId, [a.Id], "Stale", before.Revision - 1)));
        Assert.Same(before, s.Snapshot);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void ClockToTriggerAndNestedCompositeRetainTheirContracts()
    {
        using var s = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var clock = Add(s, ClockGenerator.NodeType);
        var gate = Add(s, GateGenerator.NodeType);
        Wire(s, clock, gate, GateGenerator.TriggerPortKey);
        var first = s.Execute(new GroupNodesCommand(s.GraphId, [gate.Id], "Gate", s.Snapshot.Revision)).ChangeSet!
            .Events.OfType<NodesGrouped>().Single().Composite;
        Assert.Equal(SignalType.Clock, Assert.Single(first.Inputs).SignalType);
        Assert.Equal(1, Assert.Single(first.Composite!.Graph.Connections).Amount);
        var second = s.Execute(new GroupNodesCommand(s.GraphId, [first.Id], "Nested", s.Snapshot.Revision)).ChangeSet!
            .Events.OfType<NodesGrouped>().Single().Composite;
        Assert.Equal(first.Composite.Graph.Id, second.Composite!.Graph.GetNode(first.Id).Composite!.Graph.Id);
        Assert.Throws<GraphValidationException>(() => s.Execute(new GroupNodesCommand(second.Composite.Graph.Id,
            [second.Composite.InputRackId], "Rack", s.Snapshot.Revision)));
    }

    private static GraphNodeSnapshot Add(PatchSession s, string type) => s.Execute(new AddNodeCommand(s.GraphId, type))
        .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
    private static SignalConnection Wire(PatchSession s, GraphNodeSnapshot from, GraphNodeSnapshot to, string key, double amount = 1) =>
        s.Execute(new ConnectNodesCommand(s.GraphId, new(from.Id, from.Outputs[0].Id),
            new(to.Id, to.Inputs.Single(port => port.Key == key).Id), amount)).ChangeSet!.Events.OfType<ConnectionCreated>().Single().Connection;
}
