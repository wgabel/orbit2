using Orbit.Application.Graphs;
using Orbit.Application.Models;
using Orbit.Domain.Nodes.Generators;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

/// <summary>Connections, visual roles and fit use reusable world geometry.</summary>
public sealed class GraphGeometryTests
{
    [Fact]
    public void BezierStartsAndEndsAtPortCentersWithHorizontalTangents()
    {
        var source = new CanvasPoint(20, 60);
        var destination = new CanvasPoint(-80, 180);
        var curve = BezierConnection.Between(source, destination);
        Assert.Equal(source, curve.Start);
        Assert.Equal(destination, curve.End);
        Assert.Equal(source.Y, curve.Control1.Y);
        Assert.Equal(destination.Y, curve.Control2.Y);
        Assert.True(curve.Control1.X > source.X);
        Assert.True(curve.Control2.X < destination.X);
    }

    [Fact]
    public void BadgeAnchorFollowsCubicAtMidpointIncludingBackwardsConnections()
    {
        foreach (var end in new[] { new CanvasPoint(300, 120), new CanvasPoint(-180, 120) })
        {
            var curve = BezierConnection.Between(new CanvasPoint(20, 40), end);
            Assert.Equal(curve.Start, curve.At(0));
            Assert.Equal(curve.End, curve.At(1));
            var middle = curve.At(0.5);
            Assert.Equal((curve.Start.X + 3 * curve.Control1.X + 3 * curve.Control2.X + curve.End.X) / 8, middle.X, 8);
            Assert.Equal(80, middle.Y, 8);
            Assert.Throws<ArgumentOutOfRangeException>(() => curve.At(-0.1));
        }
    }

    [Fact]
    public void PatchCableHasStraightRigidSleevesAndSoftSaggingMiddle()
    {
        var source = new CanvasPoint(10, 60);
        var target = new CanvasPoint(310, 60);
        var cable = PatchCable.Between(source, target);

        Assert.Equal(source, cable.Source);
        Assert.Equal(target, cable.Target);
        Assert.Equal(source.X + PatchCable.MaximumRigidLength, cable.Exit.X);
        Assert.Equal(target.X - PatchCable.MaximumRigidLength, cable.Entry.X);
        Assert.Equal(source.Y, cable.Exit.Y);
        Assert.Equal(target.Y, cable.Entry.Y);
        Assert.Equal(cable.Exit, cable.First.Start);
        Assert.Equal(cable.First.End, cable.Second.Start);
        Assert.Equal(cable.Entry, cable.Second.End);
        Assert.Equal(cable.First.Start.Y, cable.First.Control1.Y);
        Assert.Equal(cable.Second.End.Y, cable.Second.Control2.Y);
        Assert.Equal(cable.First.Control2.Y, cable.Second.Control1.Y);
        Assert.Equal(cable.First.End.Y, cable.First.Control2.Y);
        Assert.True(cable.At(0.5).Y > source.Y, "the flexible middle should hang below level ports");
        Assert.Equal(source, cable.At(0));
        Assert.Equal(target, cable.At(1));
    }

    [Theory]
    [InlineData(114, 60)]
    [InlineData(100, 220)]
    [InlineData(-80, 90)]
    [InlineData(100, 60)]
    public void PatchCableKeepsBothEndpointsAndAvoidsOversizedSleeves(double endX, double endY)
    {
        var source = new CanvasPoint(100, 60);
        var target = new CanvasPoint(endX, endY);
        var cable = PatchCable.Between(source, target);
        Assert.Equal(source, cable.At(0));
        Assert.Equal(target, cable.At(1));
        Assert.True(cable.Exit.X >= source.X);
        Assert.True(cable.Entry.X <= target.X);
        Assert.True(cable.Exit.X - source.X <= PatchCable.MaximumRigidLength);
        Assert.Equal(cable.Exit.X - source.X, target.X - cable.Entry.X, 8);
        if (endX > source.X)
            Assert.True((cable.Exit.X - source.X) * 3 <= endX - source.X + 0.0001);
        Assert.True(double.IsFinite(cable.At(0.5).X));
        Assert.True(double.IsFinite(cable.At(0.5).Y));
        Assert.Throws<ArgumentOutOfRangeException>(() => cable.At(1.01));
    }

    [Fact]
    public void FitCentersBoundsWithScreenPaddingAndClampedZoom()
    {
        var bounds = new CanvasBounds(-50, -100, 350, 100);
        var viewport = CanvasViewport.Fit(bounds, 500, 400, 50);
        Assert.Equal(1d, viewport.Zoom);
        Assert.Equal(new CanvasPoint(50, 100), viewport.WorldToScreen(new CanvasPoint(bounds.Left, bounds.Top)));
        Assert.Equal(new CanvasPoint(450, 300), viewport.WorldToScreen(new CanvasPoint(bounds.Right, bounds.Bottom)));

        var distant = CanvasViewport.Fit(new CanvasBounds(0, 0, 10000, 10000), 500, 400, 50);
        Assert.Equal(CanvasViewport.MinimumZoom, distant.Zoom);
    }

    [Fact]
    public void TheSeedRolesFollowSignalRoutingNotFrequency()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var graph = session.Snapshot;
        var generators = graph.Nodes.Where(node => node.TypeKey == OscillatorGenerator.NodeType).ToArray();
        Assert.Equal(NodeVisualRole.Modulator, NodeRoleClassifier.Classify(graph, generators[0]));
        Assert.Equal(NodeVisualRole.Generator, NodeRoleClassifier.Classify(graph, generators[1]));
        Assert.Equal(NodeVisualRole.Processor, NodeRoleClassifier.Classify(graph, graph.Nodes[2]));
        Assert.Equal(NodeVisualRole.Routing, NodeRoleClassifier.Classify(graph, graph.Nodes[3]));
    }
}
