using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class TouchWireAssistTests
{
    private static readonly CanvasBounds Bounds = new(0, 0, 400, 600);
    private static WireTarget Target(double x, double y) => new(new PortAddress(Guid.NewGuid(), Guid.NewGuid()), new CanvasPoint(x, y));

    [Theory]
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void OffsetAndSnapRadiusRemainInScreenPixels(double zoom)
    {
        var viewport = new CanvasViewport(new CanvasPoint(10, 20), zoom);
        var screen = viewport.WorldToScreen(viewport.ScreenToWorld(new CanvasPoint(200, 200)));
        var target = Target(screen.X, screen.Y);
        Assert.Equal(target, TouchWireAssist.Resolve(new CanvasPoint(223, 260), Bounds, [target], null).Target);
        var free = TouchWireAssist.Resolve(new CanvasPoint(225, 260), Bounds, [target], null);
        Assert.Null(free.Target);
        Assert.Equal(new CanvasPoint(225, 200), free.Point);
        Assert.Equal(screen, viewport.WorldToScreen(viewport.ScreenToWorld(target.Point)));
    }

    [Fact]
    public void HysteresisRetainsLockButCanSwitchToAdjacentPorts()
    {
        var first = Target(200, 200);
        var second = Target(200, 208);
        Assert.Equal(first, TouchWireAssist.Resolve(new CanvasPoint(200, 265), Bounds, [first, second], first).Target);
        Assert.Equal(second, TouchWireAssist.Resolve(new CanvasPoint(200, 268), Bounds, [first, second], first).Target);
        Assert.Equal(first, TouchWireAssist.Resolve(new CanvasPoint(230, 260), Bounds, [first], first).Target);
        Assert.Null(TouchWireAssist.Resolve(new CanvasPoint(237, 260), Bounds, [first], first).Target);
        Assert.Null(TouchWireAssist.Resolve(new CanvasPoint(200, 260), Bounds, [], first).Target);
    }

    [Fact]
    public void MouseAndPenAimDirectlyWithTheSameScreenPixelSnapAndReleaseRadii()
    {
        var target = Target(200, 200);
        Assert.Equal(target, TouchWireAssist.Resolve(new CanvasPoint(223, 200), Bounds, [target], null, offsetForTouch: false).Target);
        var free = TouchWireAssist.Resolve(new CanvasPoint(225, 200), Bounds, [target], null, offsetForTouch: false);
        Assert.Null(free.Target);
        Assert.Equal(new CanvasPoint(225, 200), free.Point);
        Assert.Equal(target, TouchWireAssist.Resolve(new CanvasPoint(230, 200), Bounds, [target], target, offsetForTouch: false).Target);
        Assert.Null(TouchWireAssist.Resolve(new CanvasPoint(237, 200), Bounds, [target], target, offsetForTouch: false).Target);
        Assert.Null(TouchWireAssist.Resolve(new CanvasPoint(200, 260), Bounds, [target], null, offsetForTouch: false).Target);
    }

    [Fact]
    public void ReachesBottomEdgeAndDisallowsReleaseOutsideCanvas()
    {
        var target = Target(200, 580);
        Assert.Equal(target, TouchWireAssist.Resolve(new CanvasPoint(200, 590), Bounds, [target], null).Target);
        Assert.Null(TouchWireAssist.Resolve(new CanvasPoint(200, 601), Bounds, [target], target).Target);
        Assert.False(TouchWireAssist.HasMoved(new CanvasPoint(100, 100), new CanvasPoint(103, 104)));
        Assert.True(TouchWireAssist.HasMoved(new CanvasPoint(100, 100), new CanvasPoint(106, 100)));
    }
}
