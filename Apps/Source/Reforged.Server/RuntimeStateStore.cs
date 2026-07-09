using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed class RuntimeStateStore
{
    private readonly ConcurrentDictionary<string, RuntimeWorldState> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuntimeSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuntimeCharacterState> _characters = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<RuntimeWorldState> Worlds => _worlds.Values.ToArray();
    public IReadOnlyCollection<RuntimeSessionState> Sessions => _sessions.Values.ToArray();
    public IReadOnlyCollection<RuntimeCharacterState> Characters => _characters.Values.ToArray();

    public void EnsureWorld(RuntimeWorld world)
    {
        _worlds[world.Definition.Id] = new RuntimeWorldState(
            world.Definition.Id,
            world.Definition.Name,
            world.Definition.Type,
            world.Definition.RulesProfile,
            world.Definition.AiMode,
            world.Definition.ClassicProtected,
            world.Definition.TransfersAllowed,
            world.RoomCount,
            DateTimeOffset.UtcNow);
    }

    public RuntimeSessionState TrackSession(RuntimeSession session)
    {
        var now = DateTimeOffset.UtcNow;
        var character = new RuntimeCharacterState(
            session.CharacterId,
            session.Account.AccountId,
            session.PlayerName,
            session.World.Definition.Id,
            session.Character.SaveSlot,
            session.CurrentRoom.Id,
            session.CurrentRoom.RoomNumber,
            now,
            now);

        var sessionState = new RuntimeSessionState(
            session.Id,
            session.Account.AccountId,
            session.PlayerName,
            session.CharacterId,
            session.World.Definition.Id,
            session.Character.SaveSlot,
            session.CurrentRoom.Id,
            session.CurrentRoom.RoomNumber,
            session.ConnectedAtUtc,
            session.LastCommandAtUtc,
            true);

        _characters[character.CharacterId] = character;
        _sessions[session.Id] = sessionState;
        return sessionState;
    }

    public void UpdateSessionLocation(RuntimeSession session)
    {
        var now = DateTimeOffset.UtcNow;
        _sessions[session.Id] = new RuntimeSessionState(
            session.Id,
            session.Account.AccountId,
            session.PlayerName,
            session.CharacterId,
            session.World.Definition.Id,
            session.Character.SaveSlot,
            session.CurrentRoom.Id,
            session.CurrentRoom.RoomNumber,
            session.ConnectedAtUtc,
            session.LastCommandAtUtc,
            true);

        _characters[session.CharacterId] = new RuntimeCharacterState(
            session.CharacterId,
            session.Account.AccountId,
            session.PlayerName,
            session.World.Definition.Id,
            session.Character.SaveSlot,
            session.CurrentRoom.Id,
            session.CurrentRoom.RoomNumber,
            _characters.TryGetValue(session.CharacterId, out var existing) ? existing.CreatedAtUtc : now,
            now);
    }

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var removed))
        {
            _sessions[sessionId] = removed with { Connected = false, LastCommandAtUtc = DateTimeOffset.UtcNow };
        }
    }

    public string RenderStatus()
    {
        return $"Worlds={_worlds.Count}; Sessions={_sessions.Values.Count(session => session.Connected)}; Characters={_characters.Count}";
    }
}

public sealed record RuntimeWorldState(
    string WorldId,
    string Name,
    string Type,
    string RulesProfile,
    string AiMode,
    bool ClassicProtected,
    bool TransfersAllowed,
    int RoomCount,
    DateTimeOffset LoadedAtUtc);

public sealed record RuntimeSessionState(
    string SessionId,
    string AccountId,
    string PlayerName,
    string CharacterId,
    string WorldId,
    int SaveSlot,
    string RoomId,
    int RoomNumber,
    DateTimeOffset ConnectedAtUtc,
    DateTimeOffset LastCommandAtUtc,
    bool Connected);

public sealed record RuntimeCharacterState(
    string CharacterId,
    string AccountId,
    string DisplayName,
    string WorldId,
    int SaveSlot,
    string RoomId,
    int RoomNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
