using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed class RuntimeSessionManager
{
    private readonly ConcurrentDictionary<string, RuntimeSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly RuntimeStateStore? _state;
    private readonly RuntimeEventDispatcher? _events;
    private readonly RuntimeDevelopmentOptions _development;
    private readonly RuntimeCharacterService? _characters;

    public event EventHandler? SessionsChanged;

    public RuntimeSessionManager() => _development = new RuntimeDevelopmentOptions();

    public RuntimeSessionManager(RuntimeStateStore state, RuntimeEventDispatcher events, RuntimeDevelopmentOptions development, RuntimeCharacterService? characters = null)
    {
        _state = state;
        _events = events;
        _development = development;
        _characters = characters;
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

    public RuntimeSession CreateCharacterSession(string sessionId, RuntimeWorld world, RuntimeCharacterRecord character)
    {
        if (!string.Equals(character.WorldId, world.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Character '{character.DisplayName}' belongs to world '{character.WorldId}', not '{world.Definition.Id}'.");
        }

        var room = world.FindRoomById(character.RoomId)
            ?? (character.RoomNumber > 0 ? world.FindRoomByNumber(character.RoomNumber) : null)
            ?? world.ResolveGeneratedCharacterStartRoom()
            ?? throw new InvalidOperationException($"Character '{character.DisplayName}' has no resolvable saved room and the world has no generated character start.");
        var actor = new RuntimeCharacterActor(character.CharacterId, character.DisplayName, character.RaceName, character.ClassName, character.Level, character.Sex);
        var source = string.Equals(room.Id, character.RoomId, StringComparison.OrdinalIgnoreCase)
            ? "saved-character-room"
            : "generated-character-start-recovery";
        var session = new RuntimeSession(sessionId, world, room, actor, source);
        _events?.Publish("session", "character-attached", world.Definition.Id, sessionId, actor.Id, $"account={character.AccountId}; source={source}; room={room.Id}");
        return session;
    }

    public IReadOnlyCollection<RuntimeSession> ActiveSessions => _sessions.Values.ToArray();
    public int ActiveSessionCount => _sessions.Count;

    public bool TryAdd(RuntimeSession session, int maxSessions, out string message)
    {
        if (maxSessions > 0 && _sessions.Count >= maxSessions) { message = $"Host session limit reached ({maxSessions})."; return false; }
        if (session.Actor.IsPersistentCharacter && _sessions.Values.Any(active => string.Equals(active.Actor.Id, session.Actor.Id, StringComparison.OrdinalIgnoreCase)))
        {
            message = $"Character '{session.Actor.DisplayName}' is already connected.";
            return false;
        }
        if (!_sessions.TryAdd(session.Id, session)) { message = $"Session '{session.Id}' is already active."; return false; }
        if (session.Actor.IsPersistentCharacter && _characters is not null)
        {
            foreach (var gangName in new RuntimeClassicGangStateService(_characters.ProjectRoot).ConsumeDisbandNotifications(session.Actor.Id))
                session.EnqueueMessage($"Your gang, {gangName}, has been disbanded!");
        }
        _state?.TrackSession(session);
        _events?.Publish("session", "started", session.World.Definition.Id, session.Id, session.Actor.Id, $"actor={session.Actor.DisplayName}; kind={session.Actor.Kind}");
        BroadcastRoom(session, $"{session.Actor.DisplayName} enters the world.");
        message = "accepted";
        SessionsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ReplaceCharacter(RuntimeSession session, RuntimeCharacterRecord replacement)
    {
        if (!_sessions.TryGetValue(session.Id, out var active) || !ReferenceEquals(active, session))
            throw new InvalidOperationException("Cannot replace a character on an inactive session.");
        if (!replacement.WorldId.Equals(session.World.Definition.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Replacement character belongs to a different realm.");

        var oldActor = session.Actor;
        var room = session.World.FindRoomById(replacement.RoomId)
            ?? (replacement.RoomNumber > 0 ? session.World.FindRoomByNumber(replacement.RoomNumber) : null)
            ?? session.World.ResolveGeneratedCharacterStartRoom()
            ?? throw new InvalidOperationException("Replacement character has no resolvable starting room.");
        var actor = new RuntimeCharacterActor(replacement.CharacterId, replacement.DisplayName, replacement.RaceName, replacement.ClassName, replacement.Level, replacement.Sex);

        foreach (var target in InRoom(session.World.Definition.Id, session.CurrentRoom.Id))
            if (!target.Id.Equals(session.Id, StringComparison.OrdinalIgnoreCase))
                target.EnqueueMessage($"{oldActor.DisplayName} leaves the world.");

        _state?.RemoveSession(session.Id);
        _state?.RemoveCharacter(oldActor.Id);
        session.ReplaceCharacter(session.World, room, actor, "reroll-replacement");
        _state?.TrackSession(session);
        _events?.Publish("session", "character-rerolled", session.World.Definition.Id, session.Id, actor.Id,
            $"previousActor={oldActor.Id}; replacement={actor.DisplayName}; room={room.Id}");
        BroadcastRoom(session, $"{actor.DisplayName} enters the world.");
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(string sessionId, bool saveCharacter = true)
    {
        if (!_sessions.TryRemove(sessionId, out var session)) return false;
        foreach (var target in InRoom(session.World.Definition.Id, session.CurrentRoom.Id))
        {
            target.EnqueueMessage($"{session.Actor.DisplayName} leaves the world.");
        }
        if (saveCharacter) _characters?.SaveSession(session);
        _state?.RemoveSession(sessionId);
        _events?.Publish("session", "ended", session.World.Definition.Id, session.Id, session.Actor.Id,
            $"actor={session.Actor.DisplayName}; kind={session.Actor.Kind}; saveCharacter={saveCharacter}");
        SessionsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public RuntimeSession? FindByActor(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var matches = _sessions.Values.Where(session =>
            session.Actor.Id.Equals(query, StringComparison.OrdinalIgnoreCase)
            || session.Actor.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase)
            || session.Actor.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }


    public RuntimeSession? FindUniqueInRoom(RuntimeSession source, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var matches = InRoom(source.World.Definition.Id, source.CurrentRoom.Id)
            .Where(session => !session.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase))
            .Where(session => session.Actor.Id.Equals(query, StringComparison.OrdinalIgnoreCase)
                || session.Actor.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase)
                || session.Actor.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public IReadOnlyList<RuntimeSession> InRoom(string worldId, string roomId) => _sessions.Values
        .Where(session => session.World.Definition.Id.Equals(worldId, StringComparison.OrdinalIgnoreCase)
            && session.CurrentRoom.Id.Equals(roomId, StringComparison.OrdinalIgnoreCase))
        .OrderBy(session => session.Actor.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void BroadcastRoom(
        RuntimeSession source,
        string message,
        bool includeSource = false,
        RuntimeClassicRoomMessagePriority priority = RuntimeClassicRoomMessagePriority.High,
        string? excludedSessionId = null)
    {
        foreach (var target in InRoom(source.World.Definition.Id, source.CurrentRoom.Id))
        {
            if (!includeSource && target.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(excludedSessionId) && target.Id.Equals(excludedSessionId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!ShouldDeliverClassicRoomMessage(target, priority)) continue;
            target.EnqueueMessage(message);
        }
    }

    private bool ShouldDeliverClassicRoomMessage(RuntimeSession target, RuntimeClassicRoomMessagePriority priority)
    {
        if (priority == RuntimeClassicRoomMessagePriority.High) return true;
        if (_characters is null || target.Actor is not RuntimeCharacterActor) return true;
        var character = _characters.LoadCharacter(target.Actor.Id);
        if (character is null) return true;
        var level = RuntimeLiveGameplayFacade.ReadClassicMessagesLevel(character);
        return RuntimeClassicCommunicationPersistenceLimitsAuthority.ShouldDeliverRoomMessage(level, priority);
    }

    public bool SendTell(RuntimeSession source, string targetQuery, string message, out string response)
    {
        var target = FindByActor(targetQuery);
        if (target is null) { response = $"No connected character uniquely matches '{targetQuery}'."; return false; }
        target.EnqueueMessage($"{source.Actor.DisplayName} tells you, '{message}'");
        response = $"You tell {target.Actor.DisplayName}, '{message}'";
        return true;
    }

    public string RenderStatus() => _sessions.IsEmpty
        ? "No active sessions."
        : string.Join(Environment.NewLine, _sessions.Values.OrderBy(s => s.ConnectedAtUtc)
            .Select(s => $"{s.Id}: actor={s.Actor.DisplayName}; kind={s.Actor.Kind}; world={s.World.Definition.Id}; room={s.CurrentRoom.Id}; connectedUtc={s.ConnectedAtUtc:O}"));

    public string RenderWho()
    {
        var users = _sessions.Values
            .Select(session => session.Actor)
            .OfType<RuntimeCharacterActor>()
            .OrderBy(actor => actor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var lines = new List<string> { "Who's in the Realm", "Name | Class | Level" };
        lines.AddRange(users.Select(actor =>
            $"{actor.DisplayName} | {actor.CharacterClass} | {actor.Level}"));
        lines.Add($"{users.Length} {(users.Length == 1 ? "user" : "users")} in the Realm.");
        return string.Join(Environment.NewLine, lines);
    }
}

public static class RuntimeCharacterIds
{
    public static string Create(string worldId, string playerName)
    {
        var safeName = new string(playerName.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return $"{worldId}:{(safeName.Length == 0 ? "player" : safeName)}";
    }
}
