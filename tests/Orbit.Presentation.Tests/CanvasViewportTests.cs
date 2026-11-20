using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

/// <summary>Geometry is independent of rendering technology and audio state.</summary>
public sealed class CanvasViewportTests
{
    [Fact]
    public void ConvertsBetweenWorldAndScreenWithOneTransform()
    {
        var viewport = new CanvasViewport(new CanvasPoint(-40, 60), 2);
        var world = new CanvasPoint(100, -10);

        Assert.Equal(new CanvasPoint(160, 40), viewport.WorldToScreen(world));
        Assert.Equal(world, viewport.ScreenToWorld(viewport.WorldToScreen(world)));
        Assert.Equal(new CanvasPoint(12, -8), viewport.ScreenDeltaToWorld(new CanvasPoint(24, -16)));
    }

    [Fact]
    public void PansFreelyInAllFourDirectionsWithoutChangingZoom()
    {
        var viewport = CanvasViewport.Initial
            .PanBy(new CanvasPoint(120, -80))
            .PanBy(new CanvasPoint(-200, 160));
        Assert.Equal(new CanvasPoint(-80, 80), viewport.Pan);
        Assert.Equal(1d, viewport.Zoom);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0.1, 0.25)]
    [InlineData(10, 4)]
    public void ZoomClampsAndPreservesTheWorldPointUnderThePointer(double requested, double expected)
    {
        var viewport = new CanvasViewport(new CanvasPoint(-80, 40), 1);
        var anchor = new CanvasPoint(300, 200);
        var world = viewport.ScreenToWorld(anchor);
        var next = viewport.ZoomAt(requested, anchor);

        Assert.Equal(expected, next.Zoom);
        Assert.Equal(expected * 100, next.ZoomPercentage);
        var actual = next.WorldToScreen(world);
        Assert.Equal(anchor.X, actual.X, 8);
        Assert.Equal(anchor.Y, actual.Y, 8);
    }

    [Fact]
    public void RejectsInvalidCoordinatesAndZoom()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CanvasViewport(CanvasPoint.Zero, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanvasViewport.Initial.ZoomAt(0, CanvasPoint.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanvasViewport.Initial.PanBy(new CanvasPoint(double.PositiveInfinity, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanvasViewport.Initial.WorldToScreen(new CanvasPoint(double.NaN, 0)));
    }

    [Fact]
    public void PlacesNewNodesInUnoccupiedWorldGridSlots()
    {
        var placement = new GridNodePlacementPolicy(columnSpacing: 100, rowSpacing: 80, columns: 2);
        Assert.Equal(CanvasPoint.Zero, placement.FindPosition([]));
        Assert.Equal(new CanvasPoint(100, 0), placement.FindPosition([CanvasPoint.Zero]));
        Assert.Equal(new CanvasPoint(0, 80), placement.FindPosition([
            CanvasPoint.Zero, new CanvasPoint(100, 0)]));
        Assert.Equal(new CanvasPoint(0, 80), placement.FindPosition([new CanvasPoint(50, 0)]));
    }
}
