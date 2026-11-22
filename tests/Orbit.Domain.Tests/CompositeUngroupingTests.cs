using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class CompositeUngroupingTests
{
    [Fact]
    public void ExpandsEditedCurrentRoutingAndComposesWeightsWithOneHistoryEntry()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var s = new PatchSession(registry);
        var root = s.GraphId;
        var lfo = Add(s, root, OscillatorGenerator.NodeType);
        var carrier = Add(s, root, OscillatorGenerator.NodeType);
        var gain = Add(s, root, GainProcessor.NodeType);
        var output = Add(s, root, MasterOutput.NodeType);
        var inEdge = Wire(s, root, lfo, carrier, "frequency-modulation-in", -25);
        var dormant = Wire(s, root, lfo, carrier, "pitch-modulation-in", 0);
        var inner = Wire(s, root, carrier, gain, "audio-in", .7);
        var outEdge = Wire(s, root, gain, output, "audio-in", .4);
        var wrapper = Group(s, root, carrier.Id, gain.Id);
        var child = wrapper.Composite!.Graph.Id;
        var incoming = s.Snapshot.Connections.Single(edge => edge.Destination.PortId == wrapper.Inputs[0].Id);
        var outgoing = s.Snapshot.GetGraph(child).Connections.Single(edge => edge.Destination.NodeId == wrapper.Composite.OutputRackId);
        s.Execute(new SetConnectionAmountCommand(root, incoming.Id, 2));
        s.Execute(new SetConnectionAmountCommand(child, outgoing.Id, -.5));
        var parameter = carrier.Parameters.First();
        s.Execute(new SetParameterValueCommand(child, new(carrier.Id, parameter.Id), 330));
        var edited = s.Snapshot;
        var changes = new List<GraphChangeSet>();
        using var listener = s.Subscribe(changes.Add);
        var result = s.Execute(new UngroupCompositeCommand(root, wrapper.Id, edited.Revision));
        Assert.Empty(result.SubscriberErrors);
        Assert.Single(changes);
        Assert.IsType<CompositeUngrouped>(Assert.Single(result.ChangeSet!.Events));
        Assert.Equal(edited.Revision + 1, s.Snapshot.Revision);
        Assert.Null(s.Snapshot.FindGraph(child));
        Assert.Equal(inEdge with { Amount = -50 }, s.Snapshot.Connections.Single(edge => edge.Id == inEdge.Id));
        Assert.Equal(dormant, s.Snapshot.Connections.Single(edge => edge.Id == dormant.Id));
        Assert.Equal(inner, s.Snapshot.Connections.Single(edge => edge.Id == inner.Id));
        Assert.Equal(outEdge with { Amount = -.2 }, s.Snapshot.Connections.Single(edge => edge.Id == outEdge.Id));
        Assert.Equal(330, s.Snapshot.GetNode(carrier.Id).Parameters.First().BaseValue);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var expandedSource = compiler.Compile(s.Snapshot).Source;
        Assert.Contains(" * -50.0", expandedSource);
        s.Undo();
        Assert.Equal(compiler.Compile(edited).Source, compiler.Compile(s.Snapshot).Source);
        s.Redo();
        Assert.Equal(expandedSource, compiler.Compile(s.Snapshot).Source);
        var serializer = new PatchDocumentSerializer();
        using var restored = serializer.Restore(serializer.Capture(s, 120, root, []), registry);
        Assert.Equal(expandedSource, compiler.Compile(restored.Snapshot).Source);
    }

    [Fact]
    public void FanOutAndRackPassthroughKeepReceiverIdsAndEventAdapter()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Router",
            [new ExposedPort(Guid.NewGuid(), "Clock", SignalType.Clock)],
            [new ExposedPort(Guid.NewGuid(), "Trigger", SignalType.Trigger)]));
        using var s = new PatchSession(registry);
        var root = s.GraphId;
        var source = Add(s, root, ClockGenerator.NodeType);
        var wrapper = Add(s, root, CompositeNode.NodeType);
        var gate = Add(s, root, GateGenerator.NodeType);
        var other = Add(s, root, GateGenerator.NodeType);
        var child = wrapper.Composite!;
        var disconnected = Add(s, child.Graph.Id, OscillatorGenerator.NodeType);
        Connect(s, root, new(source.Id, source.Outputs[0].Id), new(wrapper.Id, wrapper.Inputs[0].Id));
        Connect(s, child.Graph.Id, new(child.InputRackId, wrapper.Inputs[0].Id), new(child.OutputRackId, wrapper.Outputs[0].Id));
        var first = Connect(s, root, new(wrapper.Id, wrapper.Outputs[0].Id), new(gate.Id, gate.Inputs.Single(p => p.Key == GateGenerator.TriggerPortKey).Id));
        var second = Connect(s, root, new(wrapper.Id, wrapper.Outputs[0].Id), new(other.Id, other.Inputs.Single(p => p.Key == GateGenerator.TriggerPortKey).Id));
        s.Execute(new UngroupCompositeCommand(root, wrapper.Id, s.Snapshot.Revision));
        Assert.Equal(first with { Source = new(source.Id, source.Outputs[0].Id) }, s.Snapshot.Connections[0]);
        Assert.Equal(second with { Source = new(source.Id, source.Outputs[0].Id) }, s.Snapshot.Connections[1]);
        Assert.Equal(disconnected.Id, s.Snapshot.GetNode(disconnected.Id).Id);
    }

    [Fact]
    public void NestedCompositeMovesAsAUnitAndRejectsMutedOrStaleExpansion()
    {
        using var s = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var root = s.GraphId;
        var osc = Add(s, root, OscillatorGenerator.NodeType);
        var nested = Group(s, root, osc.Id);
        var outer = Group(s, root, nested.Id);
        s.Execute(new SetNodeMutedCommand(root, outer.Id, true));
        var before = s.Snapshot;
        Assert.Throws<GraphValidationException>(() => s.Execute(new UngroupCompositeCommand(root, outer.Id, before.Revision)));
        Assert.Same(before, s.Snapshot);
        s.Execute(new SetNodeMutedCommand(root, outer.Id, false));
        before = s.Snapshot;
        Assert.Throws<GraphValidationException>(() => s.Execute(new UngroupCompositeCommand(root, outer.Id, before.Revision - 1)));
        Assert.Same(before, s.Snapshot);
        s.Execute(new UngroupCompositeCommand(root, outer.Id, before.Revision));
        Assert.Equal(nested.Composite!.Graph.Id, s.Snapshot.GetNode(nested.Id).Composite!.Graph.Id);
    }

    [Fact]
    public void RefusesOneSidedAndSummedBoundaryWithoutMutation()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Sum",
            [new ExposedPort(Guid.NewGuid(), "Input", SignalType.Audio, FanIn: FanInPolicy.Sum)], []));
        using var s = new PatchSession(registry);
        var root = s.GraphId;
        var a = Add(s, root, OscillatorGenerator.NodeType);
        var b = Add(s, root, OscillatorGenerator.NodeType);
        var wrapper = Add(s, root, CompositeNode.NodeType);
        var child = wrapper.Composite!;
        var gain = Add(s, child.Graph.Id, GainProcessor.NodeType);
        Connect(s, child.Graph.Id, new(child.InputRackId, wrapper.Inputs[0].Id), new(gain.Id, gain.Inputs.Single(p => p.Key == "audio-in").Id));
        var before = s.Snapshot;
        Assert.Contains("only one side", Assert.Throws<GraphValidationException>(() => s.Execute(new UngroupCompositeCommand(root, wrapper.Id, before.Revision))).Message);
        Assert.Same(before, s.Snapshot);
        Connect(s, root, new(a.Id, a.Outputs[0].Id), new(wrapper.Id, wrapper.Inputs[0].Id));
        Connect(s, root, new(b.Id, b.Outputs[0].Id), new(wrapper.Id, wrapper.Inputs[0].Id));
        before = s.Snapshot;
        Assert.Contains("mixes several", Assert.Throws<GraphValidationException>(() => s.Execute(new UngroupCompositeCommand(root, wrapper.Id, before.Revision))).Message);
        Assert.Same(before, s.Snapshot);
    }

    private static GraphNodeSnapshot Add(PatchSession s, Guid graph, string type) => s.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
    private static GraphNodeSnapshot Group(PatchSession s, Guid graph, params Guid[] ids) => s.Execute(new GroupNodesCommand(graph, ids, "Group", s.Snapshot.Revision)).ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
    private static SignalConnection Wire(PatchSession s, Guid graph, GraphNodeSnapshot from, GraphNodeSnapshot to, string key, double amount = 1) => Connect(s, graph, new(from.Id, from.Outputs[0].Id), new(to.Id, to.Inputs.Single(port => port.Key == key).Id), amount);
    private static SignalConnection Connect(PatchSession s, Guid graph, PortAddress from, PortAddress to, double amount = 1) => s.Execute(new ConnectNodesCommand(graph, from, to, amount)).ChangeSet!.Events.OfType<ConnectionCreated>().Single().Connection;
}
