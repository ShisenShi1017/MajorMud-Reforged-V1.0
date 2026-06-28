namespace Reforged.Core.Events;

public abstract class ReforgedEventBase : IReforgedEvent
{
    public abstract string EventName { get; }
    public DateTimeOffset OccurredUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Source { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
