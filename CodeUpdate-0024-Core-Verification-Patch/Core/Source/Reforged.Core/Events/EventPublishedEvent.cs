namespace Reforged.Core.Events;

/// <summary>
/// Simple event payload used by startup diagnostics and smoke tests.
/// </summary>
public sealed record EventPublishedEvent : ReforgedEventBase
{
    public EventPublishedEvent(string name, string detail, DateTimeOffset timestampUtc)
        : base(timestampUtc)
    {
        Name = name;
        Detail = detail;
    }

    public string Name { get; }

    public string Detail { get; }
}
