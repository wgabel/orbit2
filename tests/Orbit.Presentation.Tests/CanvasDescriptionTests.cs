using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class CanvasDescriptionTests
{
    private static GraphNodeSnapshot Add(PatchSession session, Guid graph, string type) =>
        session.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    private static Guid Connect(PatchSession session, GraphNodeSnapshot source, GraphNodeSnapshot destination) =>
        session.Execute(new ConnectNodesCommand(session.GraphId, new PortAddress(source.Id, source.Outputs.Single().Id),
            new PortAddress(destination.Id, destination.Inputs.Single(port => port.Key == "audio-in").Id)))
            .ChangeSet!.Events.OfType<ConnectionCreated>().Single().Connection.Id;

    [Fact]
    public void DescriptionsShareNoteHistoryWithoutGraphChanges()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, session.GraphId, OscillatorGenerator.NodeType);
        var output = Add(session, session.GraphId, MasterOutput.NodeType);
        var edge = Connect(session, node, output);
        using var canvas = new CanvasStateService(session);
        var original = canvas.Snapshot;
        var graph = session.Snapshot;
        var description = new SavedNodeDescription(node.Id, "Harmonic bed", "A minor\nOpen fifth");
        canvas.SetNodeDescription(description);
        canvas.SetNodeDescription(description);
        canvas.AddNote(new CanvasBounds(0, 0, 800, 600), []);
        canvas.SetConnectionDescription(new(edge, "Voice feed"));
        Assert.Same(graph, session.Snapshot);
        Assert.Empty(original.NodeDescriptions);
        Assert.Equal("Harmonic bed", canvas.Snapshot.DisplayName(node.Id));
        Assert.Equal(node.Name, canvas.Snapshot.Graph.GetNode(node.Id).Name);
        canvas.UndoAnnotation();
        Assert.Empty(canvas.Snapshot.ConnectionDescriptions);
        Assert.Single(canvas.Snapshot.Notes);
        canvas.UndoAnnotation();
        Assert.Empty(canvas.Snapshot.Notes);
        canvas.UndoAnnotation();
        Assert.Equal(node.Name, canvas.Snapshot.DisplayName(node.Id));
        Assert.False(canvas.Snapshot.CanUndoAnnotations);
        canvas.RedoAnnotation();
        Assert.Equal(description, canvas.Snapshot.NodeDescriptions[node.Id]);
        canvas.SetNodeDescription(new(node.Id, "", ""));
        Assert.Empty(canvas.Snapshot.NodeDescriptions);
        Assert.False(canvas.Snapshot.CanRedoAnnotations);
        Assert.Same(graph, session.Snapshot);
    }

    [Fact]
    public void DeletionAndGraphUndoRestoreOnlyOriginalTargetsIncludingDormantHistory()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, session.GraphId, OscillatorGenerator.NodeType);
        var output = Add(session, session.GraphId, MasterOutput.NodeType);
        var edge = Connect(session, node, output);
        using var navigation = new CanvasNavigationService(session);
        var canvas = navigation.ActiveCanvas;
        var nodeDescription = new SavedNodeDescription(node.Id, "Bass", "Low layer");
        var edgeDescription = new SavedConnectionDescription(edge, "Dry route");
        canvas.SetNodeDescription(nodeDescription);
        canvas.SetConnectionDescription(edgeDescription);
        session.Execute(new RemoveNodeCommand(session.GraphId, node.Id));
        Assert.Empty(canvas.Snapshot.NodeDescriptions);
        Assert.Empty(canvas.Snapshot.ConnectionDescriptions);
        Assert.Empty(Assert.Single(navigation.CaptureLayouts()).NodeDescriptions);
        Assert.Empty(Assert.Single(navigation.CaptureLayouts()).ConnectionDescriptions);
        canvas.UndoAnnotation();
        session.Undo();
        Assert.Equal(nodeDescription, canvas.Snapshot.NodeDescriptions[node.Id]);
        Assert.Empty(canvas.Snapshot.ConnectionDescriptions);
        canvas.RedoAnnotation();
        Assert.Equal(edgeDescription, canvas.Snapshot.ConnectionDescriptions[edge]);
        session.Redo();
        var replacement = Add(session, session.GraphId, OscillatorGenerator.NodeType);
        Assert.NotEqual(node.Id, replacement.Id);
        Assert.Equal(replacement.Name, canvas.Snapshot.DisplayName(replacement.Id));
        Assert.Empty(canvas.Snapshot.NodeDescriptions);
    }

    [Fact]
    public void ConnectionLabelsRoundTripIndependentlyOfAmountAndVisibility()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var source = Add(session, session.GraphId, OscillatorGenerator.NodeType);
        var output = Add(session, session.GraphId, MasterOutput.NodeType);
        var edge = Connect(session, source, output);
        using var nav = new CanvasNavigationService(session);
        nav.ActiveCanvas.SetConnectionDescription(new(edge, "Dry voice"));
        nav.ActiveCanvas.SetConnectionLabels(ConnectionLabelMode.Off);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Deserialize(serializer.Serialize(serializer.Capture(session, 120, session.GraphId, nav.CaptureLayouts())));
        using var restored = serializer.Restore(document, registry);
        using var loadedNav = new CanvasNavigationService(restored, layouts: document.CanvasLayouts);
        Assert.Equal("Dry voice", loadedNav.ActiveCanvas.Snapshot.ConnectionDescriptions[edge].Label);
        Assert.Equal(ConnectionLabelMode.Off, loadedNav.ActiveCanvas.Snapshot.ConnectionLabels);
        Assert.Equal(Assert.Single(session.Snapshot.Connections), Assert.Single(restored.Snapshot.Connections));
        var layout = Assert.Single(document.CanvasLayouts);
        Assert.Throws<ArgumentException>(() => serializer.Restore(document with
        {
            CanvasLayouts = [layout with { ConnectionDescriptions = [new(edge, "A"), new(edge, "B")] }]
        }, registry));
    }

    [Fact]
    public void NestedAliasesAndRackDescriptionsRoundTripAndRejectMalformedTargets()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var composite = Add(session, session.GraphId, CompositeNode.NodeType);
        var childId = composite.Composite!.Graph.Id;
        using var nav = new CanvasNavigationService(session);
        nav.ActiveCanvas.SetNodeDescription(new(composite.Id, "Atmosphere", ""));
        nav.OpenChild(composite.Id);
        Assert.Equal("Atmosphere", nav.Path[^1].Name);
        var rack = nav.ActiveCanvas.Snapshot.Graph.Nodes.First();
        var rackDescription = new SavedNodeDescription(rack.Id, "Boundary", "Plain <script> text");
        nav.ActiveCanvas.SetNodeDescription(rackDescription);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, childId, nav.CaptureLayouts());
        var json = serializer.Serialize(document);
        var decoded = serializer.Deserialize(json);
        using var restored = serializer.Restore(decoded, registry);
        using var restoredNav = new CanvasNavigationService(restored, layouts: decoded.CanvasLayouts, activeGraphId: childId);
        Assert.Equal("Atmosphere", restoredNav.Path[^1].Name);
        Assert.Equal(rackDescription, restoredNav.ActiveCanvas.Snapshot.NodeDescriptions[rack.Id]);
        Assert.False(restoredNav.ActiveCanvas.Snapshot.CanUndoAnnotations);
        session.Execute(new RemoveNodeCommand(session.GraphId, composite.Id));
        session.Undo();
        nav.OpenChild(composite.Id);
        Assert.Equal(rackDescription, nav.ActiveCanvas.Snapshot.NodeDescriptions[rack.Id]);
        var child = document.CanvasLayouts.Single(layout => layout.GraphId == childId);
        foreach (var invalid in new[]
        {
            child with { NodeDescriptions = [rackDescription, rackDescription] },
            child with { NodeDescriptions = [rackDescription with { NodeId = composite.Id }] },
            child with { NodeDescriptions = [rackDescription with { Alias = new string('a', 121) }] },
            child with { ConnectionDescriptions = [new(Guid.NewGuid(), "Unknown")] },
            child with { NodeDescriptions = null! }
        })
            Assert.ThrowsAny<ArgumentException>(() => serializer.Restore(document with { CanvasLayouts = [invalid] }, registry));
        var old = serializer.Deserialize(serializer.Serialize(document with { CanvasLayouts = [new(session.GraphId, 0, 0, 1, [])] })
            .Replace(",\"nodeDescriptions\":[]", "").Replace(",\"connectionDescriptions\":[]", ""));
        Assert.Empty(old.CanvasLayouts[0].NodeDescriptions);
        Assert.Empty(old.CanvasLayouts[0].ConnectionDescriptions);
    }
}
