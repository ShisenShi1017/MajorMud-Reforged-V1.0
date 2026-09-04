using System.Collections.Concurrent;

namespace RealmGate.Server;

public sealed class RuntimeEventDispatcher
{
    private readonly ConcurrentQueue<RuntimeEvent> _events = new();
    private long _nextSequence;

    public int EventCount => _events.Count;
    public IReadOnlyList<RuntimeEvent> Events => _events.ToArray();

    public RuntimeEvent Publish(string category, string name, string worldId, string? sessionId, string? entityId, string detail)
    {
        var runtimeEvent = new RuntimeEvent(
            Interlocked.Increment(ref _nextSequence),
            DateTimeOffset.UtcNow,
            category,
            name,
            worldId,
            sessionId ?? string.Empty,
            entityId ?? string.Empty,
            detail);

        _events.Enqueue(runtimeEvent);
        return runtimeEvent;
    }

    public string RenderStatus(int maxEvents = 10)
    {
        var events = _events.ToArray()
            .OrderByDescending(item => item.Sequence)
            .Take(Math.Max(1, maxEvents))
            .OrderBy(item => item.Sequence)
            .ToArray();

        if (events.Length == 0)
        {
            return "No runtime events have been published.";
        }

        return string.Join(Environment.NewLine, events.Select(item =>
            $"#{item.Sequence} {item.Category}.{item.Name} world={item.WorldId}; session={item.SessionId}; entity={item.EntityId}; {item.Detail}"));
    }
}

public sealed record RuntimeEvent(
    long Sequence,
    DateTimeOffset PublishedAtUtc,
    string Category,
    string Name,
    string WorldId,
    string SessionId,
    string EntityId,
    string Detail);
