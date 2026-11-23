using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class NodeExpansionStateTests
{
    private static GraphNodeSnapshot Node(string type = "generator.oscillator") =>
        new(Guid.NewGuid(), type, "Test", [], [], []);

    [Fact]
    public void OnlyLeafNodesCanUseCompactView()
    {
        Assert.True(NodeExpansionState.IsEligible(Node()));
        Assert.False(NodeExpansionState.IsEligible(Node(InputRack.NodeType)));
        Assert.False(NodeExpansionState.IsEligible(Node(OutputRack.NodeType)));
        Assert.False(NodeExpansionState.IsEligible(Node(MasterOutput.NodeType)));
        Assert.False(NodeExpansionState.IsEligible(Node() with
        {
            Composite = new CompositeSnapshot(null!, Guid.Empty, Guid.Empty)
        }));
    }

    [Fact]
    public void ManualCollapseKeepsOtherControlsAvailable()
    {
        var first = Node();
        var second = Node();
        var state = new NodeExpansionState();
        Assert.True(state.IsExpanded(first)); // S1 remains full by default.
        Assert.True(state.IsExpanded(second));
        Assert.True(state.Toggle(first.Id));
        Assert.False(state.IsExpanded(first));
        Assert.True(state.IsExpanded(second));
        Assert.True(state.Expand(first.Id));
        Assert.True(state.IsExpanded(first));
        Assert.False(state.Expand(first.Id));
        Assert.False(state.Reset());
    }

    [Fact]
    public void CollapsedViewIsLocalAndPrunedOnRemoval()
    {
        var first = Node();
        var second = Node();
        var state = new NodeExpansionState();
        state.Toggle(first.Id);
        state.Toggle(second.Id);
        Assert.False(state.Prune([first, second]));
        Assert.True(state.Prune([second]));
        Assert.True(state.IsExpanded(first));
        Assert.False(state.IsExpanded(second));
        Assert.True(state.Reset());
        Assert.True(state.IsExpanded(second));
        Assert.True(state.IsExpanded(Node(MasterOutput.NodeType)));
    }
}
