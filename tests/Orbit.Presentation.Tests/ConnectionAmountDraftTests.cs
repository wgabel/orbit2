using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Models;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class ConnectionAmountDraftTests
{
    [Fact]
    public void PreviewDoesNotCommitAndReleaseProducesOneUndoableEdit()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var before = session.Snapshot;
        var edge = before.Connections[0];
        var changes = 0;
        using var listener = session.Subscribe(_ => changes++);
        var draft = ConnectionAmountDraft.Begin(1, before, edge.Id)!;
        Assert.True(draft.Preview(-0.5));
        Assert.Equal(-100d, draft.PreviewAmount);
        Assert.True(draft.Preview(0));
        Assert.True(draft.Preview(0.25));
        Assert.Same(before, session.Snapshot);
        Assert.Equal(0, changes);
        Assert.False(session.CanUndo);
        var request = draft.Complete()!;
        Assert.Null(draft.Complete());
        Assert.False(draft.Preview(1));
        var command = request.CreateCommand(1, session.Snapshot)!;
        session.Execute(command);
        Assert.Equal(1, changes);
        Assert.Equal(before.Revision + 1, session.Snapshot.Revision);
        Assert.Equal(edge with { Amount = 50 }, session.Snapshot.Connections[0]);
        Assert.Null(request.CreateCommand(1, session.Snapshot));
        session.Undo();
        Assert.Equal(edge, session.Snapshot.Connections[0]);
        Assert.False(session.CanUndo);
        session.Redo();
        Assert.Equal(50d, session.Snapshot.Connections[0].Amount);
    }

    [Fact]
    public void CancelAndReturnToSnappedThumbPreserveExactAmountAndRedo()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var id = session.Snapshot.Connections[0].Id;
        session.Execute(new SetConnectionAmountCommand(session.GraphId, id, 100.12345));
        session.Execute(new SetConnectionAmountCommand(session.GraphId, id, 101));
        session.Undo();
        var before = session.Snapshot;
        var unchanged = ConnectionAmountDraft.Begin(1, before, id)!;
        Assert.True(unchanged.Preview(0.5));
        Assert.Equal(100.12345, unchanged.PreviewAmount);
        Assert.True(unchanged.Preview(-1));
        Assert.True(unchanged.Preview(0.5));
        Assert.Null(unchanged.Complete());
        var cancelled = ConnectionAmountDraft.Begin(1, before, id)!;
        Assert.True(cancelled.Preview(-1));
        cancelled.Cancel();
        Assert.Equal(100.12345, cancelled.PreviewAmount);
        Assert.False(cancelled.Preview(1));
        Assert.Null(cancelled.Complete());
        Assert.Same(before, session.Snapshot);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void ExternalChangesAndWorkspaceReplacementRejectStaleCompletion()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var before = session.Snapshot;
        var id = before.Connections[0].Id;
        var draft = ConnectionAmountDraft.Begin(3, before, id)!;
        Assert.False(draft.Preview(double.NaN));
        Assert.False(draft.Preview(2));
        Assert.True(draft.Preview(0));
        var request = draft.Complete()!;
        Assert.Null(request.CreateCommand(4, before));
        var active = ConnectionAmountDraft.Begin(3, before, id)!;
        active.Preview(-1);
        session.Execute(new SetConnectionAmountCommand(session.GraphId, id, 50));
        Assert.False(active.Observe(3, session.Snapshot));
        Assert.Null(active.Complete());
        Assert.Null(request.CreateCommand(3, session.Snapshot));
        session.Execute(new RemoveConnectionCommand(session.GraphId, id));
        Assert.Null(request.CreateCommand(3, session.Snapshot));
        Assert.Null(ConnectionAmountDraft.Begin(3, session.Snapshot, id));
    }

    [Fact]
    public void ExactEntryPreservesPrecisionAndUsesGuardedHistory()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var before = session.Snapshot;
        var edge = before.Connections[0];
        var draft = ConnectionAmountDraft.Begin(1, before, edge.Id)!;
        Assert.False(draft.PreviewExact(double.NaN));
        Assert.False(draft.PreviewExact(double.PositiveInfinity));
        Assert.True(ConnectionAmountSemantics.TryParseAmount("-12345.6789012345", out var amount));
        Assert.True(draft.PreviewExact(amount));
        Assert.Equal(amount, draft.PreviewAmount);
        Assert.Same(before, session.Snapshot);
        Assert.False(session.CanUndo);
        var request = draft.Complete()!;
        Assert.Null(draft.Complete());
        Assert.False(draft.PreviewExact(0));
        session.Execute(request.CreateCommand(1, before)!);
        Assert.Equal(amount, session.Snapshot.Connections[0].Amount);
        session.Undo();
        Assert.Equal(edge, session.Snapshot.Connections[0]);
        Assert.False(session.CanUndo);
        Assert.True(session.CanRedo);
        session.Redo();
        Assert.Equal(amount, session.Snapshot.Connections[0].Amount);
        var canceled = ConnectionAmountDraft.Begin(1, session.Snapshot, edge.Id)!;
        Assert.True(canceled.PreviewExact(0));
        canceled.Cancel();
        Assert.Null(canceled.Complete());
        Assert.Equal(amount, session.Snapshot.Connections[0].Amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    [InlineData("1,2")]
    public void InvalidExactEntryIsRejected(string text)
        => Assert.False(ConnectionAmountSemantics.TryParseAmount(text, out _));

    [Theory]
    [InlineData(-12345.6789012345)]
    [InlineData(0.000000000000001)]
    [InlineData(double.MaxValue)]
    public void ExactTextRoundTrips(double amount)
    {
        Assert.True(ConnectionAmountSemantics.TryParseAmount(
            ConnectionAmountSemantics.FormatExactAmount(amount), out var parsed));
        Assert.Equal(amount, parsed);
    }

    [Fact]
    public void NestedDraftCommitsOnlyAgainstItsOwningCanvas()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(), BuiltInNodes.CreateEditorRegistry());
        var drone = session.Execute(new AddNodeCommand(session.GraphId, CompositeNode.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var child = drone.Composite!.Graph;
        var edge = child.Connections.First(candidate => child.GetNode(candidate.Destination.NodeId)
            .Inputs.Any(port => port.Id == candidate.Destination.PortId && port.SignalType == SignalType.Audio));
        var draft = ConnectionAmountDraft.Begin(1, child, edge.Id)!;
        draft.Preview(0);
        // Ensure this test chooses a changed amount even if the initial edge is zero.
        if (draft.PreviewAmount == edge.Amount) draft.Preview(1);
        var request = draft.Complete()!;
        Assert.Null(request.CreateCommand(1, session.Snapshot));
        var command = request.CreateCommand(1, session.Snapshot.GetGraph(child.Id))!;
        session.Execute(command);
        Assert.Equal(edge with { Amount = request.Amount }, session.Snapshot.GetGraph(child.Id)
            .Connections.Single(candidate => candidate.Id == edge.Id));
    }
}
