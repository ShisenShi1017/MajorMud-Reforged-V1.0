using System.Collections.ObjectModel;

namespace RealmGate.Client.Automation;

public sealed class EventAutomationOptions
{
    public ObservableCollection<ClientScheduledEvent> ScheduledEvents { get; set; } = [];
    public bool PauseEventsInCombat { get; set; } = true;
    public bool PauseEventsWhileDisconnected { get; set; } = true;
}

public sealed class ClientScheduledEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Event";
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 10;
    public int IntervalSeconds { get; set; }
    public string Command { get; set; } = string.Empty;
    public bool RunImmediately { get; set; }
    public bool OnlyOutOfCombat { get; set; } = true;
    public bool OnlyInCombat { get; set; }
    public int Priority { get; set; } = 100;

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Name) ? "New event" : Name;
}

public sealed record ClientTimedEventContext(
    string SessionId,
    DateTimeOffset Timestamp,
    bool Connected,
    bool GameplayReady,
    bool? InCombat,
    bool Enabled,
    int MinimumCommandDelayMs = 0,
    DateTimeOffset? LastCommandAt = null);

// Selects requests only. The caller owns transport, policy, and the shared command queue.
public sealed class ClientTimedEventScheduler
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Dictionary<string, DateTimeOffset>> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public ClientInputAutomationMatch? Evaluate(EventAutomationOptions options, ClientTimedEventContext context)
    {
        lock (_sync)
        {
            if (!context.Connected || !context.GameplayReady)
            {
                _sessions.Remove(context.SessionId);
                return null;
            }
            if (!context.Enabled || string.IsNullOrWhiteSpace(context.SessionId)) return null;
            if (options.PauseEventsInCombat && context.InCombat != false) return null;
            if (context.LastCommandAt is { } lastCommand &&
                (context.Timestamp - lastCommand).TotalMilliseconds < Math.Max(0, context.MinimumCommandDelayMs)) return null;

            if (!_sessions.TryGetValue(context.SessionId, out var lastRuns))
                _sessions[context.SessionId] = lastRuns = new(StringComparer.OrdinalIgnoreCase);

            foreach (var item in options.ScheduledEvents
                         .Where(item => item.Enabled && !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Command))
                         .OrderBy(item => item.Priority)
                         .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
            {
                // Unknown combat state cannot satisfy either combat-specific condition.
                if (item.OnlyOutOfCombat && context.InCombat != false) continue;
                if (item.OnlyInCombat && context.InCombat != true) continue;
                var commands = ClientAutomationExpansionEngine.SplitCommandSteps(item.Command);
                if (commands.Count == 0) continue;
                var intervalSeconds = item.IntervalSeconds > 0
                    ? item.IntervalSeconds
                    : Math.Max(1L, item.IntervalMinutes) * 60;
                if (!lastRuns.TryGetValue(item.Id, out var lastRun))
                {
                    lastRuns[item.Id] = context.Timestamp;
                    if (!item.RunImmediately) continue;
                }
                else if ((context.Timestamp - lastRun).TotalSeconds < intervalSeconds) continue;

                // Missed intervals never create a catch-up burst.
                lastRuns[item.Id] = context.Timestamp;
                return new(item.Id, item.Name, commands, item.Priority);
            }
            return null;
        }
    }

    public void ResetSession(string sessionId)
    {
        lock (_sync) _sessions.Remove(sessionId);
    }

    public void Reset()
    {
        lock (_sync) _sessions.Clear();
    }
}
