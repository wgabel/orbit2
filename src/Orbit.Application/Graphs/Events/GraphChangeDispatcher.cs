namespace Orbit.Application.Graphs.Events;

/// <summary>
/// Session-local synchronous notification channel. Handlers are independently
/// disposable; a failed callback cannot prevent delivery to other listeners.
/// </summary>
internal sealed class GraphChangeDispatcher
{
    private readonly object gate = new();
    private readonly List<Subscription> subscriptions = [];

    public IDisposable Subscribe(Action<GraphChangeSet> handler, Guid? graphId)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Add((change, errors) =>
        {
            if (graphId is null || change.GraphId == graphId)
                Notify(() => handler(change), errors);
        });
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, Guid? graphId)
        where TEvent : IGraphEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Add((change, errors) =>
        {
            foreach (var graphEvent in change.Events.OfType<TEvent>())
                if (graphId is null || graphEvent.GraphId == graphId)
                    Notify(() => handler(graphEvent), errors);
        });
    }

    public IReadOnlyList<Exception> Publish(GraphChangeSet change)
    {
        Subscription[] listeners;
        lock (gate)
            listeners = subscriptions.ToArray();

        // Registrations are captured at the start of delivery. Changes to
        // subscriptions during a callback apply to the next change set.
        var errors = new List<Exception>();
        foreach (var listener in listeners)
            listener.Deliver(change, errors);

        return errors.AsReadOnly();
    }

    public void Clear()
    {
        lock (gate)
            subscriptions.Clear();
    }

    private IDisposable Add(Action<GraphChangeSet, List<Exception>> deliver)
    {
        var subscription = new Subscription(this, deliver);
        lock (gate)
            subscriptions.Add(subscription);
        return subscription;
    }

    private void Remove(Subscription subscription)
    {
        lock (gate)
            subscriptions.Remove(subscription);
    }

    private static void Notify(Action handler, List<Exception> errors)
    {
        try
        {
            handler();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }

    private sealed class Subscription(
        GraphChangeDispatcher dispatcher,
        Action<GraphChangeSet, List<Exception>> deliver) : IDisposable
    {
        private GraphChangeDispatcher? owner = dispatcher;

        public void Deliver(GraphChangeSet change, List<Exception> errors) => deliver(change, errors);

        public void Dispose() => Interlocked.Exchange(ref owner, null)?.Remove(this);
    }
}
