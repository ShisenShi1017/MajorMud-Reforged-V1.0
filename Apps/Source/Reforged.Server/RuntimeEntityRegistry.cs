using System.Collections.Concurrent;

namespace Reforged.Server;

public sealed class RuntimeEntityRegistry
{
    private readonly ConcurrentDictionary<string, RuntimeEntity> _entities = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _roomOccupancy = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _occupancyLock = new();

    public IReadOnlyCollection<RuntimeEntity> Entities => _entities.Values.ToArray();

    public RuntimeEntity RegisterCharacter(RuntimeSession session)
    {
        var entity = new RuntimeEntity(
            session.CharacterId,
            session.World.Definition.Id,
            session.CurrentRoom.Id,
            "character",
            session.PlayerName,
            RuntimeEntityLifecycle.Active,
            DateTimeOffset.UtcNow);

        _entities[entity.EntityId] = entity;
        AddOccupant(entity.WorldId, entity.RoomId, entity.EntityId);
        return entity;
    }

    public RuntimeEntity RegisterNpcPlaceholder(string worldId, string roomId, string npcId, string displayName)
    {
        var entity = new RuntimeEntity(npcId, worldId, roomId, "npc", displayName, RuntimeEntityLifecycle.Inactive, DateTimeOffset.UtcNow);
        _entities[entity.EntityId] = entity;
        AddOccupant(worldId, roomId, entity.EntityId);
        return entity;
    }

    public void MoveEntity(string entityId, string worldId, string oldRoomId, string newRoomId)
    {
        if (!_entities.TryGetValue(entityId, out var entity))
        {
            return;
        }

        RemoveOccupant(worldId, oldRoomId, entityId);
        var updated = entity with { RoomId = newRoomId, UpdatedAtUtc = DateTimeOffset.UtcNow };
        _entities[entityId] = updated;
        AddOccupant(worldId, newRoomId, entityId);
    }

    public void RemoveEntity(string entityId)
    {
        if (_entities.TryRemove(entityId, out var entity))
        {
            RemoveOccupant(entity.WorldId, entity.RoomId, entity.EntityId);
        }
    }

    public IReadOnlyList<RuntimeEntity> GetRoomOccupants(string worldId, string roomId)
    {
        lock (_occupancyLock)
        {
            var key = MakeRoomKey(worldId, roomId);
            if (!_roomOccupancy.TryGetValue(key, out var occupants))
            {
                return Array.Empty<RuntimeEntity>();
            }

            return occupants
                .Select(id => _entities.TryGetValue(id, out var entity) ? entity : null)
                .Where(entity => entity is not null)
                .Cast<RuntimeEntity>()
                .OrderBy(entity => entity.EntityType)
                .ThenBy(entity => entity.DisplayName)
                .ToArray();
        }
    }

    public string RenderStatus()
    {
        var activeCharacters = _entities.Values.Count(item => item.EntityType == "character" && item.Lifecycle == RuntimeEntityLifecycle.Active);
        var npcs = _entities.Values.Count(item => item.EntityType == "npc");
        return $"Entities={_entities.Count}; ActiveCharacters={activeCharacters}; Npcs={npcs}; OccupiedRooms={_roomOccupancy.Count}";
    }

    private void AddOccupant(string worldId, string roomId, string entityId)
    {
        lock (_occupancyLock)
        {
            var key = MakeRoomKey(worldId, roomId);
            if (!_roomOccupancy.TryGetValue(key, out var occupants))
            {
                occupants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _roomOccupancy[key] = occupants;
            }

            occupants.Add(entityId);
        }
    }

    private void RemoveOccupant(string worldId, string roomId, string entityId)
    {
        lock (_occupancyLock)
        {
            var key = MakeRoomKey(worldId, roomId);
            if (_roomOccupancy.TryGetValue(key, out var occupants))
            {
                occupants.Remove(entityId);
                if (occupants.Count == 0)
                {
                    _roomOccupancy.TryRemove(key, out _);
                }
            }
        }
    }

    private static string MakeRoomKey(string worldId, string roomId) => $"{worldId}::{roomId}";
}

public sealed record RuntimeEntity(
    string EntityId,
    string WorldId,
    string RoomId,
    string EntityType,
    string DisplayName,
    RuntimeEntityLifecycle Lifecycle,
    DateTimeOffset UpdatedAtUtc);

public enum RuntimeEntityLifecycle
{
    Inactive,
    Active,
    Dead,
    Despawned
}
