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
    public void OnlyLeafNodesAreCompactable()
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
    public void ActivationIsEphemeralAndExclusive()
    {
        var first = Node();
        var second = Node();
        var state = new NodeExpansionState();
        Assert.False(state.IsExpanded(first));
        Assert.True(state.Expand(first.Id));
        Assert.True(state.IsExpanded(first));
        Assert.False(state.Expand(first.Id));
        Assert.True(state.Expand(second.Id));
        Assert.False(state.IsExpanded(first));
        Assert.True(state.IsExpanded(second));
        Assert.True(state.Toggle(second.Id));
        Assert.False(state.IsExpanded(second));
        Assert.False(state.Collapse());
    }

    [Fact]
    public void DeletedExpandedNodeIsPrunedWithoutChangingOtherNodes()
    {
        var first = Node();
        var second = Node();
        var state = new NodeExpansionState();
        state.Expand(first.Id);
        Assert.False(state.Prune([first, second]));
        Assert.True(state.Prune([second]));
        Assert.Null(state.ExpandedNodeId);
        Assert.True(state.IsExpanded(Node(MasterOutput.NodeType)));
    }
}
