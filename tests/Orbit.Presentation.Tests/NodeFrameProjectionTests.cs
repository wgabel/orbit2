using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class NodeFrameProjectionTests
{
    private static GraphNodeSnapshot Node() => new(Guid.NewGuid(), "generator.test", "Test",
        [new ParameterSnapshot(Guid.NewGuid(), new ParameterDefinition("frequency", "Frequency", 100, 1, 1000,
            ParameterUnit.Hertz, ParameterScale.Linear, true, ParameterSmoothing.Pitch), 100, Guid.NewGuid())], [], []);
    private static GraphSnapshot Graph(Guid id, long revision, params GraphNodeSnapshot[] nodes) =>
        new(id, revision, nodes, []);
    private static GraphNodeSnapshot Clone(GraphNodeSnapshot node) => node with
    {
        Parameters = node.Parameters.Select(parameter => parameter with { }).ToArray(),
        Inputs = node.Inputs.ToArray(), Outputs = node.Outputs.ToArray()
    };

    [Fact]
    public void UnrelatedLeafRetainsReferenceAcrossGraphRevisions()
    {
        var a = Node();
        var b = Node();
        var id = Guid.NewGuid();
        var cache = new NodeFrameProjection();
        var first = cache.Update(Graph(id, 1, a, b), new Dictionary<ParameterAddress, ParameterRange?>());
        var changed = Clone(a) with { Parameters = [a.Parameters[0] with { BaseValue = 200 }] };
        var graph = Graph(id, 2, changed, Clone(b));
        var ranges = new Dictionary<ParameterAddress, ParameterRange?>();
        var second = cache.Update(graph, ranges);
        Assert.Same(changed, second[0].Node);
        Assert.Same(first[1], second[1]);
        Assert.Same(second, cache.Update(graph, ranges));
    }

    [Fact]
    public void RangeChangesAreScopedAndUnknownIsNotAbsent()
    {
        var a = Node();
        var b = Node();
        var address = new ParameterAddress(a.Id, a.Parameters[0].Id);
        var cache = new NodeFrameProjection();
        var graph = Graph(Guid.NewGuid(), 1, a, b);
        var first = cache.Update(graph, new Dictionary<ParameterAddress, ParameterRange?>());
        var unknown = cache.Update(graph, new Dictionary<ParameterAddress, ParameterRange?> { [address] = null });
        Assert.NotSame(first[0], unknown[0]);
        Assert.True(unknown[0].Ranges.ContainsKey(address));
        Assert.Same(first[1], unknown[1]);
        var known = cache.Update(graph, new Dictionary<ParameterAddress, ParameterRange?> { [address] = new(10, 20) });
        Assert.NotSame(unknown[0], known[0]);
        Assert.Same(unknown[1], known[1]);
        var same = cache.Update(graph, new Dictionary<ParameterAddress, ParameterRange?> { [address] = new(10, 20) });
        Assert.Same(known[0], same[0]);
        var removed = cache.Update(graph, new Dictionary<ParameterAddress, ParameterRange?>());
        Assert.Empty(removed[0].Ranges);
    }

    [Fact]
    public void PortAndAuditionChangesInvalidateWhileCompositeRemainsConservative()
    {
        var a = Node();
        var id = Guid.NewGuid();
        var composite = Node() with { Composite = new CompositeSnapshot(Graph(Guid.NewGuid(), 1), Guid.NewGuid(), Guid.NewGuid()) };
        var cache = new NodeFrameProjection();
        var ranges = new Dictionary<ParameterAddress, ParameterRange?>();
        var first = cache.Update(Graph(id, 1, a, composite), ranges);
        var updated = a with { IsMuted = true, Outputs = [new PortDefinition(Guid.NewGuid(), "out", "Output", PortDirection.Output, SignalType.Audio)] };
        var childChanged = composite with { Composite = composite.Composite! with { Graph = Graph(composite.Composite!.Graph.Id, 2) } };
        var second = cache.Update(Graph(id, 2, updated, childChanged), ranges);
        Assert.NotSame(first[0].Node, second[0].Node);
        Assert.Same(childChanged, second[1].Node);
        cache.Reset();
        var fresh = Clone(updated);
        Assert.Same(fresh, cache.Update(Graph(id, 3, fresh), ranges)[0].Node);
    }
}
