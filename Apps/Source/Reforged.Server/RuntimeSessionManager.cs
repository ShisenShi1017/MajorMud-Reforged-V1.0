using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed class RuntimeSessionManager
{
    private readonly ConcurrentDictionary<string, RuntimeSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeStateStore? _state;
    private readonly RuntimeEventDispatcher? _events;
    private readonly RuntimeDevelopmentOptions _development;

    public RuntimeSessionManager() => _development = new RuntimeDevelopmentOptions();

    public RuntimeSessionManager(RuntimeStateStore state, RuntimeEventDispatcher events, RuntimeDevelopmentOptions development)
    {
        _state = state;
        _events = events;
        _development = development;
    }

    public RuntimeSession CreateSession(string sessionId, RuntimeWorld world, string? playerName = null)
    {
        var normalizedName = string.IsNullOrWhiteSpace(playerName) ? sessionId : playerName.Trim();
        var characterId = RuntimeCharacterIds.Create(world.Definition.Id, normalizedName);
        RuntimeCharacterState? savedCharacter = null;
        if (_state is not null && _state.TryGetCharacter(characterId, out var existing))
        {
            savedCharacter = existing;
        }

        var spawn = RuntimeSessionSpawnResolver.Resolve(world, normalizedName, savedCharacter, _development);
        var session = new RuntimeSession(sessionId, world, spawn.Room, spawn.Actor, spawn.Source);
        _events?.Publish("session", "actor-attached", world.Definition.Id, sessionId, spawn.Actor.Id,
            $"actorKind={spawn.Actor.Kind}; persistentCharacter={spawn.Actor.IsPersistentCharacter}; source={spawn.Source}; room={spawn.Room.Id}");
        return session;
    }

    public IReadOnlyCollection<RuntimeSession> ActiveSessions => _sessions.Values.ToArray();
    public int ActiveSessionCount => _sessions.Count;

    public bool TryAdd(RuntimeSession session, int maxSessions, out string message)
    {
        if (_sessions.Count >= maxSessions) { message = $"Host session limit reached ({maxSessions})."; return false; }
        if (!_sessions.TryAdd(session.Id, session)) { message = $"Session '{session.Id}' is already active."; return false; }
        _state?.TrackSession(session);
        _events?.Publish("session", "started", session.World.Definition.Id, session.Id, session.Actor.Id, $"actor={session.Actor.DisplayName}; kind={session.Actor.Kind}");
        message = "accepted";
        return true;
    }

    public bool Remove(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var session)) return false;
        _state?.RemoveSession(sessionId);
        _events?.Publish("session", "ended", session.World.Definition.Id, session.Id, session.Actor.Id, $"actor={session.Actor.DisplayName}; kind={session.Actor.Kind}");
        return true;
    }

    public string RenderStatus() => _sessions.IsEmpty
        ? "No active sessions."
        : string.Join(Environment.NewLine, _sessions.Values.OrderBy(s => s.ConnectedAtUtc)
            .Select(s => $"{s.Id}: actor={s.Actor.DisplayName}; kind={s.Actor.Kind}; world={s.World.Definition.Id}; room={s.CurrentRoom.Id}; connectedUtc={s.ConnectedAtUtc:O}"));
}

public static class RuntimeCharacterIds
{
    public static string Create(string worldId, string playerName)
    {
        var safeName = new string(playerName.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return $"{worldId}:{(safeName.Length == 0 ? "player" : safeName)}";
    }
}
