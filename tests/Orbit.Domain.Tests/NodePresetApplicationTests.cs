using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Presets;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Infrastructure.Presets;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class NodePresetApplicationTests
{
    [Fact]
    public void DomainValidatesEveryValueBeforeChangingAnyParameter()
    {
        var graph = new SignalGraph();
        var oscillator = new OscillatorGenerator(Guid.NewGuid());
        graph.AddNode(oscillator);
        var original = oscillator.Parameters.Select(parameter => parameter.BaseValue).ToArray();
        var values = oscillator.Parameters.ToDictionary(parameter => parameter.Definition.Key, parameter => parameter.BaseValue);
        values["frequency"] = 110;
        values["division"] = -1; // Last, inactive discrete value must still be validated.
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.SetNodeParameterValues(oscillator.Id, oscillator.TypeKey, values));
        Assert.Equal(original, oscillator.Parameters.Select(parameter => parameter.BaseValue));
        values.Remove("division");
        Assert.Throws<GraphValidationException>(() => graph.SetNodeParameterValues(oscillator.Id, oscillator.TypeKey, values));
        values["unknown"] = 0;
        Assert.Throws<GraphValidationException>(() => graph.SetNodeParameterValues(oscillator.Id, oscillator.TypeKey, values));
        Assert.Equal(original, oscillator.Parameters.Select(parameter => parameter.BaseValue));
    }

    [Fact]
    public void NestedPresetCommitsOncePreservingIdentitiesDormantRoutesAndMute()
    {
        using var patch = new NestedFaustPatch();
        var session = patch.Session;
        var childId = patch.ChildId;
        var nodeId = patch.Frequency.NodeId;
        session.Execute(new SetNodeMutedCommand(childId, nodeId, true));
        var before = session.Snapshot;
        var original = before.GetGraph(childId).GetNode(nodeId);
        var frequency = original.Parameters.Single(parameter => parameter.Definition.Key == "frequency");
        var preset = BuiltIn("oscillator-quarter-pulse");
        var commits = new List<GraphChangeSet>();
        var facts = new List<ParameterChanged>();
        using var subscription = session.Subscribe(commits.Add);
        using var typedSubscription = session.Subscribe<ParameterChanged>(facts.Add, childId);

        var command = new ApplyNodePresetCommand(childId, nodeId, preset);
        var change = session.Execute(command).ChangeSet!;
        Assert.Same(change, Assert.Single(commits));
        Assert.Equal(before.Revision + 1, change.Revision);
        Assert.Equal(childId, change.GraphId);
        Assert.Equal(original.Parameters.Where(p => p.BaseValue != preset.Parameters[p.Definition.Key]).Select(p => p.Id),
            facts.Select(fact => fact.Address.ParameterId));
        Assert.Equal(facts.ToArray(), change.Events.Cast<ParameterChanged>().ToArray());
        foreach (var fact in facts)
        {
            var parameter = original.Parameters.Single(p => p.Id == fact.Address.ParameterId);
            Assert.Equal(parameter.BaseValue, fact.PreviousValue);
            Assert.Equal(preset.Parameters[parameter.Definition.Key], fact.CurrentValue);
        }
        var applied = change.ChangedGraph.GetNode(nodeId);
        AssertConfiguration(preset, applied);
        Assert.Equal(original.Parameters.Select(p => p.Id), applied.Parameters.Select(p => p.Id));
        Assert.Equal(original.Inputs.ToArray(), applied.Inputs.ToArray());
        Assert.Equal(original.Outputs.ToArray(), applied.Outputs.ToArray());
        Assert.Equal(before.GetGraph(childId).Connections.ToArray(), change.ChangedGraph.Connections.ToArray());
        Assert.Equal(before.Connections.ToArray(), change.Snapshot.Connections.ToArray());
        Assert.True(applied.IsMuted);
        Assert.False(applied.IsInputActive(frequency.ModulationPortId!.Value));
        Assert.False(session.Execute(command).Changed);
        Assert.Single(commits);
        Assert.Same(change.Snapshot, session.Snapshot);

        session.Undo();
        var undone = session.Snapshot.GetGraph(childId).GetNode(nodeId);
        Assert.Equal(original.Parameters.Select(p => p.BaseValue), undone.Parameters.Select(p => p.BaseValue));
        Assert.True(undone.IsInputActive(frequency.ModulationPortId.Value));
        Assert.True(undone.IsMuted);
        session.Redo();
        AssertConfiguration(preset, session.Snapshot.GetGraph(childId).GetNode(nodeId));
        Assert.Equal(before.GetGraph(childId).Connections.ToArray(), session.Snapshot.GetGraph(childId).Connections.ToArray());
    }

    [Fact]
    public void RejectedPresetsPreserveCommittedSnapshotHistoryAndSubscribers()
    {
        using var session = ModulationDemoPatch.OpenSession(new());
        var before = session.Snapshot;
        var oscillator = before.Nodes.First(node => node.TypeKey == OscillatorGenerator.NodeType);
        var output = before.Nodes.Single(node => node.Parameters.Count == 0);
        var preset = BuiltIn("oscillator-deep-sine");
        var invalidValues = preset.Parameters.ToDictionary(pair => pair.Key, pair => pair.Value);
        invalidValues["phase"] = 361;
        var invalid = new NodePreset(1, "invalid", preset.NodeType, "Invalid", "Test", null, invalidValues);
        var ineligible = new NodePreset(1, "ineligible", output.TypeKey, "Invalid", "Test", null, new Dictionary<string, double>());
        var commits = new List<GraphChangeSet>();
        using var subscription = session.Subscribe(commits.Add);
        Assert.Throws<ArgumentException>(() => session.Execute(new ApplyNodePresetCommand(before.Id, oscillator.Id, invalid)));
        Assert.Throws<ArgumentException>(() => session.Execute(new ApplyNodePresetCommand(before.Id, output.Id, ineligible)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new ApplyNodePresetCommand(before.Id, oscillator.Id, BuiltIn("gain-half-level"))));
        Assert.Throws<GraphValidationException>(() => session.Execute(new ApplyNodePresetCommand(before.Id, Guid.NewGuid(), preset)));
        Assert.Throws<GraphValidationException>(() => session.Execute(new ApplyNodePresetCommand(Guid.NewGuid(), oscillator.Id, preset)));
        Assert.Same(before, session.Snapshot);
        Assert.Empty(commits);
        Assert.False(session.CanUndo);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void SingleValuePresetFinalizesSliderGestureAndPreservesBypass()
    {
        using var session = ModulationDemoPatch.OpenSession(new());
        var graphId = session.GraphId;
        var gain = session.Snapshot.Nodes.Single(node => node.TypeKey == GainProcessor.NodeType);
        var parameter = gain.Parameters.Single();
        var gesture = Guid.NewGuid();
        session.Execute(new SetNodeBypassedCommand(graphId, gain.Id, true));
        var address = new ParameterAddress(gain.Id, parameter.Id);
        session.Execute(new SetParameterValueCommand(graphId, address, 0.25), gesture);
        // Even a caller-supplied matching gesture ID must not merge a preset with the slider.
        session.Execute(new ApplyNodePresetCommand(graphId, gain.Id, BuiltIn("gain-half-level")), gesture);
        Assert.Equal(0.5, Current().Parameters.Single().BaseValue);
        session.Undo();
        Assert.Equal(0.25, Current().Parameters.Single().BaseValue);
        Assert.True(Current().IsBypassed);
        session.Undo();
        Assert.Equal(parameter.BaseValue, Current().Parameters.Single().BaseValue);
        Assert.True(Current().IsBypassed);
        session.Redo();
        session.Redo();
        Assert.Equal(0.5, Current().Parameters.Single().BaseValue);
        Assert.True(Current().IsBypassed);

        GraphNodeSnapshot Current() => session.Snapshot.GetNode(gain.Id);
    }

    private static NodePreset BuiltIn(string id) =>
        new EmbeddedNodePresetCatalog().LoadBuiltInPresets().Single(preset => preset.Id == id);

    private static void AssertConfiguration(NodePreset preset, GraphNodeSnapshot node)
    {
        foreach (var parameter in node.Parameters)
            Assert.Equal(preset.Parameters[parameter.Definition.Key], parameter.BaseValue);
    }
}
