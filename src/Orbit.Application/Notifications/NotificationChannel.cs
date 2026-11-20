namespace Orbit.Application.Notifications;

/// <summary>Reusable, instance-local synchronous notifications with disposable
/// registrations and isolated subscriber failures. Never a global event bus.</summary>
public sealed class NotificationChannel<TNotification>
{
    private readonly object gate = new();
    private readonly List<Subscription> subscriptions = [];

    public IDisposable Subscribe(Action<TNotification> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Add((notification, errors) => Notify(() => handler(notification), errors));
    }

    /// <summary>Deliver each matching item separately, even if one callback fails.</summary>
    public IDisposable SubscribeMany<TItem>(
        Func<TNotification, IEnumerable<TItem>> select, Action<TItem> handler)
    {
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(handler);
        return Add((notification, errors) =>
        {
            foreach (var item in select(notification))
                Notify(() => handler(item), errors);
        });
    }

    public IReadOnlyList<Exception> Publish(TNotification notification)
    {
        Subscription[] listeners;
        lock (gate)
            listeners = subscriptions.ToArray();

        // Registration changes during delivery apply to the next notification.
        var errors = new List<Exception>();
        foreach (var listener in listeners)
            Notify(() => listener.Deliver(notification, errors), errors);

        return errors.AsReadOnly();
    }

    public void Clear()
    {
        lock (gate)
            subscriptions.Clear();
    }

    private IDisposable Add(Action<TNotification, List<Exception>> deliver)
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
        NotificationChannel<TNotification> channel,
        Action<TNotification, List<Exception>> deliver) : IDisposable
    {
        private NotificationChannel<TNotification>? owner = channel;

        public void Deliver(TNotification notification, List<Exception> errors) => deliver(notification, errors);

        public void Dispose() => Interlocked.Exchange(ref owner, null)?.Remove(this);
    }
}
