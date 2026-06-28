namespace MajorMud.Reforged.Core.Events;

/// <summary>
/// Dependency-free event publisher for development, validation, importer reports, editor actions, and server lifecycle events.
/// </summary>
public sealed class InMemoryEventBus : IEventPublisher
{
    private readonly List<Action<IReforgedEvent>> _handlers = new();

    public void Publish(IReforgedEvent eventData)
    {
        foreach (var handler in _handlers.ToArray())
        {
            handler(eventData);
        }
    }

    public IDisposable Subscribe(Action<IReforgedEvent> handler)
    {
        _handlers.Add(handler);
        return new Subscription(() => _handlers.Remove(handler));
    }

    public IDisposable Subscribe<TEvent>(IEventSubscriber<TEvent> subscriber) where TEvent : IReforgedEvent
    {
        return Subscribe(e =>
        {
            if (e is TEvent typed)
            {
                subscriber.Handle(typed);
            }
        });
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public Subscription(Action dispose) => _dispose = dispose;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _dispose();
        }
    }
}
