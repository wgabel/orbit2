using Orbit.Domain.Parameters;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class NodeEffectiveValuesProjectionTests
{
    [Fact]
    public void OnlyChangedNodeReceivesNewDictionary()
    {
        var a = new ParameterAddress(Guid.NewGuid(), Guid.NewGuid());
        var b = new ParameterAddress(Guid.NewGuid(), Guid.NewGuid());
        var cache = new NodeEffectiveValuesProjection();
        var unmodulated = Guid.NewGuid();
        cache.Update(new Dictionary<ParameterAddress, double> { [a] = 120, [b] = 0.5 });
        var firstA = cache.ForNode(a.NodeId);
        var firstB = cache.ForNode(b.NodeId);
        var empty = cache.ForNode(unmodulated);
        cache.Update(new Dictionary<ParameterAddress, double> { [b] = 0.5, [a] = 121 });
        Assert.NotSame(firstA, cache.ForNode(a.NodeId));
        Assert.Equal(121d, cache.ForNode(a.NodeId)[a]);
        Assert.Equal(120d, firstA[a]);
        Assert.Same(firstB, cache.ForNode(b.NodeId));
        Assert.Same(empty, cache.ForNode(unmodulated));
        Assert.Single(cache.ForNode(a.NodeId));
    }

    [Fact]
    public void MissingParametersAndStoppedTelemetryClearPreviousValues()
    {
        var a = new ParameterAddress(Guid.NewGuid(), Guid.NewGuid());
        var b = new ParameterAddress(a.NodeId, Guid.NewGuid());
        var cache = new NodeEffectiveValuesProjection();
        cache.Update(new Dictionary<ParameterAddress, double> { [a] = 0, [b] = 40 });
        cache.Update(new Dictionary<ParameterAddress, double> { [a] = 0 });
        Assert.Single(cache.ForNode(a.NodeId));
        Assert.Equal(0d, cache.ForNode(a.NodeId)[a]);
        Assert.False(cache.ForNode(a.NodeId).ContainsKey(b));
        cache.Update(new Dictionary<ParameterAddress, double>());
        Assert.Empty(cache.ForNode(a.NodeId));
    }

    [Fact]
    public void RepeatedFramesAreStableAndResetDiscardsCanvasState()
    {
        var a = new ParameterAddress(Guid.NewGuid(), Guid.NewGuid());
        var frame = new Dictionary<ParameterAddress, double> { [a] = 42 };
        var cache = new NodeEffectiveValuesProjection();
        cache.Update(frame);
        var old = cache.ForNode(a.NodeId);
        cache.Update(frame);
        Assert.Same(old, cache.ForNode(a.NodeId));
        cache.Update(new Dictionary<ParameterAddress, double> { [a] = 42 });
        Assert.Same(old, cache.ForNode(a.NodeId));
        cache.Reset();
        Assert.Empty(cache.ForNode(a.NodeId));
        cache.Update(frame);
        Assert.NotSame(old, cache.ForNode(a.NodeId));
        Assert.Equal(42d, cache.ForNode(a.NodeId)[a]);
    }
}
