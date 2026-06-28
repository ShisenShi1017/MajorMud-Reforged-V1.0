namespace Reforged.Core.Events;

public interface IEventPublisher
{
    void Publish(IReforgedEvent eventData);

    IDisposable Subscribe(Action<IReforgedEvent> handler);
}
