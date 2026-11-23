using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class NodeDuplicationTests
{
    [Fact]
    public void LeafDuplicateCopiesSettingsButNotExternalEdgesAndIsOneUndoableEdit()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var source = Add(session, OscillatorGenerator.NodeType);
        var gain = Add(session, GainProcessor.NodeType);
        var master = Add(session, MasterOutput.NodeType);
        session.Execute(new SetParameterValueCommand(session.GraphId,
            new ParameterAddress(source.Id, source.Parameters[0].Id), 330));
        session.Execute(new SetNodeMutedCommand(session.GraphId, source.Id, true));
        session.Execute(new ConnectNodesCommand(session.GraphId,
            new PortAddress(source.Id, source.Outputs[0].Id),
            new PortAddress(gain.Id, gain.Inputs.Single(p => p.Key == "audio-in").Id), .6));
        session.Execute(new ConnectNodesCommand(session.GraphId,
            new PortAddress(gain.Id, gain.Outputs[0].Id),
            new PortAddress(master.Id, master.Inputs[0].Id)));
        var before = session.Snapshot;
        var original = before.GetNode(source.Id);
        var duplicateId = Guid.NewGuid();
        var events = new List<GraphChangeSet>();
        using var listener = session.Subscribe(events.Add);

        var committed = session.Execute(new DuplicateNodeCommand(session.GraphId, source.Id, duplicateId));

        Assert.Empty(committed.SubscriberErrors);
        Assert.Single(events);
        Assert.Equal(before.Revision + 1, session.Snapshot.Revision);
        var added = Assert.IsType<NodeAdded>(Assert.Single(committed.ChangeSet!.Events));
        Assert.Equal(duplicateId, added.Node.Id);
        var copy = session.Snapshot.GetNode(duplicateId);
        Assert.Equal(original.TypeKey, copy.TypeKey);
        Assert.True(copy.IsMuted);
        Assert.Equal(original.Parameters.Select(p => p.BaseValue), copy.Parameters.Select(p => p.BaseValue));
        Assert.Empty(original.Parameters.Select(p => p.Id).Intersect(copy.Parameters.Select(p => p.Id)));
        Assert.Empty(original.Inputs.Concat(original.Outputs).Select(p => p.Id)
            .Intersect(copy.Inputs.Concat(copy.Outputs).Select(p => p.Id)));
        Assert.Equal(before.Connections, session.Snapshot.Connections);
        Assert.DoesNotContain(session.Snapshot.Connections, edge =>
            edge.Source.NodeId == duplicateId || edge.Destination.NodeId == duplicateId);

        var serializer = new PatchDocumentSerializer();
        using var restored = serializer.Restore(
            serializer.Capture(session, 120, session.GraphId, []), registry);
        Assert.Equal(copy.Parameters.Select(p => p.BaseValue),
            restored.Snapshot.GetNode(duplicateId).Parameters.Select(p => p.BaseValue));

        session.Undo();
        Assert.Equal(before.Nodes.Select(n => n.Id), session.Snapshot.Nodes.Select(n => n.Id));
        Assert.Equal(before.Connections, session.Snapshot.Connections);
        session.Redo();
        Assert.Equal(copy.TypeKey, session.Snapshot.GetNode(duplicateId).TypeKey);
        Assert.Equal(copy.Parameters.Select(p => p.BaseValue),
            session.Snapshot.GetNode(duplicateId).Parameters.Select(p => p.BaseValue));
    }

    [Fact]
    public void CompositeDuplicateOwnsNewNestedGraphsRacksPortsAndInternalEdges()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var oscillator = Add(session, OscillatorGenerator.NodeType);
        var gain = Add(session, GainProcessor.NodeType);
        session.Execute(new ConnectNodesCommand(session.GraphId,
            new PortAddress(oscillator.Id, oscillator.Outputs[0].Id),
            new PortAddress(gain.Id, gain.Inputs.Single(p => p.Key == "audio-in").Id), .35));
        var composite = session.Execute(new GroupNodesCommand(session.GraphId,
            [oscillator.Id, gain.Id], "Voice", session.Snapshot.Revision))
            .ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
        var originalChild = composite.Composite!.Graph;
        var duplicateId = Guid.NewGuid();

        var result = session.Execute(new DuplicateNodeCommand(session.GraphId, composite.Id, duplicateId));
        var duplicated = result.ChangeSet!.ChangedGraph.GetNode(duplicateId);
        var copyChild = duplicated.Composite!.Graph;

        Assert.Equal("Voice", duplicated.Name);
        Assert.NotEqual(originalChild.Id, copyChild.Id);
        Assert.NotEqual(composite.Composite.InputRackId, duplicated.Composite.InputRackId);
        Assert.NotEqual(composite.Composite.OutputRackId, duplicated.Composite.OutputRackId);
        Assert.Equal(originalChild.Nodes.Select(n => n.TypeKey), copyChild.Nodes.Select(n => n.TypeKey));
        Assert.Empty(originalChild.Nodes.Select(n => n.Id).Intersect(copyChild.Nodes.Select(n => n.Id)));
        Assert.Equal(originalChild.Connections.Select(c => c.Amount), copyChild.Connections.Select(c => c.Amount));
        Assert.Empty(originalChild.Connections.Select(c => c.Id).Intersect(copyChild.Connections.Select(c => c.Id)));
        Assert.All(copyChild.Connections, edge =>
        {
            Assert.Contains(copyChild.Nodes, node => node.Id == edge.Source.NodeId &&
                node.Outputs.Any(port => port.Id == edge.Source.PortId));
            Assert.Contains(copyChild.Nodes, node => node.Id == edge.Destination.NodeId &&
                node.Inputs.Any(port => port.Id == edge.Destination.PortId));
        });
        Assert.Empty(session.Snapshot.Connections);
        Assert.Equal(2, session.Snapshot.Nodes.Count);
    }

    [Fact]
    public void NestedGraphDuplicateChangesOnlyItsOwningGraph()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var source = Add(session, OscillatorGenerator.NodeType);
        var wrapper = session.Execute(new GroupNodesCommand(session.GraphId, [source.Id],
            "Subpatch", session.Snapshot.Revision)).ChangeSet!.Events.OfType<NodesGrouped>().Single().Composite;
        var child = wrapper.Composite!.Graph;
        var cloneId = Guid.NewGuid();

        var result = session.Execute(new DuplicateNodeCommand(child.Id, source.Id, cloneId));

        Assert.Equal(child.Id, result.ChangeSet!.GraphId);
        Assert.Equal(1, session.Snapshot.Nodes.Count);
        var updatedChild = session.Snapshot.GetGraph(child.Id);
        Assert.Contains(updatedChild.Nodes, n => n.Id == source.Id);
        Assert.Contains(updatedChild.Nodes, n => n.Id == cloneId);
        Assert.Empty(updatedChild.Connections);
        session.Undo();
        Assert.DoesNotContain(session.Snapshot.GetGraph(child.Id).Nodes, n => n.Id == cloneId);
    }

    [Fact]
    public void InvalidDuplicatesLeaveRevisionAndNotificationsUntouched()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var source = Add(session, OscillatorGenerator.NodeType);
        var master = Add(session, MasterOutput.NodeType);
        var before = session.Snapshot;
        var count = 0;
        using var subscription = session.Subscribe(_ => count++);

        Assert.Throws<GraphValidationException>(() => session.Execute(
            new DuplicateNodeCommand(session.GraphId, master.Id, Guid.NewGuid())));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new DuplicateNodeCommand(session.GraphId, source.Id, source.Id)));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new DuplicateNodeCommand(session.GraphId, source.Id, Guid.Empty)));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new DuplicateNodeCommand(session.GraphId, Guid.NewGuid(), Guid.NewGuid())));
        Assert.Same(before, session.Snapshot);
        Assert.Equal(0, count);
    }

    private static GraphNodeSnapshot Add(PatchSession session, string type) =>
        session.Execute(new AddNodeCommand(session.GraphId, type)).ChangeSet!
            .Events.OfType<NodeAdded>().Single().Node;
}
