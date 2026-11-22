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
    public void ContinuousParameterEditPublishesEveryRevisionButUndoesWholeGesture()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var parameter = node.Parameters.Single(p => p.Definition.Key == "frequency");
        var address = new ParameterAddress(node.Id, parameter.Id);
        var gesture = Guid.NewGuid();
        var events = new List<ParameterChanged>();
        using var subscription = session.Subscribe<ParameterChanged>(events.Add);
        var revision = session.Snapshot.Revision;
        foreach (var value in new[] { 450, 480, 520 })
            session.Execute(new SetParameterValueCommand(session.GraphId, address, value), gesture);
        session.CompleteHistoryGesture(gesture);
        Assert.Equal(3, events.Count);
        Assert.Equal(revision + 3, session.Snapshot.Revision);
        double Current() => session.Snapshot.GetNode(node.Id).Parameters.Single(p => p.Id == parameter.Id).BaseValue;
        session.Undo();
        Assert.Equal(parameter.BaseValue, Current());
        session.Redo();
        Assert.Equal(520, Current());
        session.Undo();
        var unchanged = Guid.NewGuid();
        session.Execute(new SetParameterValueCommand(session.GraphId, address, 600), unchanged);
        session.Execute(new SetParameterValueCommand(session.GraphId, address, parameter.BaseValue), unchanged);
        session.CompleteHistoryGesture(unchanged);
        Assert.True(session.CanRedo);
        session.Redo();
        Assert.Equal(520, Current());
        session.Undo();
        session.Undo();
        Assert.Empty(session.Snapshot.Nodes);
    }

    [Fact]
    public void CancelPendingParameterGestureRestoresLiveValueWithoutAddingUndoOrClearingRedo()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var parameter = node.Parameters.Single(p => p.Definition.Key == "frequency");
        var address = new ParameterAddress(node.Id, parameter.Id);
        double Current() => session.Snapshot.GetNode(node.Id).Parameters.Single(p => p.Id == parameter.Id).BaseValue;
        session.Execute(new SetParameterValueCommand(session.GraphId, address, 520));
        session.Undo();
        var gesture = Guid.NewGuid();
        session.Execute(new SetParameterValueCommand(session.GraphId, address, 700), gesture);
        session.CancelParameterGesture(Guid.NewGuid());
        Assert.Equal(700, Current());
        var cancellation = session.CancelParameterGesture(gesture);
        Assert.IsType<ParameterChanged>(Assert.Single(cancellation.ChangeSet!.Events));
        Assert.Equal(parameter.BaseValue, Current());
        Assert.True(session.CanRedo);
        session.Redo();
        Assert.Equal(520, Current());
        session.Undo();
        session.Undo();
        Assert.Empty(session.Snapshot.Nodes);
    }

    [Fact]
    public void InterveningCommandsAndDifferentTargetsNeverMergeHistory()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var frequency = node.Parameters.Single(p => p.Definition.Key == "frequency");
        var amplitude = node.Parameters.Single(p => p.Definition.Key == "amplitude");
        var gesture = Guid.NewGuid();
        void Set(Guid id, double value, Guid? group = null) => session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(node.Id, id), value), group);
        double Current(Guid id) => session.Snapshot.GetNode(node.Id).Parameters.Single(p => p.Id == id).BaseValue;
        Set(frequency.Id, 500, gesture);
        Set(amplitude.Id, 0.75, gesture);
        Set(frequency.Id, 600, gesture);
        session.CompleteHistoryGesture(Guid.NewGuid()); // stale release cannot end this gesture
        Set(frequency.Id, 700, gesture);
        session.Undo(); // implicit completion handles interrupted gestures
        Assert.Equal(500, Current(frequency.Id));
        Assert.Equal(0.75, Current(amplitude.Id));
        session.Undo();
        Assert.Equal(amplitude.BaseValue, Current(amplitude.Id));
        session.Undo();
        Assert.Equal(frequency.BaseValue, Current(frequency.Id));
    }

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
