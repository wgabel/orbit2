using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class PatchHistoryTests
{
    [Fact]
    public void UndoRedoRestoresExactIdentitiesWithMonotonicRevisionAndNewEditClearsRedo()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        Assert.False(session.CanUndo);
        var oscillator = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var parameter = oscillator.Parameters.First();
        var address = new ParameterAddress(oscillator.Id, parameter.Id);
        session.Execute(new SetParameterValueCommand(session.GraphId, address, 220));
        var revision = session.Snapshot.Revision;
        var events = new List<GraphHistoryRestored>();
        using var subscription = session.Subscribe<GraphHistoryRestored>(events.Add, session.GraphId);
        session.Undo();
        Assert.Equal(revision + 1, session.Snapshot.Revision);
        Assert.Equal(parameter.BaseValue, session.Snapshot.GetNode(oscillator.Id).Parameters.First().BaseValue);
        Assert.True(Assert.Single(events).IsUndo);
        session.Redo();
        Assert.Equal(revision + 2, session.Snapshot.Revision);
        Assert.Equal(220, session.Snapshot.GetNode(oscillator.Id).Parameters.First().BaseValue);
        session.Undo();
        session.Undo();
        Assert.Empty(session.Snapshot.Nodes);
        session.Redo();
        Assert.Equal(parameter.Id, session.Snapshot.GetNode(oscillator.Id).Parameters.First().Id);
        Assert.Equal(oscillator.Outputs.Single().Id, session.Snapshot.GetNode(oscillator.Id).Outputs.Single().Id);
        session.Execute(new SetParameterValueCommand(session.GraphId, address, 330));
        Assert.False(session.CanRedo);
        Assert.Equal(330, session.Snapshot.GetNode(oscillator.Id).Parameters.First().BaseValue);
    }

    [Fact]
    public void RackRemovalUndoRestoresBothSidesAndEdgeIdentities()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var root = session.GraphId;
        var lfo = session.Execute(new AddNodeCommand(root, OscillatorGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var drone = session.Execute(new AddNodeCommand(root, CompositeNode.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var child = drone.Composite!.Graph.Id;
        var pitch = drone.Inputs[0].Id;
        session.Execute(new ConnectNodesCommand(root, new PortAddress(lfo.Id, lfo.Outputs.Single().Id), new PortAddress(drone.Id, pitch), -10));
        var before = session.Snapshot;
        session.Execute(new RemoveBoundaryPortCommand(child, PortDirection.Input, pitch));
        Assert.Empty(session.Snapshot.Connections);
        var undo = session.Undo().ChangeSet!;
        Assert.Equal(before.Connections.ToArray(), undo.Snapshot.Connections.ToArray());
        Assert.Equal(before.GetGraph(child).Connections.ToArray(), undo.Snapshot.GetGraph(child).Connections.ToArray());
        Assert.Equal(pitch, undo.Snapshot.GetNode(drone.Id).Inputs[0].Id);
        Assert.Equal(pitch, undo.Snapshot.GetGraph(child).GetNode(drone.Composite.InputRackId).Outputs[0].Id);
        Assert.Equal(new[] { root, child }.Order(), undo.Events.Select(fact => fact.GraphId).Order());
        session.Redo();
        Assert.Empty(session.Snapshot.Connections);
        Assert.DoesNotContain(session.Snapshot.GetNode(drone.Id).Inputs, port => port.Id == pitch);
        session.Execute(new RemoveNodeCommand(root, drone.Id));
        Assert.Null(session.Snapshot.FindGraph(child));
        session.Undo();
        Assert.NotNull(session.Snapshot.FindGraph(child));
    }
}
