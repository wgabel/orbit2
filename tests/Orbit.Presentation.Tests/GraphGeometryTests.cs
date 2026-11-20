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
        var generators = graph.Nodes.Where(node => node.TypeKey == SineGenerator.NodeType).ToArray();
        Assert.Equal(NodeVisualRole.Modulator, NodeRoleClassifier.Classify(graph, generators[0]));
        Assert.Equal(NodeVisualRole.Generator, NodeRoleClassifier.Classify(graph, generators[1]));
        Assert.Equal(NodeVisualRole.Processor, NodeRoleClassifier.Classify(graph, graph.Nodes[2]));
        Assert.Equal(NodeVisualRole.Routing, NodeRoleClassifier.Classify(graph, graph.Nodes[3]));
    }
}
