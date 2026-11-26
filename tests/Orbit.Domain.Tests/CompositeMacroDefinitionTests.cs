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
    public void MacroValueCommandsPreserveDefinitionsAndGroupHistoryWithoutChangingManualValues()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var wrapper = session.Snapshot.GetGraph(patch.OuterId).Nodes.Single(n => n.Composite?.Graph.Id == patch.ChildId);
        var macro = new CompositeMacroDefinition(Guid.NewGuid(), "Size", 0.5, 0.5, new([new(patch.Frequency, 200, 400)]));
        session.Execute(new ReplaceCompositeMacrosCommand(patch.OuterId, wrapper.Id, [macro]));
        CompositeMacroDefinition Current() => Assert.Single(session.Snapshot.GetGraph(patch.OuterId).GetNode(wrapper.Id).Composite!.Macros);
        SetCompositeMacroValueCommand Set(double value) => new(patch.OuterId, wrapper.Id, macro.Id, value);
        var initial = session.Snapshot;
        Assert.False(session.Execute(Set(0.5)).Changed);
        foreach (var invalid in new[] { -0.1, 1.1, double.NaN, double.PositiveInfinity })
            Assert.Throws<GraphValidationException>(() => session.Execute(Set(invalid)));
        Assert.Throws<GraphValidationException>(() => session.Execute(Set(0.7) with { MacroId = Guid.NewGuid() }));
        Assert.Same(initial, session.Snapshot);

        var gesture = Guid.NewGuid();
        var changes = new List<CompositeMacroValueChanged>();
        using var subscription = session.Subscribe<CompositeMacroValueChanged>(changes.Add);
        var first = session.Execute(Set(0.25), gesture).ChangeSet!;
        Assert.False(first.TopologyChanged);
        session.Execute(Set(0.75), gesture);
        session.CompleteHistoryGesture(gesture);
        Assert.Equal(2, changes.Count);
        Assert.Equal(0.5, changes[0].PreviousValue);
        Assert.Equal(0.75, changes[1].CurrentValue);
        Assert.Equal(initial.Revision + 2, session.Snapshot.Revision);
        Assert.Equal(macro.Id, Current().Id);
        Assert.Equal(macro.DefaultValue, Current().DefaultValue);
        Assert.Same(macro.Mapping, Current().Mapping);
        Assert.Equal(0.5, Assert.Single(initial.GetGraph(patch.OuterId).GetNode(wrapper.Id).Composite!.Macros).BaseValue);
        Assert.Equal(440, session.Snapshot.GetGraph(patch.ChildId).GetNode(patch.Frequency.NodeId)
            .Parameters.Single(p => p.Id == patch.Frequency.ParameterId).BaseValue);
        var program = Orbit.Audio.Compilation.FaustGraphCompiler.CreateBuiltIn().Compile(session.Snapshot);
        Assert.Equal(350, program.Controls.Single(c => c.GraphId == patch.ChildId && c.Address == patch.Frequency).InitialValue);

        session.Undo();
        Assert.Equal(0.5, Current().BaseValue);
        session.Redo();
        Assert.Equal(0.75, Current().BaseValue);
        session.Undo();
        var cancel = Guid.NewGuid();
        session.Execute(Set(0.1), cancel);
        Assert.False(session.CancelParameterGesture(Guid.NewGuid()).Changed);
        Assert.Equal(0.1, Current().BaseValue);
        Assert.IsType<CompositeMacroValueChanged>(Assert.Single(session.CancelParameterGesture(cancel).ChangeSet!.Events));
        Assert.Equal(0.5, Current().BaseValue);
        Assert.True(session.CanRedo);
        session.Redo();
        Assert.Equal(0.75, Current().BaseValue);
    }

    [Fact]
    public void MacroAndParameterGesturesWithSameIdentityStaySeparate()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var wrapper = session.Snapshot.GetGraph(patch.OuterId).Nodes.Single(n => n.Composite?.Graph.Id == patch.ChildId);
        var macro = new CompositeMacroDefinition(Guid.NewGuid(), "Size", 0.5, 0.5, new([new(patch.Frequency, 200, 400)]));
        session.Execute(new ReplaceCompositeMacrosCommand(patch.OuterId, wrapper.Id, [macro]));
        var gesture = Guid.NewGuid();
        session.Execute(new SetCompositeMacroValueCommand(patch.OuterId, wrapper.Id, macro.Id, 0.75), gesture);
        session.Execute(new SetParameterValueCommand(patch.ChildId, patch.Frequency, 330), gesture);
        session.Undo();
        Assert.Equal(440, session.Snapshot.GetGraph(patch.ChildId).GetNode(patch.Frequency.NodeId)
            .Parameters.Single(p => p.Id == patch.Frequency.ParameterId).BaseValue);
        Assert.Equal(0.75, Assert.Single(session.Snapshot.GetGraph(patch.OuterId).GetNode(wrapper.Id).Composite!.Macros).BaseValue);
        session.Undo();
        Assert.Equal(0.5, Assert.Single(session.Snapshot.GetGraph(patch.OuterId).GetNode(wrapper.Id).Composite!.Macros).BaseValue);
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
