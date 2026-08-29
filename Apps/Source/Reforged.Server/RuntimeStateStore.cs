using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed class RuntimeStateStore
{
    private readonly ConcurrentDictionary<string, RuntimeWorldState> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuntimeTrackedSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuntimeCharacterState> _characters = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<RuntimeWorldState> Worlds => _worlds.Values.ToArray();
    public IReadOnlyCollection<RuntimeTrackedSessionState> Sessions => _sessions.Values.ToArray();
    public IReadOnlyCollection<RuntimeCharacterState> Characters => _characters.Values.ToArray();
    public bool TryGetCharacter(string characterId, out RuntimeCharacterState character) => _characters.TryGetValue(characterId, out character!);

    public void EnsureWorld(RuntimeWorld world) => _worlds[world.Definition.Id] = new RuntimeWorldState(world.Definition.Id, world.Definition.Name, world.Definition.Type, world.Definition.RulesProfile, world.Definition.AiMode, world.Definition.ClassicProtected, world.Definition.TransfersAllowed, world.RoomCount, DateTimeOffset.UtcNow);

    public RuntimeTrackedSessionState TrackSession(RuntimeSession session)
    {
        var state = CreateSessionState(session, true);
        _sessions[session.Id] = state;
        UpdateCharacterLocationIfApplicable(session);
        return state;
    }

    public void UpdateSessionLocation(RuntimeSession session)
    {
        _sessions[session.Id] = CreateSessionState(session, true);
        UpdateCharacterLocationIfApplicable(session);
    }

    private void UpdateCharacterLocationIfApplicable(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor character) return;
        var now = DateTimeOffset.UtcNow;
        _characters[character.Id] = new RuntimeCharacterState(character.Id, character.DisplayName, session.World.Definition.Id, session.CurrentRoom.Id, session.CurrentRoom.RoomNumber, character.Race, character.CharacterClass, character.Level, _characters.TryGetValue(character.Id, out var existing) ? existing.CreatedAtUtc : now, now);
    }

    private static RuntimeTrackedSessionState CreateSessionState(RuntimeSession session, bool connected) => new(session.Id, session.Actor.Id, session.Actor.DisplayName, session.Actor.Kind, session.World.Definition.Id, session.CurrentRoom.Id, session.CurrentRoom.RoomNumber, session.ConnectedAtUtc, session.LastCommandAtUtc, connected);

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var removed)) _sessions[sessionId] = removed with { Connected = false, LastCommandAtUtc = DateTimeOffset.UtcNow };
    }

    public void RemoveCharacter(string characterId) => _characters.TryRemove(characterId, out _);

    public string RenderStatus() => $"Worlds={_worlds.Count}; Sessions={_sessions.Values.Count(s => s.Connected)}; Characters={_characters.Count}";
}

public sealed record RuntimeWorldState(string WorldId, string Name, string Type, string RulesProfile, string AiMode, bool ClassicProtected, bool TransfersAllowed, int RoomCount, DateTimeOffset LoadedAtUtc);
public sealed record RuntimeTrackedSessionState(string SessionId, string ActorId, string ActorName, string ActorKind, string WorldId, string RoomId, int RoomNumber, DateTimeOffset ConnectedAtUtc, DateTimeOffset LastCommandAtUtc, bool Connected);
public sealed record RuntimeCharacterState(string CharacterId, string DisplayName, string WorldId, string RoomId, int RoomNumber, string Race, string CharacterClass, int Level, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
