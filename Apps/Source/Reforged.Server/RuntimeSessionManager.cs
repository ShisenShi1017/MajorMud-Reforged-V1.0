using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed class RuntimeSessionManager
{
    private readonly ConcurrentDictionary<string, RuntimeSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

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

        message = "accepted";
        return true;
    }

    public bool Remove(string sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
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
