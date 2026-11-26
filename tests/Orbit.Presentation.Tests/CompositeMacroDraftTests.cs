using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class CompositeMacroDraftTests
{
    [Fact]
    public void PreviewUsesIndependentCurvesAndDormantManualValuesWithoutMutatingPatch()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Test", [], []));
        using var session = new PatchSession(registry);
        var wrapper = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var child = wrapper.Composite!.Graph.Id;
        var oscillator = session.Execute(new AddNodeCommand(child, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var nested = session.Execute(new AddNodeCommand(child, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var grandchild = session.Execute(new AddNodeCommand(nested.Composite!.Graph.Id, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        ParameterAddress Address(string key) => new(oscillator.Id, oscillator.Parameters.Single(p => p.Definition.Key == key).Id);
        var frequency = Address("frequency");
        var width = Address(OscillatorGenerator.PulseWidthKey);
        var before = session.Snapshot;
        var draft = new CompositeMacroDraft(before, session.GraphId, wrapper.Id);
        Assert.DoesNotContain(draft.Targets, target => target.Address == Address("waveform") || target.Address.NodeId == grandchild.Id);
        Assert.False(draft.Targets.Single(target => target.Address == width).IsActive);
        var macro = new CompositeMacroDefinition(Guid.NewGuid(), "Size", 0.5, 0.5,
            new([new(frequency, 440, 240, 2), new(width, 0.2, 0.4)]));
        var rows = draft.Preview([macro]);
        Assert.Equal(390, rows[0].MappedBase);
        Assert.Equal(390, rows[0].EffectiveBase);
        Assert.Equal(440, rows[0].Target.ManualBase);
        Assert.Equal(0.3, rows[1].MappedBase, 10);
        Assert.Equal(0.5, rows[1].EffectiveBase);
        Assert.Same(before, session.Snapshot);

        var list = new List<CompositeMacroDefinition> { macro };
        var command = draft.CreateCommand(list);
        list.Clear();
        Assert.False(session.Execute(command).ChangeSet!.TopologyChanged);
        var reopened = new CompositeMacroDraft(session.Snapshot, session.GraphId, wrapper.Id);
        Assert.Equal(macro.Id, reopened.Targets.Single(t => t.Address == frequency).ExistingOwner);
        Assert.Same(macro, Assert.Single(reopened.OriginalMacros));
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, session.GraphId, []);
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(document)), registry);
        Assert.Equal(macro.Id, Assert.Single(restored.Snapshot.GetNode(wrapper.Id).Composite!.Macros).Id);
        session.Undo();
        Assert.Empty(session.Snapshot.GetNode(wrapper.Id).Composite!.Macros);
        session.Redo();
        Assert.Equal(macro.Id, Assert.Single(session.Snapshot.GetNode(wrapper.Id).Composite!.Macros).Id);
    }

    [Fact]
    public void OwnershipProjectionIsLocalStableAndPreservesManualFallback()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Test", [], []));
        using var session = new PatchSession(registry);
        var wrapper = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var child = wrapper.Composite!.Graph.Id;
        var oscillator = session.Execute(new AddNodeCommand(child, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var nested = session.Execute(new AddNodeCommand(child, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var grandchild = nested.Composite!.Graph.Id;
        var inner = session.Execute(new AddNodeCommand(grandchild, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        ParameterAddress Address(Orbit.Application.Graphs.Snapshots.GraphNodeSnapshot node, string key) =>
            new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id);
        var frequency = Address(oscillator, "frequency");
        var width = Address(oscillator, OscillatorGenerator.PulseWidthKey);
        var innerFrequency = Address(inner, "frequency");
        var macro = new CompositeMacroDefinition(Guid.NewGuid(), "Size", 0.5, 0.5,
            new([new(frequency, 440, 240, 2), new(width, 0.2, 0.4)]));
        var innerMacro = new CompositeMacroDefinition(Guid.NewGuid(), "Inner", 0.5, 0.5,
            new([new(innerFrequency, 100, 500)]));
        session.Execute(new ReplaceCompositeMacrosCommand(session.GraphId, wrapper.Id, [macro]));
        session.Execute(new ReplaceCompositeMacrosCommand(child, nested.Id, [innerMacro]));
        var projection = new MacroOwnershipProjection();
        Assert.Empty(projection.Update(session.Snapshot, session.GraphId, null));
        var first = projection.Update(session.Snapshot, child, session.GraphId);
        Assert.Same(first, projection.Update(session.Snapshot, child, session.GraphId));
        var rows = first[oscillator.Id];
        var active = rows.Single(row => row.Address == frequency);
        var dormant = rows.Single(row => row.Address == width);
        Assert.Equal("Size", active.MacroName);
        Assert.Equal(440, active.ManualBase);
        Assert.Equal(390, active.MappedBase);
        Assert.Equal(390, active.EffectiveBase);
        Assert.False(dormant.IsActive);
        Assert.Equal(0.3, dormant.MappedBase, 10);
        Assert.Equal(0.5, dormant.EffectiveBase);
        session.Execute(new SetParameterValueCommand(grandchild, innerFrequency, 330));
        Assert.Same(rows, projection.Update(session.Snapshot, child, session.GraphId)[oscillator.Id]);
        session.Execute(new SetParameterValueCommand(child, frequency, 350));
        var edited = projection.Update(session.Snapshot, child, session.GraphId)[oscillator.Id];
        Assert.Equal(350, edited.Single(row => row.Address == frequency).ManualBase);
        Assert.Equal(390, edited.Single(row => row.Address == frequency).EffectiveBase);
        session.Execute(new SetCompositeMacroValueCommand(session.GraphId, wrapper.Id, macro.Id, 0.75));
        var moved = projection.Update(session.Snapshot, child, session.GraphId)[oscillator.Id];
        Assert.Equal(327.5, moved.Single(row => row.Address == frequency).MappedBase);
        Assert.Equal(350, moved.Single(row => row.Address == frequency).ManualBase);
        var innerRows = projection.Update(session.Snapshot, grandchild, child);
        Assert.False(innerRows.ContainsKey(oscillator.Id));
        Assert.Equal("Inner", Assert.Single(innerRows[inner.Id]).MacroName);
        Assert.Empty(projection.Update(session.Snapshot, session.GraphId, null));
        session.Execute(new ReplaceCompositeMacrosCommand(session.GraphId, wrapper.Id, []));
        Assert.Empty(projection.Update(session.Snapshot, child, session.GraphId));
        Assert.Equal(350, session.Snapshot.GetGraph(child).GetNode(oscillator.Id).Parameters.Single(p => p.Id == frequency.ParameterId).BaseValue);
        session.Undo();
        Assert.Equal("Size", projection.Update(session.Snapshot, child, session.GraphId)[oscillator.Id][0].MacroName);
        session.Redo();
        Assert.Empty(projection.Update(session.Snapshot, child, session.GraphId));
    }

    [Fact]
    public void InvalidAndStaleDraftsCannotOverwriteCommittedConfiguration()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Test", [], []));
        using var session = new PatchSession(registry);
        var wrapper = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var child = wrapper.Composite!.Graph.Id;
        var oscillator = session.Execute(new AddNodeCommand(child, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var frequency = new ParameterAddress(oscillator.Id, oscillator.Parameters.Single(p => p.Definition.Key == "frequency").Id);
        var draft = new CompositeMacroDraft(session.Snapshot, session.GraphId, wrapper.Id);
        var valid = new CompositeMacroDefinition(Guid.NewGuid(), "Power", 0, 0.5, new([new(frequency, 100, 500)]));
        var conflicting = new CompositeMacroDefinition(Guid.NewGuid(), "Other", 0, 0.5, valid.Mapping);
        var invalid = new CompositeMacroDefinition(Guid.NewGuid(), "Bad", 0, 0.5, new([new(frequency, -1, 500)]));
        var foreign = new CompositeMacroDefinition(Guid.NewGuid(), "Foreign", 0, 0.5,
            new([new(new ParameterAddress(Guid.NewGuid(), Guid.NewGuid()), 0, 1)]));
        var before = session.Snapshot;
        Assert.Throws<GraphValidationException>(() => draft.Preview([valid, conflicting]));
        Assert.Throws<GraphValidationException>(() => draft.CreateCommand([invalid]));
        Assert.Throws<GraphValidationException>(() => draft.Preview([foreign]));
        Assert.Same(before, session.Snapshot);
        var stale = draft.CreateCommand([valid]);
        session.Execute(new SetParameterValueCommand(child, frequency, 330));
        var current = session.Snapshot;
        Assert.Throws<GraphValidationException>(() => session.Execute(stale));
        Assert.Same(current, session.Snapshot);
        Assert.Empty(session.Snapshot.GetNode(wrapper.Id).Composite!.Macros);
    }
}
