using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class WireEdgePanTests
{
    private static readonly CanvasBounds Bounds = new(10, 20, 410, 620);

    [Fact]
    public void VelocityAcceleratesTowardEdgesAndPreservesNeutralCenter()
    {
        Assert.Equal(CanvasPoint.Zero, WireEdgePan.Velocity(Bounds, new(210, 320)));
        Assert.Equal(CanvasPoint.Zero, WireEdgePan.Velocity(Bounds, new(360, 320)));
        Assert.Equal(new CanvasPoint(-150, 0), WireEdgePan.Velocity(Bounds, new(385, 320)));
        Assert.Equal(new CanvasPoint(-600, 0), WireEdgePan.Velocity(Bounds, new(410, 320)));
        Assert.Equal(new CanvasPoint(600, 0), WireEdgePan.Velocity(Bounds, new(10, 320)));
        Assert.Equal(new CanvasPoint(0, -600), WireEdgePan.Velocity(Bounds, new(210, 620)));
        Assert.Equal(new CanvasPoint(0, 600), WireEdgePan.Velocity(Bounds, new(210, 20)));
        Assert.Equal(CanvasPoint.Zero, WireEdgePan.Velocity(Bounds, new(411, 320)));
        var corner = WireEdgePan.Velocity(Bounds, new(410, 620));
        Assert.True(corner.X < 0 && corner.Y < 0);
        Assert.Equal(600, Math.Sqrt(corner.X * corner.X + corner.Y * corner.Y), 8);
    }

    [Fact]
    public void DwellResetsOnLeavingEdgeChangingDirectionOrSnapping()
    {
        var pan = new WireEdgePan();
        var right = new CanvasPoint(409, 320);
        Assert.Equal(CanvasPoint.Zero, pan.Update(Bounds, right, 1000, false));
        Assert.Equal(CanvasPoint.Zero, pan.Update(Bounds, right, 1149, false));
        Assert.NotEqual(CanvasPoint.Zero, pan.Update(Bounds, right, 1150, false));
        Assert.Equal(CanvasPoint.Zero, pan.Update(Bounds, right, 1151, true));
        Assert.Equal(CanvasPoint.Zero, pan.Update(Bounds, right, 1301, false));
        Assert.NotEqual(CanvasPoint.Zero, pan.Update(Bounds, right, 1451, false));
        Assert.Equal(CanvasPoint.Zero, pan.Update(Bounds, new(11, 320), 1452, false));
        Assert.Equal(CanvasPoint.Zero, pan.Update(Bounds, new(210, 320), 1602, false));
        Assert.Equal(CanvasPoint.Zero, pan.Update(Bounds, right, 1603, false));
        Assert.NotEqual(CanvasPoint.Zero, pan.Update(Bounds, right, 1753, false));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(4)]
    public void ScrollKeepsThePointerEndpointInScreenSpaceAndZoomUnchanged(double zoom)
    {
        var viewport = new CanvasViewport(new(15, 25), zoom);
        var pointer = new CanvasPoint(395, 320);
        var next = viewport.PanBy(WireEdgePan.Velocity(Bounds, pointer).Scale(0.1));
        Assert.Equal(zoom, next.Zoom);
        Assert.Equal(pointer, next.WorldToScreen(next.ScreenToWorld(pointer)));
        Assert.Equal(CanvasPoint.Zero, WireEdgePan.Velocity(new(0, 0, 60, 60), new(30, 30)));
    }
}
