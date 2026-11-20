using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Exercises atomic cross-canvas edits, event filtering, preserved identities
/// and compilation using only committed snapshots.</summary>
public sealed class BoundaryPortCommandTests
{
    [Fact]
    public void AddRenameAndReorderMirrorBothViewsWithoutChangingExistingDsp()
    {
        using var patch = new ConnectedDrone();
        var session = patch.Session;
        var before = session.Snapshot;
        var original = before.GetNode(patch.Drone.Id);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var source = compiler.Compile(before).Source;
        var pitchId = original.Inputs[0].Id;

        session.Execute(new AddBoundaryPortCommand(patch.ChildId, PortDirection.Input, "Cutoff", SignalType.Modulation, FanInPolicy.Sum));
        var result = session.Execute(new RenameBoundaryPortCommand(patch.ChildId, PortDirection.Input, pitchId, "Sweep"));
        var facts = result.ChangeSet!.Events.OfType<BoundaryPortChanged>().ToArray();
        Assert.Equal(2, facts.Length);
        Assert.All(facts, fact =>
        {
            Assert.Equal(pitchId, fact.Before!.Id);
            Assert.Equal(pitchId, fact.After!.Id);
            Assert.Equal("Sweep", fact.After.Name);
        });
        session.Execute(new MoveBoundaryPortCommand(patch.ChildId, PortDirection.Input, pitchId, 2));
        var next = session.Snapshot.GetNode(patch.Drone.Id);
        var rack = next.Composite!.Graph.GetNode(next.Composite.InputRackId);
        Assert.Equal(new[] { "Gain", "Cutoff", "Sweep" }, next.Inputs.Select(port => port.Name));
        Assert.Equal(next.Inputs.Select(port => port.Id), rack.Outputs.Select(port => port.Id));
        Assert.Equal(next.Inputs.Select(port => port.Name), rack.Outputs.Select(port => port.Name));
        Assert.Equal(FanInPolicy.Sum, next.Inputs[1].FanIn);
        Assert.Equal(FanInPolicy.Single, rack.Outputs[1].FanIn);
        Assert.Equal("Pitch", original.Inputs[0].Name);
        Assert.Equal(2, original.Inputs.Count);
        Assert.Equal(before.Connections.ToArray(), session.Snapshot.Connections.ToArray());
        Assert.Equal(original.Composite!.Graph.Connections.ToArray(), next.Composite.Graph.Connections.ToArray());
        Assert.Equal(source, compiler.Compile(session.Snapshot).Source);
    }

    [Fact]
    public void RemovingConnectedInputPublishesOneCommitWithBothLocalFactsAndEdges()
    {
        using var patch = new ConnectedDrone();
        var session = patch.Session;
        var before = session.Snapshot;
        var drone = before.GetNode(patch.Drone.Id);
        var pitch = drone.Inputs[0].Id;
        var all = new List<GraphChangeSet>();
        var parent = new List<GraphChangeSet>();
        var child = new List<GraphChangeSet>();
        var parentFacts = new List<BoundaryPortChanged>();
        var childFacts = new List<BoundaryPortChanged>();
        using var a = session.Subscribe(all.Add);
        using var b = session.Subscribe(parent.Add, session.GraphId);
        using var c = session.Subscribe(child.Add, patch.ChildId);
        using var d = session.Subscribe<BoundaryPortChanged>(parentFacts.Add, session.GraphId);
        using var e = session.Subscribe<BoundaryPortChanged>(childFacts.Add, patch.ChildId);
        var change = session.Execute(new RemoveBoundaryPortCommand(patch.ChildId, PortDirection.Input, pitch)).ChangeSet!;
        Assert.Equal(before.Revision + 1, change.Revision);
        Assert.Equal(patch.ChildId, change.GraphId);
        Assert.Same(change, Assert.Single(all));
        Assert.Same(change, Assert.Single(parent));
        Assert.Same(change, Assert.Single(child));
        Assert.Equal(PortDirection.Input, Assert.Single(parentFacts).Direction);
        Assert.Equal(PortDirection.Output, Assert.Single(childFacts).Direction);
        Assert.Null(parentFacts[0].After);
        Assert.Null(childFacts[0].After);
        Assert.Collection(change.Events,
            fact => Assert.Equal(session.GraphId, Assert.IsType<ConnectionRemoved>(fact).GraphId),
            fact => Assert.Equal(patch.ChildId, Assert.IsType<ConnectionRemoved>(fact).GraphId),
            fact => Assert.IsType<BoundaryPortChanged>(fact),
            fact => Assert.IsType<BoundaryPortChanged>(fact));
        Assert.Single(change.Snapshot.Connections);
        var next = change.Snapshot.GetNode(drone.Id);
        Assert.Single(next.Inputs);
        Assert.Single(next.Composite!.Graph.GetNode(next.Composite.InputRackId).Outputs);
        Assert.Equal(3, next.Composite.Graph.Connections.Count);
        Assert.Equal(2, before.Connections.Count);
        Assert.NotEqual(FaustGraphCompiler.CreateBuiltIn().Compile(before).Source,
            FaustGraphCompiler.CreateBuiltIn().Compile(change.Snapshot).Source);
    }

    [Fact]
    public void OutputRackUsesReceivingFanInAndRemovalDisconnectsMaster()
    {
        using var patch = new ConnectedDrone();
        var session = patch.Session;
        var audioId = patch.Drone.Outputs[0].Id;
        var created = session.Execute(new AddBoundaryPortCommand(patch.ChildId, PortDirection.Output, "Aux", SignalType.Audio, FanInPolicy.Sum));
        var drone = created.ChangeSet!.Snapshot.GetNode(patch.Drone.Id);
        var aux = drone.Outputs[1];
        var rack = drone.Composite!.Graph.GetNode(drone.Composite.OutputRackId);
        Assert.Equal(FanInPolicy.Single, aux.FanIn);
        Assert.Equal(FanInPolicy.Sum, rack.Inputs[1].FanIn);
        Assert.Equal(aux.Id, rack.Inputs[1].Id);
        session.Execute(new RenameBoundaryPortCommand(patch.ChildId, PortDirection.Output, aux.Id, "Auxiliary"));
        session.Execute(new MoveBoundaryPortCommand(patch.ChildId, PortDirection.Output, aux.Id, 0));
        var removed = session.Execute(new RemoveBoundaryPortCommand(patch.ChildId, PortDirection.Output, audioId)).ChangeSet!;
        Assert.Equal(2, removed.Events.OfType<ConnectionRemoved>().Count());
        Assert.Single(removed.Snapshot.Connections); // External LFO remains.
        Assert.Equal("Auxiliary", Assert.Single(removed.Snapshot.GetNode(drone.Id).Outputs).Name);
        Assert.Contains("process = 0.0;", FaustGraphCompiler.CreateBuiltIn().Compile(removed.Snapshot).Source);
    }

    [Fact]
    public void RejectedAndNoOpEditsDoNotCommitOrPublish()
    {
        using var patch = new ConnectedDrone();
        var session = patch.Session;
        var before = session.Snapshot;
        var pitch = patch.Drone.Inputs[0].Id;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        Assert.Throws<GraphValidationException>(() => session.Execute(new AddBoundaryPortCommand(session.GraphId, PortDirection.Input, "No rack", SignalType.Audio)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new AddBoundaryPortCommand(patch.ChildId, PortDirection.Input, "pitch", SignalType.Audio)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new RenameBoundaryPortCommand(patch.ChildId, PortDirection.Input, pitch, "Gain")));
        Assert.Throws<GraphValidationException>(() => session.Execute(new RenameBoundaryPortCommand(patch.ChildId, PortDirection.Input, pitch, " ")));
        Assert.Throws<GraphValidationException>(() => session.Execute(new MoveBoundaryPortCommand(patch.ChildId, PortDirection.Input, pitch, 99)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new RemoveBoundaryPortCommand(patch.ChildId, PortDirection.Output, pitch)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new AddBoundaryPortCommand(patch.ChildId, PortDirection.Input, "Bad", SignalType.Audio, ChannelCount: 0)));
        Assert.False(session.Execute(new RenameBoundaryPortCommand(patch.ChildId, PortDirection.Input, pitch, "Pitch")).Changed);
        Assert.False(session.Execute(new MoveBoundaryPortCommand(patch.ChildId, PortDirection.Input, pitch, 0)).Changed);
        Assert.Same(before, session.Snapshot);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void NestedBoundaryPublishesOnlyToItsImmediateParentAndChild()
    {
        using var patch = new ConnectedDrone();
        var session = patch.Session;
        var nested = Add(session, patch.ChildId, CompositeNode.NodeType);
        var nestedId = nested.Composite!.Graph.Id;
        var rootCalls = 0;
        var parentCalls = 0;
        var childCalls = 0;
        using var a = session.Subscribe(_ => rootCalls++, session.GraphId);
        using var b = session.Subscribe(_ => parentCalls++, patch.ChildId);
        using var c = session.Subscribe(_ => childCalls++, nestedId);
        session.Execute(new AddBoundaryPortCommand(nestedId, PortDirection.Input, "Nested", SignalType.Modulation));
        Assert.Equal(0, rootCalls);
        Assert.Equal(1, parentCalls);
        Assert.Equal(1, childCalls);
        Assert.Equal("Nested", session.Snapshot.GetGraph(patch.ChildId).GetNode(nested.Id).Inputs[^1].Name);
    }

    private static GraphNodeSnapshot Add(IPatchSession session, Guid graphId, string type) =>
        session.Execute(new AddNodeCommand(graphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    private sealed class ConnectedDrone : IDisposable
    {
        public PatchSession Session { get; } = new(BuiltInNodes.CreateEditorRegistry());
        public GraphNodeSnapshot Drone { get; }
        public Guid ChildId => Drone.Composite!.Graph.Id;

        public ConnectedDrone()
        {
            var root = Session.GraphId;
            var lfo = Add(Session, root, OscillatorGenerator.NodeType);
            Drone = Add(Session, root, CompositeNode.NodeType);
            var master = Add(Session, root, MasterOutput.NodeType);
            Session.Execute(new ConnectNodesCommand(root, new PortAddress(lfo.Id, lfo.Outputs[0].Id),
                new PortAddress(Drone.Id, Drone.Inputs[0].Id), 100));
            Session.Execute(new ConnectNodesCommand(root, new PortAddress(Drone.Id, Drone.Outputs[0].Id),
                new PortAddress(master.Id, master.Inputs[0].Id)));
        }

        public void Dispose() => Session.Dispose();
    }
}
