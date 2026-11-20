using Orbit.Application.Notifications;

namespace Orbit.Application.Graphs.Events;

/// <summary>Adapts the shared notification channel to committed graph change sets
/// and individual typed graph facts, scoped to one patch session.</summary>
internal sealed class GraphChangeDispatcher
{
    private readonly NotificationChannel<GraphChangeSet> channel = new();

    public IDisposable Subscribe(Action<GraphChangeSet> handler, Guid? graphId)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return channel.Subscribe(change =>
        {
            if (graphId is null || change.GraphId == graphId)
                handler(change);
        });
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, Guid? graphId)
        where TEvent : IGraphEvent =>
        channel.SubscribeMany(
            change => change.Events.OfType<TEvent>()
                .Where(graphEvent => graphId is null || graphEvent.GraphId == graphId),
            handler);

    public IReadOnlyList<Exception> Publish(GraphChangeSet change) => channel.Publish(change);

    public void Clear() => channel.Clear();
}
