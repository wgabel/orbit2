using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Application.Presets;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class PatchPersistenceTests
{
    [Fact]
    public void AppliedPresetPersistsActualValuesAndKeepsCanvasLayout()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var oscillator = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        using var navigation = new CanvasNavigationService(session);
        var canvas = navigation.ActiveCanvas;
        canvas.MoveNode(oscillator.Id, new CanvasPoint(123, 456));
        canvas.ZoomAt(0.8, CanvasPoint.Zero);
        var before = canvas.Snapshot;
        var changes = new List<CanvasStateChange>();
        using var observer = canvas.Subscribe(changes.Add);
        var values = oscillator.Parameters.ToDictionary(parameter => parameter.Definition.Key, parameter => parameter.BaseValue);
        values["frequency"] = 220;
        values["amplitude"] = 0.5;
        var preset = new NodePreset(1, "test-preset", oscillator.TypeKey, "Test", "Test", null, values);
        session.Execute(new ApplyNodePresetCommand(session.GraphId, oscillator.Id, preset));
        Assert.Equal(CanvasChangeKind.GraphCommitted, Assert.Single(changes).Kind);
        Assert.Equal(before.LayoutRevision, canvas.Snapshot.LayoutRevision);
        Assert.Equal(before.NodePositions[oscillator.Id], canvas.Snapshot.NodePositions[oscillator.Id]);
        Assert.Equal(before.Viewport, canvas.Snapshot.Viewport);

        var serializer = new PatchDocumentSerializer();
        var json = serializer.Serialize(serializer.Capture(session, 120, session.GraphId, navigation.CaptureLayouts()));
        Assert.DoesNotContain(preset.Id, json);
        using var restored = serializer.Restore(serializer.Deserialize(json), registry);
        foreach (var parameter in restored.Snapshot.GetNode(oscillator.Id).Parameters)
        {
            Assert.Equal(values[parameter.Definition.Key], parameter.BaseValue);
            Assert.Equal(oscillator.Parameters.Single(p => p.Definition.Key == parameter.Definition.Key).Id, parameter.Id);
        }
    }

    [Fact]
    public void RoundTripPreservesNestedIdentitiesValuesBoundariesAndViewports()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var drone = session.Execute(new AddNodeCommand(session.GraphId, DroneTemplate.LayeredPresetKey))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var graphId = drone.Composite!.Graph.Id;
        var oscillator = drone.Composite.Graph.Nodes.First(node => node.TypeKey == OscillatorGenerator.NodeType);
        var frequency = oscillator.Parameters.Single(parameter => parameter.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(graphId, new ParameterAddress(oscillator.Id, frequency.Id), 123));
        session.Execute(new RenameBoundaryPortCommand(graphId, PortDirection.Input, drone.Inputs[0].Id, "Sweep"));
        using var navigation = new CanvasNavigationService(session);
        navigation.ActiveCanvas.MoveNode(drone.Id, new CanvasPoint(300, 55));
        navigation.ActiveCanvas.ZoomAt(0.8, CanvasPoint.Zero);
        navigation.OpenChild(drone.Id);
        navigation.ActiveCanvas.PanBy(new CanvasPoint(13, 41));
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 87, graphId, navigation.CaptureLayouts());
        var json = serializer.Serialize(document);
        var restored = serializer.Deserialize(json);
        using var loaded = serializer.Restore(restored, registry);
        using var restoredNavigation = new CanvasNavigationService(loaded, layouts: restored.CanvasLayouts, activeGraphId: restored.ActiveGraphId);
        Assert.Equal(session.PatchId, loaded.PatchId);
        Assert.Equal(graphId, restoredNavigation.ActiveCanvas.Snapshot.GraphId);
        Assert.False(restoredNavigation.IsFirstVisit);
        Assert.Equal(navigation.ActiveCanvas.Snapshot.Viewport, restoredNavigation.ActiveCanvas.Snapshot.Viewport);
        Assert.Equal(123, loaded.Snapshot.GetGraph(graphId).GetNode(oscillator.Id).Parameters.Single(p => p.Id == frequency.Id).BaseValue);
        Assert.Equal("Sweep", loaded.Snapshot.GetNode(drone.Id).Inputs[0].Name);
        Assert.Equal(json, serializer.Serialize(serializer.Capture(loaded, 87, graphId, restoredNavigation.CaptureLayouts())));
        restoredNavigation.NavigateTo(loaded.GraphId);
        Assert.Equal(new CanvasPoint(300, 55), restoredNavigation.ActiveCanvas.Snapshot.NodePositions[drone.Id]);
        Assert.Equal(0.8, restoredNavigation.ActiveCanvas.Snapshot.Viewport.Zoom);
    }

    [Fact]
    public void RejectsUnknownVersionAndInvalidGraphWithoutChangingOriginal()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, session.GraphId, []);
        Assert.Throws<NotSupportedException>(() => serializer.Restore(document with { SchemaVersion = 99 }, registry));
        Assert.Throws<ArgumentException>(() => serializer.Restore(document with { RootGraph = document.RootGraph with { Id = Guid.Empty } }, registry));
        Assert.Empty(session.Snapshot.Nodes);
    }
}
