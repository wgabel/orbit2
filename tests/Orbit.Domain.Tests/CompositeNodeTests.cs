using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Exercises boundary identity, child ownership and snapshot isolation without DSP or UI.</summary>
public sealed class CompositeNodeTests
{
    [Fact]
    public void BoundarySocketsMirrorTheirRacksAndSupportIndependentRouting()
    {
        var pitch = new ExposedPort(Guid.NewGuid(), "Pitch", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var audio = new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio);
        var drone = new CompositeNode(Guid.NewGuid(), "Drone", [pitch], [audio]);
        var root = new SignalGraph();
        var lfo = new SineGenerator(Guid.NewGuid());
        var master = new MasterOutput(Guid.NewGuid());
        root.AddNode(drone);
        root.AddNode(lfo);
        root.AddNode(master);
        var carrier = new SineGenerator(Guid.NewGuid());
        drone.InternalGraph.AddNode(carrier);

        Assert.Equal(pitch.Id, Assert.Single(drone.Inputs).Id);
        Assert.Equal(pitch.Id, Assert.Single(drone.InputRack.Outputs).Id);
        Assert.Equal(PortDirection.Input, drone.Inputs[0].Direction);
        Assert.Equal(PortDirection.Output, drone.InputRack.Outputs[0].Direction);
        Assert.Equal(FanInPolicy.Sum, drone.Inputs[0].FanIn);
        Assert.Equal(audio.Id, Assert.Single(drone.Outputs).Id);
        Assert.Equal(audio.Id, Assert.Single(drone.OutputRack.Inputs).Id);
        Assert.Equal(PortDirection.Input, drone.OutputRack.Inputs[0].Direction);
        Assert.Equal(PortDirection.Output, drone.Outputs[0].Direction);
        Assert.Equal(2, root.Connect(new PortAddress(lfo.Id, lfo.Outputs[0].Id), new PortAddress(drone.Id, pitch.Id), 2).Amount);
        root.Connect(new PortAddress(drone.Id, audio.Id), new PortAddress(master.Id, master.Inputs[0].Id));
        drone.InternalGraph.Connect(new PortAddress(drone.InputRack.Id, pitch.Id), new PortAddress(carrier.Id, carrier.Inputs[0].Id));
        drone.InternalGraph.Connect(new PortAddress(carrier.Id, carrier.Outputs[0].Id), new PortAddress(drone.OutputRack.Id, audio.Id));
        Assert.Equal(2, root.Connections.Count);
        Assert.Equal(2, drone.InternalGraph.Connections.Count);
        Assert.Throws<GraphValidationException>(() => root.Connect(
            new PortAddress(lfo.Id, lfo.Outputs[0].Id), new PortAddress(carrier.Id, carrier.Inputs[0].Id)));
    }

    [Fact]
    public void BoundaryDefinitionsAreCopiedAndValidated()
    {
        var port = new ExposedPort(Guid.NewGuid(), "Pitch", SignalType.Modulation);
        var inputs = new List<ExposedPort> { port };
        var drone = new CompositeNode(Guid.NewGuid(), "Drone", inputs, []);
        inputs.Clear();
        Assert.Equal(port, Assert.Single(drone.InputRack.Ports));
        Assert.True(Assert.IsAssignableFrom<IList<ExposedPort>>(drone.InputRack.Ports).IsReadOnly);
        Assert.Throws<GraphValidationException>(() => new CompositeNode(Guid.NewGuid(), "Invalid", [port], [port]));
        Assert.Throws<GraphValidationException>(() => new CompositeNode(Guid.NewGuid(), "Invalid", [port, port with { Id = Guid.NewGuid(), Name = "pitch" }], []));
        Assert.Throws<GraphValidationException>(() => new CompositeNode(Guid.NewGuid(), "Invalid", [port with { ChannelCount = 0 }], []));
    }

    [Fact]
    public void ProtectsRacksAndRejectsRecursiveOrSharedGraphOwnership()
    {
        var parent = EmptyComposite();
        var child = EmptyComposite();
        var root = new SignalGraph();

        Assert.Throws<GraphValidationException>(() => parent.InternalGraph.AddNode(parent));
        parent.InternalGraph.AddNode(child);
        Assert.Throws<GraphValidationException>(() => child.InternalGraph.AddNode(parent));
        root.AddNode(parent);
        Assert.Throws<GraphValidationException>(() => new SignalGraph().AddNode(parent));
        Assert.Throws<GraphValidationException>(() => root.AddNode(child.InputRack));
        Assert.Throws<GraphValidationException>(() => child.InternalGraph.RemoveNode(child.OutputRack.Id));
        Assert.Throws<GraphValidationException>(() => child.InternalGraph.AddNode(new MasterOutput(Guid.NewGuid())));
        Assert.Equal(2, child.InternalGraph.Nodes.Count);
        Assert.Equal(3, parent.InternalGraph.Nodes.Count);
        Assert.Single(root.Nodes);
    }

    [Fact]
    public void IndependentCompositesCannotAccidentallyShareMutableGeneratorState()
    {
        var first = EmptyComposite();
        var second = EmptyComposite();
        var oscillator = new SineGenerator(Guid.NewGuid());
        var other = new SineGenerator(Guid.NewGuid());
        first.InternalGraph.AddNode(oscillator);
        second.InternalGraph.AddNode(other);
        Assert.NotEqual(first.InternalGraph.Id, second.InternalGraph.Id);
        Assert.Throws<GraphValidationException>(() => second.InternalGraph.AddNode(oscillator));
        var address = new ParameterAddress(oscillator.Id, oscillator.Parameters[0].Id);
        first.InternalGraph.SetParameterValue(address, 220);
        Assert.Equal(440, other.Parameters[0].BaseValue);
    }

    [Fact]
    public void RootSessionSnapshotsCaptureNestedGraphAndRackIdentities()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id =>
        {
            var outer = new CompositeNode(id, "Drone", [], []);
            outer.InternalGraph.AddNode(EmptyComposite());
            return outer;
        });
        using var session = new PatchSession(registry);
        var added = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType));
        var before = session.Snapshot;
        var outer = Assert.Single(before.Nodes);
        Assert.NotNull(outer.Composite);
        var child = outer.Composite.Graph;
        var nested = child.Nodes.Single(node => node.Composite is not null).Composite!;
        Assert.Equal(before.Revision, child.Revision);
        Assert.Equal(before.Revision, nested.Graph.Revision);
        Assert.Same(nested.Graph, before.GetGraph(nested.Graph.Id));
        Assert.Equal(InputRack.NodeType, child.GetNode(outer.Composite.InputRackId).TypeKey);
        Assert.Equal(OutputRack.NodeType, child.GetNode(outer.Composite.OutputRackId).TypeKey);
        var fact = Assert.IsType<NodeAdded>(Assert.Single(added.ChangeSet!.Events));
        Assert.Equal(child.Id, fact.Node.Composite!.Graph.Id);
        session.Execute(new RemoveNodeCommand(session.GraphId, outer.Id));
        Assert.Throws<GraphValidationException>(() => session.Snapshot.GetGraph(child.Id));
        Assert.Same(child, before.GetGraph(child.Id));
    }

    private static CompositeNode EmptyComposite() => new(Guid.NewGuid(), "Composite", [], []);
}
