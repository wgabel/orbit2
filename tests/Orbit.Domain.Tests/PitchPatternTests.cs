using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Models;
using Orbit.Application.Persistence;
using Orbit.Application.Presets;
using Orbit.Domain.Nodes.Generators;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class PitchPatternTests
{
    [Fact]
    public void ArticulationExtendsPortsWithoutChangingExistingPitchIdentity()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var id = session.GraphId;
        var node = session.Execute(new AddNodeCommand(id, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        Assert.Equal(new[] { "pitch-out", "gate-out", "trigger-out", "velocity-out" }, node.Outputs.Select(p => p.Key));
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, id, []);
        var saved = document.RootGraph.Nodes.Single();
        var removed = new[] { NoteSequencerGenerator.ArticulationKey, NoteSequencerGenerator.GateLengthKey, NoteSequencerGenerator.VelocityKey };
        var legacy = saved with
        {
            Parameters = saved.Parameters.Where(p => !removed.Contains(p.Key)).ToArray(),
            Ports = saved.Ports.Where(p => p.Key == "pitch-out").ToArray()
        };
        using var restored = serializer.Restore(document with { RootGraph = document.RootGraph with { Nodes = [legacy] } }, registry);
        var migrated = restored.Snapshot.GetNode(node.Id);
        Assert.Equal(node.Outputs.Single(p => p.Key == "pitch-out").Id, migrated.Outputs.Single(p => p.Key == "pitch-out").Id);
        Assert.Equal(4, migrated.Outputs.Count);
        Assert.Equal(0, migrated.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.ArticulationKey).BaseValue);
        Assert.Equal(0.9, migrated.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.GateLengthKey).BaseValue);
        Assert.Equal(0.8, migrated.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.VelocityKey).BaseValue);
        Assert.Equal(64, PitchPattern.FromNode(migrated).Steps.Count);
    }

    [Fact]
    public void PerStepLengthsMigrateAndRoundTripWithUndo()
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var id = session.GraphId;
        var node = session.Execute(new AddNodeCommand(id, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, id, []);
        var saved = document.RootGraph.Nodes.Single();
        var legacy = saved with { Parameters = saved.Parameters.Where(p => !NoteSequencerGenerator.IsStepGateLengthParameter(p.Key)).ToArray() };
        using var restored = serializer.Restore(document with { RootGraph = document.RootGraph with { Nodes = [legacy] } }, registry);
        Assert.All(PitchPattern.FromNode(restored.Snapshot.GetNode(node.Id)).GateFractions!, v => Assert.Equal(0d, v));
        var preset = new NodePreset(1, "old-length", node.TypeKey, "Old lengths", "Custom", null, legacy.Parameters.ToDictionary(p => p.Key, p => p.BaseValue));
        new NodePresetValidator(registry).Validate(preset);
        var baseline = PitchPattern.FromNode(session.Snapshot.GetNode(node.Id));
        var address = new Orbit.Domain.Parameters.ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.StepGateLengthKey(0)).Id);
        session.Execute(new SetParameterValueCommand(id, address, .25));
        var edited = PitchPattern.FromNode(session.Snapshot.GetNode(node.Id));
        Assert.False(baseline.Matches(edited));
        Assert.Equal(.25, edited.GateFractions![0]);
        using var roundtrip = serializer.Restore(serializer.Deserialize(serializer.Serialize(serializer.Capture(session, 120, id, []))), registry);
        Assert.True(edited.Matches(PitchPattern.FromNode(roundtrip.Snapshot.GetNode(node.Id))));
        session.Undo();
        Assert.True(baseline.Matches(PitchPattern.FromNode(session.Snapshot.GetNode(node.Id))));
    }

    [Fact]
    public void FixedSlotsMigratePreserveInactiveNotesAndUndoAtomically()
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var id = session.GraphId;
        var node = session.Execute(new AddNodeCommand(id, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, id, []);
        var saved = document.RootGraph.Nodes.Single();
        var legacy = saved with { Parameters = saved.Parameters.Where(p => !NoteSequencerGenerator.IsPatternParameter(p.Key)).ToArray() };
        using var restored = serializer.Restore(document with { RootGraph = document.RootGraph with { Nodes = [legacy] } }, registry);
        var migrated = restored.Snapshot.GetNode(node.Id);
        Assert.All(legacy.Parameters, p => Assert.Contains(migrated.Parameters, a => a.Id == p.Id));
        Assert.Equal(64, PitchPattern.FromNode(migrated).Steps.Count);
        Assert.False(PitchPattern.FromNode(migrated).Loop);
        var preset = new NodePreset(1, "old", node.TypeKey, "Old", "Custom", null, legacy.Parameters.ToDictionary(p => p.Key, p => p.BaseValue));
        new NodePresetValidator(registry).Validate(preset);
        var notes = new int[64]; notes[0] = 46; notes[63] = 58;
        session.Execute(new SetPitchPatternStepsCommand(id, node.Id, notes));
        session.Execute(new SetParameterValueCommand(id, new(node.Id, node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.CountKey).Id), 1));
        Assert.Equal(58, PitchPattern.FromNode(session.Snapshot.GetNode(node.Id)).Steps[63]);
        var revision = session.Snapshot.Revision;
        session.Execute(new SetPitchPatternStepsCommand(id, node.Id, new int[64]));
        Assert.Equal(revision + 1, session.Snapshot.Revision);
        Assert.All(PitchPattern.FromNode(session.Snapshot.GetNode(node.Id)).Steps, code => Assert.Equal(0, code));
        session.Undo();
        Assert.Equal(notes, PitchPattern.FromNode(session.Snapshot.GetNode(node.Id)).Steps);
        using var roundtrip = serializer.Restore(serializer.Deserialize(serializer.Serialize(serializer.Capture(session, 120, id, []))), registry);
        Assert.True(PitchPattern.FromNode(session.Snapshot.GetNode(node.Id)).Matches(PitchPattern.FromNode(roundtrip.Snapshot.GetNode(node.Id))));
    }
}
