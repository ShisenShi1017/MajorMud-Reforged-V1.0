namespace Reforged.Core.Events;

public interface IEventSubscriber<in TEvent> where TEvent : IReforgedEvent
{
    void Handle(TEvent eventData);
}
