using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class NodeVisualizationTests
{
    [Fact]
    public void ResponseModeIsAvailableOnlyForSupportedProcessorsAndRoundTrips()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var oscillator = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var filter = session.Execute(new AddNodeCommand(session.GraphId, FilterProcessor.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var eq = session.Execute(new AddNodeCommand(session.GraphId, ParametricEqProcessor.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        using var navigation = new CanvasNavigationService(session);
        var settings = NodeVisualizationSettings.Default with { Visible = true, Mode = "response" };
        var before = session.Snapshot;
        navigation.ActiveCanvas.SetVisualization(oscillator.Id, settings);
        navigation.ActiveCanvas.SetVisualization(filter.Id, settings);
        navigation.ActiveCanvas.SetVisualization(eq.Id, settings);
        Assert.Equal("waveform", navigation.ActiveCanvas.Snapshot.VisualizationFor(oscillator.Id).Mode);
        Assert.Same(before, session.Snapshot);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Deserialize(serializer.Serialize(serializer.Capture(session, 120, session.GraphId, navigation.CaptureLayouts())));
        using var restored = serializer.Restore(document, registry);
        using var loaded = new CanvasNavigationService(restored, layouts: document.CanvasLayouts);
        Assert.Equal(settings, loaded.ActiveCanvas.Snapshot.VisualizationFor(filter.Id));
        Assert.Equal(settings, loaded.ActiveCanvas.Snapshot.VisualizationFor(eq.Id));
    }

    [Fact]
    public void LayoutPreferencesDoNotEditGraphAndRestoreAfterNodeUndo()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        using var canvas = new CanvasStateService(session);
        var before = canvas.Snapshot;
        var settings = NodeVisualizationSettings.Default with { Visible = true, Mode = "spectrum", FftSize = "2048" };
        var changes = new List<CanvasStateChange>();
        using var observer = canvas.Subscribe(changes.Add);
        canvas.SetVisualization(node.Id, settings);
        canvas.SetExpandedVisualization(node.Id);
        Assert.Equal(before.GraphRevision, session.Snapshot.Revision);
        Assert.Same(before.Graph, canvas.Snapshot.Graph);
        Assert.Equal(before.Viewport, canvas.Snapshot.Viewport);
        Assert.All(changes, change => Assert.Equal(CanvasChangeKind.VisualizationChanged, change.Kind));
        Assert.Equal(2, changes.Count);
        Assert.Empty(before.Visualizations);
        Assert.Equal(settings, canvas.Snapshot.VisualizationFor(node.Id));
        Assert.Equal(node.Id, canvas.Snapshot.ExpandedVisualizationNodeId);
        Assert.False(canvas.SetVisualization(node.Id, settings).Changed);

        session.Undo(); // The only graph history entry was adding the node.
        Assert.Empty(canvas.Snapshot.Graph.Nodes);
        Assert.Empty(canvas.Snapshot.Visualizations);
        Assert.Null(canvas.Snapshot.ExpandedVisualizationNodeId);
        session.Redo();
        Assert.Equal(settings, canvas.Snapshot.VisualizationFor(node.Id));
    }

    [Fact]
    public void DormantPortFallsBackWithoutLosingPreferenceAndMalformedValuesNormalize()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var frequency = node.Parameters.Single(parameter => parameter.Definition.Key == "frequency");
        var timing = node.Parameters.Single(parameter => parameter.Definition.Key == "timing");
        using var canvas = new CanvasStateService(session);
        canvas.SetVisualization(node.Id, NodeVisualizationSettings.Default with
        {
            PortId = frequency.ModulationPortId, Mode = "invalid", FftSize = "999999", Timebase = "NaN"
        });
        var saved = canvas.Snapshot.VisualizationFor(node.Id);
        Assert.Equal("waveform", saved.Mode);
        Assert.Equal("8192", saved.FftSize);
        Assert.Equal("0.02", saved.Timebase);
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(node.Id, timing.Id), 1));
        var synchronized = canvas.Snapshot.Graph.GetNode(node.Id);
        Assert.Equal(frequency.ModulationPortId, canvas.Snapshot.VisualizationFor(node.Id).PortId);
        Assert.Equal(node.Outputs.Single().Id, NodeVisualizationCapabilities.ResolvePort(synchronized, saved.PortId)!.Id);
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(node.Id, timing.Id), 0));
        Assert.Equal(frequency.ModulationPortId, NodeVisualizationCapabilities.ResolvePort(canvas.Snapshot.Graph.GetNode(node.Id), saved.PortId)!.Id);
    }

    [Fact]
    public void RoundTripRestoresIndependentNestedPreferencesAndLegacyDefaults()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var drone = session.Execute(new AddNodeCommand(session.GraphId, "composite.graph"))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        using var navigation = new CanvasNavigationService(session);
        var rootSettings = NodeVisualizationSettings.Default with { Visible = true, Mode = "level" };
        navigation.ActiveCanvas.SetVisualization(drone.Id, rootSettings);
        navigation.ActiveCanvas.SetExpandedVisualization(drone.Id);
        navigation.OpenChild(drone.Id);
        var child = navigation.ActiveCanvas.Snapshot.Graph.Nodes.First(node => node.TypeKey == OscillatorGenerator.NodeType);
        var childSettings = NodeVisualizationSettings.Default with { Visible = true, Mode = "spectrum", Window = "rectangular", Smoothing = "0", FftSize = "1024", PortId = child.Outputs.Single().Id };
        navigation.ActiveCanvas.SetVisualization(child.Id, childSettings);
        navigation.ActiveCanvas.SetExpandedVisualization(child.Id);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Deserialize(serializer.Serialize(serializer.Capture(session, 120, navigation.ActiveCanvas.Snapshot.GraphId, navigation.CaptureLayouts())));
        using var restored = serializer.Restore(document, registry);
        using var loaded = new CanvasNavigationService(restored, layouts: document.CanvasLayouts, activeGraphId: document.ActiveGraphId);
        Assert.Equal(childSettings, loaded.ActiveCanvas.Snapshot.VisualizationFor(child.Id));
        Assert.Equal(child.Id, loaded.ActiveCanvas.Snapshot.ExpandedVisualizationNodeId);
        loaded.NavigateTo(restored.GraphId);
        Assert.Equal(rootSettings, loaded.ActiveCanvas.Snapshot.VisualizationFor(drone.Id));
        Assert.Equal(drone.Id, loaded.ActiveCanvas.Snapshot.ExpandedVisualizationNodeId);

        var legacy = document.CanvasLayouts.Select(layout => new SavedCanvas(layout.GraphId, layout.PanX, layout.PanY, layout.Zoom, layout.Positions)).ToArray();
        using var legacyNavigation = new CanvasNavigationService(restored, layouts: legacy);
        Assert.Equal(NodeVisualizationSettings.Default, legacyNavigation.ActiveCanvas.Snapshot.VisualizationFor(drone.Id));
        Assert.Null(legacyNavigation.ActiveCanvas.Snapshot.ExpandedVisualizationNodeId);
    }
}
