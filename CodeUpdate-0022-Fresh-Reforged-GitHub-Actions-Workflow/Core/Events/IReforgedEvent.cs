namespace Reforged.Core.Events;

/// <summary>
/// Marker contract for events raised by server, editor, importer, launcher, and validation systems.
/// Events are plain data objects so they can be logged, replayed, tested, or bridged to future clients.
/// </summary>
public interface IReforgedEvent
{
    string EventName { get; }
    DateTimeOffset OccurredUtc { get; }
    string Source { get; }
    IReadOnlyDictionary<string, string> Metadata { get; }
}
