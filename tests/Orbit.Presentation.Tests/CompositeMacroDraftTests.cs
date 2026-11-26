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
