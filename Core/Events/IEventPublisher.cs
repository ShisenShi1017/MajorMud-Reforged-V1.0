namespace MajorMud.Reforged.Core.Events;

public interface IEventPublisher
{
    void Publish(IReforgedEvent eventData);
}
