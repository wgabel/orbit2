using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Models;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Timing;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class PitchRecordingTakeTests
{
    private static PitchPattern Pattern(bool free = false, int count = 16, int ticks = 1, double seconds = 20) =>
        new(true, count, free, ticks, seconds, [46, 0, 0, 0, 53, .. new int[58], 70]);
    private static TransportSnapshot At(double beats, double seconds, long sample, long generation = 0) =>
        new(120, beats, true, new(sample, 48000, true)) { ElapsedSeconds = seconds, ResetGeneration = generation };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(.49, 0)]
    [InlineData(.5, 4)]
    [InlineData(1.49, 4)]
    [InlineData(1.5, 8)]
    [InlineData(3.5, 0)]
    [InlineData(40003.5, 0)]
    public void QuantizationUsesNearestAllowedSlotWithForwardTiesAndWrap(double beats, int slot) =>
        Assert.Equal(slot, PitchRecordingTake.QuantizeSlot(Pattern(), 4, beats));

    [Fact]
    public void FreeTimingUsesSecondsAndOverdubKeepsUnrecordedSlots()
    {
        var take = new PitchRecordingTake(Pattern(true, 4), 69, 1, false, At(0, 0, 0));
        Assert.True(take.TryRecord(60, At(1000, 20, 960000)));
        Assert.True(take.TryRecord(62, At(1001, 21, 1008000)));
        Assert.False(take.TryRecord(63, At(1000, 20, 960000))); // delayed older event
        Assert.Equal(1, take.RecordedSlots);
        Assert.Equal(46, take.Step(0));
        Assert.Equal(63, take.Step(1));
        Assert.Equal(70, take.Step(63));
        Assert.Equal(0, take.Baseline.Steps[1]);
    }

    [Fact]
    public void ReplacePreservesHiddenSlotsAndRejectsStaleOrInvalidNotes()
    {
        var pattern = Pattern();
        var take = new PitchRecordingTake(pattern, 69, 4, true, At(0, 0, 100));
        Assert.All(take.CaptureSteps().Take(16), code => Assert.Equal(0, code));
        Assert.Equal(70, take.Step(63));
        Assert.False(take.TryRecord(60, At(1, .5, 99)));
        Assert.False(take.TryRecord(60, At(1, .5, 24000, 1)));
        Assert.False(take.TryRecord(0, At(1, .5, 24000)));
        Assert.False(take.TryRecord(60, At(1, .5, 24000) with { IsPlaying = false }));
        Assert.True(take.TryRecord(60, At(1, .5, 24000)));
        Assert.Equal(61, take.Step(4));
        Assert.False(take.MatchesSource(pattern with { Count = 8 }, 69));
        Assert.False(take.MatchesSource(pattern, 45));
        Assert.Throws<ArgumentException>(() => new PitchRecordingTake(pattern, 69, 3, false, At(0, 0, 0)));
        Assert.Throws<ArgumentException>(() => new PitchRecordingTake(pattern with { Loop = false }, 69, 1, false, At(0, 0, 0)));
    }

    [Fact]
    public void CommitIsOneUndoablePersistentPatternEditAndCancelLeavesGraphUntouched()
    {
        var registry = BuiltInNodes.CreateRegistry();
        using var session = new PatchSession(registry);
        var graph = session.GraphId;
        var node = session.Execute(new AddNodeCommand(graph, NoteSequencerGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        session.Execute(new SetParameterValueCommand(graph, new(node.Id, node.Parameters.Single(p => p.Definition.Key == NoteSequencerGenerator.LoopKey).Id), 1));
        var baseline = PitchPattern.FromNode(session.Snapshot.GetNode(node.Id));
        var revision = session.Snapshot.Revision;
        var cancelled = new PitchRecordingTake(baseline, 69, 1, false, At(0, 0, 0));
        cancelled.TryRecord(60, At(0, 0, 1));
        Assert.Equal(revision, session.Snapshot.Revision);
        var take = new PitchRecordingTake(baseline, 69, 1, false, At(0, 0, 0));
        take.TryRecord(62, At(0, 0, 1));
        take.TryRecord(64, At(baseline.Ticks / 4d, .5, 24000));
        Assert.Equal(revision, session.Snapshot.Revision);
        session.Execute(new SetPitchPatternStepsCommand(graph, node.Id, take.CaptureSteps()));
        Assert.Equal(revision + 1, session.Snapshot.Revision);
        var committed = PitchPattern.FromNode(session.Snapshot.GetNode(node.Id));
        Assert.Equal(take.CaptureSteps(), committed.Steps);
        session.Undo();
        Assert.True(baseline.Matches(PitchPattern.FromNode(session.Snapshot.GetNode(node.Id))));
        session.Redo();
        var serializer = new PatchDocumentSerializer();
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(serializer.Capture(session, 120, graph, []))), registry);
        Assert.True(committed.Matches(PitchPattern.FromNode(restored.Snapshot.GetNode(node.Id))));
    }
}
