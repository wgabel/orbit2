using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class GraphElementCountsTests
{
    [Fact]
    public void EmptyAndFlatPatchesCountOnlyStoredElements()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        using var navigation = new CanvasNavigationService(session);
        Assert.Equal(new GraphElementCounts(0, 0), navigation.PatchCounts);
        Add(session, session.GraphId, OscillatorGenerator.NodeType);
        Assert.Equal(new GraphElementCounts(1, 0), navigation.PatchCounts);
        navigation.ActiveCanvas.AddNote(new CanvasBounds(0, 0, 1000, 1000), []);
        Assert.Equal(new GraphElementCounts(1, 0), navigation.PatchCounts);
    }

    [Fact]
    public void RecursiveTotalsIncludeUnvisitedSiblingsAndFollowCommitsAndHistory()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var outer = Add(session, session.GraphId, CompositeNode.NodeType);
        var sibling = Add(session, session.GraphId, CompositeNode.NodeType);
        var inner = Add(session, outer.Composite!.Graph.Id, CompositeNode.NodeType);
        var innerGraph = inner.Composite!.Graph;
        using var navigation = new CanvasNavigationService(session);
        Assert.Equal(new GraphElementCounts(15, 12), navigation.PatchCounts);
        navigation.OpenChild(outer.Id);
        navigation.OpenChild(inner.Id);
        Assert.Equal(4, navigation.ActiveCanvas.Snapshot.Graph.Nodes.Count);
        Assert.Equal(new GraphElementCounts(15, 12), navigation.PatchCounts);

        // The unvisited sibling still contributes to the live patch total.
        Add(session, sibling.Composite!.Graph.Id, OscillatorGenerator.NodeType);
        Assert.Equal(new GraphElementCounts(16, 12), navigation.PatchCounts);
        var observed = new List<GraphElementCounts>();
        using var listener = navigation.Subscribe(_ => observed.Add(navigation.PatchCounts));
        var removed = session.Execute(new RemoveConnectionCommand(innerGraph.Id, innerGraph.Connections[0].Id));
        Assert.Empty(removed.SubscriberErrors);
        Assert.Equal(new GraphElementCounts(16, 11), Assert.Single(observed));
        session.Undo();
        Assert.Equal(new GraphElementCounts(16, 12), navigation.PatchCounts);
        session.Redo();
        Assert.Equal(new GraphElementCounts(16, 11), navigation.PatchCounts);
        session.Execute(new RemoveNodeCommand(session.GraphId, outer.Id));
        Assert.Equal(session.GraphId, navigation.ActiveCanvas.Snapshot.GraphId);
        Assert.Equal(new GraphElementCounts(6, 4), navigation.PatchCounts);
        session.Undo();
        Assert.Equal(new GraphElementCounts(16, 11), navigation.PatchCounts);
    }

    private static GraphNodeSnapshot Add(PatchSession session, Guid graphId, string type) =>
        session.Execute(new AddNodeCommand(graphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
}
