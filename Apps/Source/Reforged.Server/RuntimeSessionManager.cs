using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed class RuntimeSessionManager
{
    private readonly ConcurrentDictionary<string, RuntimeSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeStateStore? _state;
    private readonly RuntimeEventDispatcher? _events;

    public RuntimeSessionManager()
    {
    }

    public RuntimeSessionManager(RuntimeStateStore state, RuntimeEventDispatcher events)
    {
        _state = state;
        _events = events;
    }

    public IReadOnlyCollection<RuntimeSession> ActiveSessions => _sessions.Values.ToArray();
    public int ActiveSessionCount => _sessions.Count;

    public bool TryAdd(RuntimeSession session, int maxSessions, out string message)
    {
        if (_sessions.Count >= maxSessions)
        {
            message = $"Host session limit reached ({maxSessions}).";
            return false;
        }

        if (!_sessions.TryAdd(session.Id, session))
        {
            message = $"Session '{session.Id}' is already active.";
            return false;
        }

        _state?.TrackSession(session);
        _events?.Publish("session", "started", session.World.Definition.Id, session.Id, session.CharacterId, $"player={session.PlayerName}");
        message = "accepted";
        return true;
    }

    public bool Remove(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
        {
            return false;
        }

        _state?.RemoveSession(sessionId);
        _events?.Publish("session", "ended", session.World.Definition.Id, session.Id, session.CharacterId, $"player={session.PlayerName}");
        return true;
    }

    public string RenderStatus()
    {
        if (_sessions.IsEmpty)
        {
            return "No active sessions.";
        }

        return string.Join(Environment.NewLine, _sessions.Values
            .OrderBy(session => session.ConnectedAtUtc)
            .Select(session => $"{session.Id}: player={session.PlayerName}; world={session.World.Definition.Id}; room={session.CurrentRoom.Id}; connectedUtc={session.ConnectedAtUtc:O}"));
    }
}
