using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class CompositeMacroDefinitionTests
{
    [Fact]
    public void DefinitionsAreAtomicAndReserveTargetsWithoutChangingManualValues()
    {
        var composite = new CompositeNode(Guid.NewGuid(), "Drum", [], []);
        var oscillator = new OscillatorGenerator(Guid.NewGuid());
        composite.InternalGraph.AddNode(oscillator);
        var address = new ParameterAddress(oscillator.Id, oscillator.Parameters.Single(p => p.Definition.Key == "frequency").Id);
        var size = new CompositeMacroDefinition(Guid.NewGuid(), "Size", 0.5, 0.8, new([new(address, 120, 45)]));
        var proposed = new List<CompositeMacroDefinition> { size };
        Assert.True(composite.ReplaceMacros(proposed));
        proposed.Clear();
        Assert.Same(size, Assert.Single(composite.Macros));
        Assert.False(composite.ReplaceMacros([new(size.Id, size.Name, size.DefaultValue, size.BaseValue, new(size.Mapping.Bindings))]));
        var conflict = new CompositeMacroDefinition(Guid.NewGuid(), "Other", 0, 1, new([new(address, 80, 40)]));
        Assert.Throws<GraphValidationException>(() => composite.ReplaceMacros([size, conflict]));
        var orphan = new CompositeMacroDefinition(Guid.NewGuid(), "Other", 0, 1,
            new([new(new ParameterAddress(Guid.NewGuid(), Guid.NewGuid()), 0, 1)]));
        Assert.Throws<GraphValidationException>(() => composite.ReplaceMacros([size, orphan]));
        Assert.Same(size, Assert.Single(composite.Macros));
        Assert.Throws<GraphValidationException>(() => composite.InternalGraph.RemoveNode(oscillator.Id));
        Assert.Equal(440, composite.InternalGraph.GetParameter(address).BaseValue);
        composite.InternalGraph.SetParameterValue(address, 220);
        Assert.Equal(220, composite.InternalGraph.GetParameter(address).BaseValue);
        Assert.True(composite.ReplaceMacros([]));
        composite.InternalGraph.RemoveNode(oscillator.Id);
    }

    [Fact]
    public void DefinitionsRoundTripUndoAndCloneWithRemappedTargets()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Drum", [], []));
        using var session = new PatchSession(registry);
        var wrapper = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var child = wrapper.Composite!.Graph.Id;
        var osc = session.Execute(new AddNodeCommand(child, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var address = new ParameterAddress(osc.Id, osc.Parameters.Single(p => p.Definition.Key == "frequency").Id);
        var size = new CompositeMacroDefinition(Guid.NewGuid(), "Size", 0.5, 0.75, new([new(address, 120, 45, 2)]));
        var before = session.Snapshot.Revision;
        var command = new ReplaceCompositeMacrosCommand(session.GraphId, wrapper.Id, [size]);
        Assert.IsType<CompositeMacrosChanged>(Assert.Single(session.Execute(command).ChangeSet!.Events));
        Assert.Equal(before + 1, session.Snapshot.Revision);
        Assert.False(session.Execute(command).Changed);
        session.Undo();
        Assert.Empty(session.Snapshot.GetNode(wrapper.Id).Composite!.Macros);
        session.Redo();
        Assert.Equal(size.Id, Assert.Single(session.Snapshot.GetNode(wrapper.Id).Composite!.Macros).Id);

        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, session.GraphId, []);
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(document)), registry);
        var loaded = Assert.Single(restored.Snapshot.GetNode(wrapper.Id).Composite!.Macros);
        Assert.Equal(size.Id, loaded.Id);
        Assert.Equal(size.BaseValue, loaded.BaseValue);
        Assert.Equal(size.DefaultValue, loaded.DefaultValue);
        Assert.Equal(size.Mapping.Bindings.ToArray(), loaded.Mapping.Bindings.ToArray());

        var duplicateId = Guid.NewGuid();
        session.Execute(new DuplicateNodeCommand(session.GraphId, wrapper.Id, duplicateId));
        var duplicate = session.Snapshot.GetNode(duplicateId).Composite!;
        var cloned = Assert.Single(duplicate.Macros);
        var target = Assert.Single(cloned.Mapping.Bindings).Target;
        Assert.NotEqual(size.Id, cloned.Id);
        Assert.NotEqual(address.NodeId, target.NodeId);
        Assert.NotEqual(address.ParameterId, target.ParameterId);
        Assert.Equal(440, duplicate.Graph.GetNode(target.NodeId).Parameters.Single(p => p.Id == target.ParameterId).BaseValue);
        session.Execute(new ReplaceCompositeMacrosCommand(session.GraphId, duplicateId, []));
        Assert.Single(session.Snapshot.GetNode(wrapper.Id).Composite!.Macros);
        Assert.Empty(session.Snapshot.GetNode(duplicateId).Composite!.Macros);
    }
}
