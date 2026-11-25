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
    public void VelocityOverridesMigratePreserveIdentityAndRoundTripWithUndo()
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var id = session.GraphId;
        var node = session.Execute(new AddNodeCommand(id, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, id, []);
        var saved = document.RootGraph.Nodes.Single();
        var legacy = saved with { Parameters = saved.Parameters.Where(p => !NoteSequencerGenerator.IsStepVelocityParameter(p.Key)).ToArray() };
        using var restored = serializer.Restore(document with { RootGraph = document.RootGraph with { Nodes = [legacy] } }, registry);
        var migrated = restored.Snapshot.GetNode(node.Id);
        Assert.All(legacy.Parameters, p => Assert.Contains(migrated.Parameters, a => a.Id == p.Id && a.BaseValue == p.BaseValue));
        var baseline = PitchPattern.FromNode(migrated);
        Assert.Equal(64, baseline.VelocityOverrides!.Count);
        Assert.All(baseline.VelocityOverrides!, v => Assert.Equal(NoteSequencerGenerator.InheritVelocity, v));
        Assert.True(baseline.Matches(baseline with { VelocityOverrides = null }));
        var validator = new NodePresetValidator(registry);
        var preset = new NodePreset(1, "old-velocity", node.TypeKey, "Old velocity", "Custom", null, legacy.Parameters.ToDictionary(p => p.Key, p => p.BaseValue));
        validator.Validate(preset);
        Assert.Equal(NoteSequencerGenerator.InheritVelocity, preset.Parameters[NoteSequencerGenerator.StepVelocityKey(0)]);

        var address = new Orbit.Domain.Parameters.ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.StepVelocityKey(0)).Id);
        var hidden = new Orbit.Domain.Parameters.ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.StepVelocityKey(63)).Id);
        session.Execute(new SetParameterValueCommand(id, hidden, .73));
        session.Execute(new SetParameterValueCommand(id, address, 0));
        var edited = PitchPattern.FromNode(session.Snapshot.GetNode(node.Id));
        Assert.False(baseline.Matches(edited));
        Assert.Equal(0d, edited.VelocityOverrides![0]);
        Assert.Equal(.73, edited.VelocityOverrides![63]);
        Assert.Equal(16, edited.Count);
        Assert.Equal(.8, edited.Velocity);
        var currentDocument = serializer.Capture(session, 120, id, []);
        using var roundtrip = serializer.Restore(serializer.Deserialize(serializer.Serialize(currentDocument)), registry);
        Assert.True(edited.Matches(PitchPattern.FromNode(roundtrip.Snapshot.GetNode(node.Id))));
        session.Undo();
        Assert.Equal(NoteSequencerGenerator.InheritVelocity, PitchPattern.FromNode(session.Snapshot.GetNode(node.Id)).VelocityOverrides![0]);
        session.Redo();
        Assert.True(edited.Matches(PitchPattern.FromNode(session.Snapshot.GetNode(node.Id))));

        var partial = currentDocument.RootGraph.Nodes.Single();
        partial = partial with { Parameters = partial.Parameters.Where(p => p.Key != NoteSequencerGenerator.StepVelocityKey(0)).ToArray() };
        Assert.Throws<ArgumentException>(() => serializer.Restore(currentDocument with { RootGraph = currentDocument.RootGraph with { Nodes = [partial] } }, registry));
        Assert.Throws<ArgumentException>(() => validator.Validate(new NodePreset(1, "partial-velocity", node.TypeKey, "Partial velocity", "Custom", null, partial.Parameters.ToDictionary(p => p.Key, p => p.BaseValue))));
    }

    [Theory]
    [InlineData(-1.01)]
    [InlineData(-.5)]
    [InlineData(1.01)]
    [InlineData(.123)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidVelocityOverridesAreRejectedWithoutHistory(double value)
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var id = session.GraphId;
        var node = session.Execute(new AddNodeCommand(id, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var parameter = node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.StepVelocityKey(0));
        var revision = session.Snapshot.Revision;
        Assert.False(parameter.Definition.IsValidBaseValue(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Execute(new SetParameterValueCommand(id, new(node.Id, parameter.Id), value)));
        Assert.Equal(revision, session.Snapshot.Revision);
        Assert.Equal(NoteSequencerGenerator.InheritVelocity, PitchPattern.FromNode(session.Snapshot.GetNode(node.Id)).VelocityOverrides![0]);
    }

    [Fact]
    public void TimingOffsetsMigratePreserveIdentityAndRoundTripWithUndo()
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var id = session.GraphId;
        var node = session.Execute(new AddNodeCommand(id, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, id, []);
        var saved = document.RootGraph.Nodes.Single();
        var legacy = saved with { Parameters = saved.Parameters.Where(p => !NoteSequencerGenerator.IsStepTimingOffsetParameter(p.Key)).ToArray() };
        using var restored = serializer.Restore(document with { RootGraph = document.RootGraph with { Nodes = [legacy] } }, registry);
        var migrated = restored.Snapshot.GetNode(node.Id);
        Assert.All(legacy.Parameters, p => Assert.Contains(migrated.Parameters, a => a.Id == p.Id && a.BaseValue == p.BaseValue));
        var baseline = PitchPattern.FromNode(migrated);
        Assert.Equal(NoteSequencerGenerator.Capacity, baseline.TimingOffsets!.Count);
        Assert.All(baseline.TimingOffsets!, value => Assert.Equal(0d, value));
        Assert.True(baseline.Matches(baseline with { TimingOffsets = null }));
        var validator = new NodePresetValidator(registry);
        var preset = new NodePreset(1, "old-offset", node.TypeKey, "Old offsets", "Custom", null, legacy.Parameters.ToDictionary(p => p.Key, p => p.BaseValue));
        validator.Validate(preset);
        Assert.Equal(0d, preset.Parameters[NoteSequencerGenerator.StepTimingOffsetKey(0)]);

        var first = new Orbit.Domain.Parameters.ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.StepTimingOffsetKey(0)).Id);
        var hidden = new Orbit.Domain.Parameters.ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.StepTimingOffsetKey(63)).Id);
        session.Execute(new SetParameterValueCommand(id, hidden, .45));
        session.Execute(new SetParameterValueCommand(id, first, -.45));
        var edited = PitchPattern.FromNode(session.Snapshot.GetNode(node.Id));
        Assert.False(baseline.Matches(edited));
        Assert.Equal(-.45, edited.TimingOffsets![0]);
        Assert.Equal(.45, edited.TimingOffsets![63]);
        Assert.Equal(16, edited.Count);
        var current = serializer.Capture(session, 120, id, []);
        using var roundtrip = serializer.Restore(serializer.Deserialize(serializer.Serialize(current)), registry);
        Assert.True(edited.Matches(PitchPattern.FromNode(roundtrip.Snapshot.GetNode(node.Id))));
        session.Undo();
        Assert.Equal(0d, PitchPattern.FromNode(session.Snapshot.GetNode(node.Id)).TimingOffsets![0]);
        session.Redo();
        Assert.True(edited.Matches(PitchPattern.FromNode(session.Snapshot.GetNode(node.Id))));

        var partial = current.RootGraph.Nodes.Single();
        partial = partial with { Parameters = partial.Parameters.Where(p => p.Key != NoteSequencerGenerator.StepTimingOffsetKey(0)).ToArray() };
        Assert.Throws<ArgumentException>(() => serializer.Restore(current with { RootGraph = current.RootGraph with { Nodes = [partial] } }, registry));
        Assert.Throws<ArgumentException>(() => validator.Validate(new NodePreset(1, "partial-offset", node.TypeKey, "Partial offsets", "Custom", null, partial.Parameters.ToDictionary(p => p.Key, p => p.BaseValue))));
    }

    [Theory]
    [InlineData(-.46)]
    [InlineData(.46)]
    [InlineData(.123)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidTimingOffsetsAreRejectedWithoutHistory(double value)
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var id = session.GraphId;
        var node = session.Execute(new AddNodeCommand(id, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var parameter = node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.StepTimingOffsetKey(0));
        var revision = session.Snapshot.Revision;
        Assert.False(parameter.Definition.IsValidBaseValue(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Execute(new SetParameterValueCommand(id, new(node.Id, parameter.Id), value)));
        Assert.Equal(revision, session.Snapshot.Revision);
        Assert.Equal(0d, PitchPattern.FromNode(session.Snapshot.GetNode(node.Id)).TimingOffsets![0]);
    }

    [Fact]
    public void HumanizationIsDeterministicBoundedAndDoesNotChangeTheScore()
    {
        var steps = new[] { 46, 0, 129, 53, 57, 58 }.Concat(Enumerable.Repeat(46, 58)).ToArray();
        var velocities = Enumerable.Repeat(-1d, 64).ToArray();
        velocities[3] = 0; // intentionally silent
        var offsets = new double[64];
        offsets[4] = -.44;
        var pattern = new PitchPattern(true, 6, false, 1, 1, steps, Notes: true,
            VelocityOverrides: velocities, TimingOffsets: offsets);
        var profile = new PitchHumanization(HumanizationTiming.Ahead, 15, 10, 1234);
        var changes = profile.CreateChanges(pattern, 1, 4);
        Assert.Equal(changes.ToArray(), profile.CreateChanges(pattern, 1, 4).ToArray());
        Assert.Equal(-.45, changes[NoteSequencerGenerator.StepTimingOffsetKey(4)]);
        Assert.InRange(changes[NoteSequencerGenerator.StepTimingOffsetKey(3)], -.15, -.01);
        Assert.DoesNotContain(NoteSequencerGenerator.StepVelocityKey(3), changes.Keys);
        Assert.All(changes.Keys, key => Assert.True(new[] { 3, 4 }.Any(i =>
            key == NoteSequencerGenerator.StepTimingOffsetKey(i) || key == NoteSequencerGenerator.StepVelocityKey(i))));
        var behind = profile with { Timing = HumanizationTiming.Behind };
        Assert.InRange(behind.CreateChanges(pattern, 3, 1)[NoteSequencerGenerator.StepTimingOffsetKey(3)], .01, .15);
        var centered = profile with { Timing = HumanizationTiming.Centered, TimingPercent = 45 };
        var all = centered.CreateChanges(pattern, 0, 6);
        Assert.All(all.Where(pair => NoteSequencerGenerator.IsStepTimingOffsetParameter(pair.Key)), pair => Assert.InRange(pair.Value, -.45, .45));
        Assert.All(all.Where(pair => NoteSequencerGenerator.IsStepVelocityParameter(pair.Key)), pair => Assert.InRange(pair.Value, 0, 1));
        Assert.Empty((profile with { TimingPercent = 0, VelocityPercent = 0 }).CreateChanges(pattern, 0, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.CreateChanges(pattern, 5, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => (profile with { TimingPercent = 46 }).CreateChanges(pattern, 0, 6));
        Assert.Throws<ArgumentException>(() => profile.CreateChanges(pattern with { Notes = false }, 0, 6));
    }

    [Fact]
    public void HumanizationCommitsOneRevisionAndRoundTripsWithUndo()
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var id = session.GraphId;
        var node = session.Execute(new AddNodeCommand(id, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        session.Execute(new SetParameterValueCommand(id, new(node.Id, node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.ArticulationKey).Id), 1));
        var steps = new int[64]; steps[0] = 46; steps[2] = 53; steps[63] = 58;
        session.Execute(new SetPitchPatternStepsCommand(id, node.Id, steps));
        var baseline = PitchPattern.FromNode(session.Snapshot.GetNode(node.Id));
        var profile = new PitchHumanization(HumanizationTiming.Behind, 12, 10, 77);
        var revision = session.Snapshot.Revision;
        session.Execute(new HumanizePitchPatternCommand(id, node.Id, 0, 16, profile));
        Assert.Equal(revision + 1, session.Snapshot.Revision);
        var edited = PitchPattern.FromNode(session.Snapshot.GetNode(node.Id));
        Assert.Equal(steps, edited.Steps);
        Assert.Equal(baseline.GateFractions!.ToArray(), edited.GateFractions!.ToArray());
        Assert.InRange(edited.TimingOffsets![0], .01, .12);
        Assert.InRange(edited.TimingOffsets![2], .01, .12);
        Assert.Equal(0d, edited.TimingOffsets![63]);
        Assert.Equal(-1d, edited.VelocityOverrides![63]);
        var serializer = new PatchDocumentSerializer();
        using var roundtrip = serializer.Restore(serializer.Deserialize(serializer.Serialize(serializer.Capture(session, 120, id, []))), registry);
        Assert.True(edited.Matches(PitchPattern.FromNode(roundtrip.Snapshot.GetNode(node.Id))));
        revision = session.Snapshot.Revision;
        session.Execute(new HumanizePitchPatternCommand(id, node.Id, 0, 16, profile with { TimingPercent = 0, VelocityPercent = 0 }));
        Assert.Equal(revision, session.Snapshot.Revision);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Execute(new HumanizePitchPatternCommand(id, node.Id, 15, 2, profile)));
        Assert.Equal(revision, session.Snapshot.Revision);
        session.Undo();
        Assert.True(baseline.Matches(PitchPattern.FromNode(session.Snapshot.GetNode(node.Id))));
        session.Redo();
        Assert.True(edited.Matches(PitchPattern.FromNode(session.Snapshot.GetNode(node.Id))));
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
