using Orbit.Application.Graphs.Events;

namespace Orbit.Application.Graphs.Sessions;

/// <summary>Commit outcome. Subscriber failures are reported separately and never undo a commit.</summary>
public sealed record GraphCommandResult(
    GraphChangeSet? ChangeSet, IReadOnlyList<Exception> SubscriberErrors)
{
    public bool Changed => ChangeSet is not null;
}
