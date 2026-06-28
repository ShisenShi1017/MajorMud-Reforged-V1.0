namespace Reforged.Core.Events;

/// <summary>
/// Thread-safe, dependency-free event bus for Milestone 1 verification, diagnostics, and future app shells.
/// </summary>
public sealed class InMemoryEventBus : IEventPublisher
{
    private readonly object gate = new();
    private readonly List<Action<IReforgedEvent>> handlers = new();

    public int SubscriberCount
    {
        get
        {
            lock (gate)
            {
                return handlers.Count;
            }
        }
    }

    public void Publish(IReforgedEvent eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Action<IReforgedEvent>[] snapshot;
        lock (gate)
        {
            snapshot = handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            handler(eventData);
        }
    }

    public IDisposable Subscribe(Action<IReforgedEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (gate)
        {
            handlers.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (gate)
            {
                handlers.Remove(handler);
            }
        });
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IReforgedEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(eventData =>
        {
            if (eventData is TEvent typed)
            {
                handler(typed);
            }
        });
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action dispose;
        private bool disposed;

        public Subscription(Action dispose) => this.dispose = dispose;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            dispose();
        }
    }
}
