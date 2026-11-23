using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class DuplicateNodeTests
{
    [Fact]
    public void LeafDuplicateKeepsParametersAndMuteButNotExternalConnections()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var graphId = session.GraphId;
        var source = Add(session, graphId, OscillatorGenerator.NodeType);
        var target = Add(session, graphId, OscillatorGenerator.NodeType);
        var frequency = source.Parameters.Single(p => p.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(graphId, new ParameterAddress(source.Id, frequency.Id), 233));
        session.Execute(new SetNodeMutedCommand(graphId, source.Id, true));
        session.Execute(new ConnectNodesCommand(graphId,
            new PortAddress(source.Id, source.Outputs.Single().Id),
            new PortAddress(target.Id, target.Inputs.Single(p => p.Key == "frequency-modulation-in").Id)));
        var before = session.Snapshot;
        var copyId = Guid.NewGuid();

        var result = session.Execute(new DuplicateNodeCommand(graphId, source.Id, copyId));
        var added = Assert.IsType<NodeAdded>(Assert.Single(result.ChangeSet!.Events));
        var copy = added.Node;
        var original = before.GetNode(source.Id);

        Assert.Equal(copyId, copy.Id);
        Assert.Equal(original.TypeKey, copy.TypeKey);
        Assert.True(copy.IsMuted);
        Assert.Equal(original.Parameters.Select(p => (p.Definition.Key, p.BaseValue)),
            copy.Parameters.Select(p => (p.Definition.Key, p.BaseValue)));
        Assert.Empty(original.Parameters.Select(p => p.Id).Intersect(copy.Parameters.Select(p => p.Id)));
        Assert.Empty(original.Inputs.Concat(original.Outputs).Select(p => p.Id)
            .Intersect(copy.Inputs.Concat(copy.Outputs).Select(p => p.Id)));
        Assert.Equal(before.Connections.ToArray(), session.Snapshot.Connections.ToArray());
        Assert.True(result.ChangeSet.TopologyChanged);

        session.Undo();
        Assert.Equal(before.Nodes.Count, session.Snapshot.Nodes.Count);
        Assert.DoesNotContain(session.Snapshot.Nodes, node => node.Id == copyId);
        session.Redo();
        Assert.Equal(copy.Parameters.Select(p => p.Id),
            session.Snapshot.GetNode(copyId).Parameters.Select(p => p.Id));
    }

    [Fact]
    public void CompositeDuplicateDeepCopiesNestedGraphsAndInternalCables()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var root = session.GraphId;
        var original = Add(session, root, CompositeNode.NodeType);
        var originalChildId = original.Composite!.Graph.Id;
        var nested = Add(session, originalChildId, CompositeNode.NodeType);
        var originalOscillator = original.Composite.Graph.Nodes.Single(n => n.TypeKey == OscillatorGenerator.NodeType);
        var frequency = originalOscillator.Parameters.Single(p => p.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(originalChildId,
            new ParameterAddress(originalOscillator.Id, frequency.Id), 176));
        session.Execute(new SetNodeMutedCommand(root, original.Id, true));
        var before = session.Snapshot;
        var rootSource = Add(session, root, OscillatorGenerator.NodeType);
        session.Execute(new ConnectNodesCommand(root,
            new PortAddress(rootSource.Id, rootSource.Outputs.Single().Id),
            new PortAddress(original.Id, original.Inputs[0].Id)));
        var external = session.Snapshot.Connections.ToArray();
        var copyId = Guid.NewGuid();

        session.Execute(new DuplicateNodeCommand(root, original.Id, copyId));
        var copy = session.Snapshot.GetNode(copyId);
        var originalNow = session.Snapshot.GetNode(original.Id);
        var originalChild = originalNow.Composite!.Graph;
        var copyChild = copy.Composite!.Graph;
        Assert.True(copy.IsMuted);
        Assert.Equal(originalNow.Name, copy.Name);
        Assert.Equal(originalChild.Nodes.Count, copyChild.Nodes.Count);
        Assert.Equal(originalChild.Connections.Count, copyChild.Connections.Count);
        Assert.NotEqual(originalChild.Id, copyChild.Id);
        Assert.NotEqual(originalNow.Inputs[0].Id, copy.Inputs[0].Id);
        Assert.Equal(copy.Inputs[0].Id, copyChild.GetNode(copy.Composite.InputRackId).Outputs[0].Id);
        Assert.Equal(external, session.Snapshot.Connections.ToArray());

        var copyOscillator = copyChild.Nodes.Single(n => n.TypeKey == OscillatorGenerator.NodeType);
        Assert.Equal(176, copyOscillator.Parameters.Single(p => p.Definition.Key == "frequency").BaseValue);
        Assert.NotEqual(originalOscillator.Id, copyOscillator.Id);
        var copyNested = copyChild.Nodes.Single(n => n.TypeKey == CompositeNode.NodeType);
        Assert.NotEqual(nested.Id, copyNested.Id);
        Assert.NotEqual(nested.Composite!.Graph.Id, copyNested.Composite!.Graph.Id);
        Assert.Equal(nested.Composite.Graph.Connections.Count, copyNested.Composite.Graph.Connections.Count);
        Assert.Empty(AllIds(originalNow).Intersect(AllIds(copy)));
        
        session.Undo();
        Assert.DoesNotContain(session.Snapshot.Nodes, node => node.Id == copyId);
        session.Redo();
        Assert.Equal(copyNested.Composite.Graph.Id,
            session.Snapshot.GetNode(copyId).Composite!.Graph.Nodes.Single(n => n.TypeKey == CompositeNode.NodeType).Composite!.Graph.Id);
    }

    [Fact]
    public void InvalidDuplicateDoesNotAdvanceRevisionAndRackCannotBeDuplicated()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var original = Add(session, session.GraphId, CompositeNode.NodeType);
        var before = session.Snapshot;
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new DuplicateNodeCommand(session.GraphId, original.Id, original.Id)));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new DuplicateNodeCommand(session.GraphId, original.Id, Guid.Empty)));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new DuplicateNodeCommand(original.Composite!.Graph.Id, original.Composite.InputRackId, Guid.NewGuid())));
        Assert.Throws<GraphValidationException>(() => session.Execute(
            new DuplicateNodeCommand(session.GraphId, Guid.NewGuid(), Guid.NewGuid())));
        Assert.Same(before, session.Snapshot);
    }

    private static GraphNodeSnapshot Add(PatchSession session, Guid graphId, string type) =>
        Assert.IsType<NodeAdded>(Assert.Single(session.Execute(new AddNodeCommand(graphId, type)).ChangeSet!.Events)).Node;

    private static HashSet<Guid> AllIds(GraphNodeSnapshot node)
    {
        var ids = new HashSet<Guid>();
        void CollectNode(GraphNodeSnapshot current)
        {
            ids.Add(current.Id);
            foreach (var port in current.Inputs.Concat(current.Outputs)) ids.Add(port.Id);
            foreach (var parameter in current.Parameters) ids.Add(parameter.Id);
            if (current.Composite is not { } composite) return;
            ids.Add(composite.Graph.Id);
            foreach (var connection in composite.Graph.Connections) ids.Add(connection.Id);
            foreach (var child in composite.Graph.Nodes) CollectNode(child);
        }
        CollectNode(node);
        return ids;
    }
}
