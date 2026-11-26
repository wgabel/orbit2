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
    public void TelemetryFilteringPreservesVisibleValuesAndClearsFoldedConsumers()
    {
        var first = Node();
        var second = Node();
        var master = Node(MasterOutput.NodeType);
        var state = new NodeExpansionState();
        IReadOnlyDictionary<Guid, double> empty = new Dictionary<Guid, double>();
        IReadOnlyDictionary<Guid, double> values = new Dictionary<Guid, double>
        {
            [first.Id] = 0, [second.Id] = 5, [master.Id] = 7
        };
        Assert.Same(values, state.FilterTelemetry(values, static id => id, empty));
        state.Toggle(first.Id);
        var filtered = state.FilterTelemetry(values, static id => id, empty);
        Assert.False(filtered.ContainsKey(first.Id));
        Assert.Equal(5, filtered[second.Id]);
        Assert.Equal(7, filtered[master.Id]);
        Assert.False(state.IsTelemetryVisible(first.Id));
        state.Toggle(second.Id);
        IReadOnlyDictionary<Guid, double> leafValues = new Dictionary<Guid, double>
        {
            [first.Id] = 0, [second.Id] = 5
        };
        Assert.Same(empty, state.FilterTelemetry(leafValues, static id => id, empty));
        Assert.Same(empty, state.FilterTelemetry(empty, static id => id, empty));
        state.Expand(first.Id);
        Assert.Equal(0, state.FilterTelemetry(leafValues, static id => id, empty)[first.Id]);
        state.Reset();
        Assert.Same(leafValues, state.FilterTelemetry(leafValues, static id => id, empty));
    }

    [Fact]
    public void TelemetryFilteringUsesNodeIdentityForMultipleParameterAddresses()
    {
        var first = Node();
        var second = Node();
        var state = new NodeExpansionState();
        IReadOnlyDictionary<(Guid Node, Guid Parameter), double> empty =
            new Dictionary<(Guid Node, Guid Parameter), double>();
        var a = (first.Id, Guid.NewGuid());
        var b = (first.Id, Guid.NewGuid());
        var c = (second.Id, Guid.NewGuid());
        IReadOnlyDictionary<(Guid Node, Guid Parameter), double> values =
            new Dictionary<(Guid Node, Guid Parameter), double> { [a] = 1, [b] = 2, [c] = 3 };
        state.Toggle(first.Id);
        var visible = state.FilterTelemetry(values, static key => key.Node, empty);
        Assert.Single(visible);
        Assert.Equal(3, visible[c]);
        state.Expand(first.Id);
        Assert.Same(values, state.FilterTelemetry(values, static key => key.Node, empty));
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
