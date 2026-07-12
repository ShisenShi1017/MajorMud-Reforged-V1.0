using System.Text.Json;

namespace Reforged.Server;

public sealed class RuntimeWorldLoader
{
    public RuntimeWorld Load(string projectRoot, WorldRuntimeDefinition definition)
    {
        var roomsPath = Path.Combine(projectRoot, definition.DataRoot, definition.RoomsFile);
        if (!File.Exists(roomsPath))
        {
            throw new FileNotFoundException($"World rooms file was not found for world '{definition.Id}'.", roomsPath);
        }

        var rooms = new Dictionary<string, RuntimeRoom>(StringComparer.OrdinalIgnoreCase);
        var roomsByKey = new Dictionary<RuntimeRoomKey, RuntimeRoom>();
        var roomsByNumberCandidates = new Dictionary<int, List<RuntimeRoom>>();
        using (var document = JsonDocument.Parse(File.ReadAllText(roomsPath)))
        {
            foreach (var roomElement in EnumerateGeneratedRecords(document.RootElement, roomsPath))
            {
                var room = RuntimeRoom.FromJson(roomElement);
                if (string.IsNullOrWhiteSpace(room.Id))
                {
                    continue;
                }

                rooms[room.Id] = room;
                if (room.MapId <= 0 || room.RoomNumber <= 0)
                {
                    throw new InvalidOperationException($"Generated room '{room.Id}' is not map-qualified.");
                }
                var roomKey = new RuntimeRoomKey(room.MapId, room.RoomNumber);
                if (!roomsByKey.TryAdd(roomKey, room))
                {
                    throw new InvalidOperationException($"Duplicate generated room identity '{roomKey}'.");
                }
                if (!roomsByNumberCandidates.TryGetValue(room.RoomNumber, out var candidates))
                {
                    candidates = new List<RuntimeRoom>();
                    roomsByNumberCandidates[room.RoomNumber] = candidates;
                }
                candidates.Add(room);
            }
        }

        if (rooms.Count == 0)
        {
            throw new InvalidOperationException($"World rooms file did not contain any loadable room records: {roomsPath}");
        }

        var roomsByNumber = roomsByNumberCandidates
            .Where(pair => pair.Value.Count == 1)
            .ToDictionary(pair => pair.Key, pair => pair.Value[0]);

        var exitsPath = Path.Combine(projectRoot, definition.DataRoot, definition.ExitsFile);
        if (File.Exists(exitsPath))
        {
            ApplyGeneratedExits(exitsPath, rooms, roomsByKey, roomsByNumber);
        }

        var triggersPath = Path.Combine(projectRoot, definition.DataRoot, definition.TriggersFile);
        var triggers = LoadTriggers(triggersPath, roomsByKey);

        var npcsPath = Path.Combine(projectRoot, definition.DataRoot, definition.NpcsFile);
        var spawnsPath = Path.Combine(projectRoot, definition.DataRoot, definition.SpawnsFile);
        var npcs = LoadNpcs(npcsPath);
        var spawns = LoadSpawns(spawnsPath, rooms, roomsByKey, roomsByNumber, npcs);

        var generatedCharacterStartRoomId = ReadGeneratedCharacterStartRoom(projectRoot, definition);
        if (!string.IsNullOrWhiteSpace(generatedCharacterStartRoomId) && !rooms.ContainsKey(generatedCharacterStartRoomId))
        {
            throw new InvalidOperationException(
                $"Generated character-start room '{generatedCharacterStartRoomId}' was not found for world '{definition.Id}'.");
        }

        if (!string.IsNullOrWhiteSpace(definition.DiagnosticRoom) && !rooms.ContainsKey(definition.DiagnosticRoom))
        {
            throw new InvalidOperationException(
                $"Configured diagnostic room '{definition.DiagnosticRoom}' was not found for world '{definition.Id}'.");
        }

        var catalog = GeneratedWorldDataCatalog.Validate(projectRoot, definition);
        if (!catalog.Passed)
        {
            throw new InvalidOperationException($"Generated world data validation failed for world '{definition.Id}': {string.Join("; ", catalog.Diagnostics)}");
        }

        return new RuntimeWorld(
            definition,
            rooms,
            roomsByKey,
            roomsByNumber,
            roomsPath,
            exitsPath,
            triggersPath,
            npcsPath,
            spawnsPath,
            generatedCharacterStartRoomId,
            npcs,
            spawns,
            triggers,
            catalog);
    }


    private static IReadOnlyList<RuntimeMovementTrigger> LoadTriggers(
        string triggersPath,
        IReadOnlyDictionary<RuntimeRoomKey, RuntimeRoom> roomsByKey)
    {
        if (!File.Exists(triggersPath))
        {
            throw new FileNotFoundException("Generated trigger file was not found.", triggersPath);
        }

        var triggers = new List<RuntimeMovementTrigger>();
        using var document = JsonDocument.Parse(File.ReadAllText(triggersPath));
        foreach (var element in EnumerateGeneratedRecords(document.RootElement, triggersPath))
        {
            if (!string.Equals(JsonValueReader.ReadString(element, "trigger_type", string.Empty),
                    "movement_or_exit_gate", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var trigger = RuntimeMovementTrigger.FromJson(element, roomsByKey);
            if (string.IsNullOrWhiteSpace(trigger.Id) || trigger.FromRoomNumber <= 0 ||
                trigger.ToRoomNumber <= 0 || string.IsNullOrWhiteSpace(trigger.Direction))
            {
                throw new InvalidOperationException($"Generated movement trigger is missing executable identity: {trigger.Id}");
            }

            triggers.Add(trigger);
        }

        // Promoted MMExplorer worlds express movement gates on exits.json and use
        // triggers.json for room/exit interactions. Legacy movement trigger records
        // remain supported when present, but an interaction-only trigger catalog is valid.
        return triggers;
    }

    private static IReadOnlyDictionary<int, RuntimeNpcDefinition> LoadNpcs(string npcsPath)
    {
        if (!File.Exists(npcsPath))
        {
            throw new FileNotFoundException("Generated NPC file was not found.", npcsPath);
        }

        var npcs = new Dictionary<int, RuntimeNpcDefinition>();
        using var document = JsonDocument.Parse(File.ReadAllText(npcsPath));
        foreach (var element in EnumerateGeneratedRecords(document.RootElement, npcsPath))
        {
            var npc = RuntimeNpcDefinition.FromJson(element);
            if (npc.LegacyId <= 0 || string.IsNullOrWhiteSpace(npc.Id))
            {
                continue;
            }

            npcs[npc.LegacyId] = npc;
        }

        if (npcs.Count == 0)
        {
            throw new InvalidOperationException($"Generated NPC file did not contain any loadable NPC definitions: {npcsPath}");
        }

        return npcs;
    }

    private static IReadOnlyList<RuntimeNpcSpawn> LoadSpawns(
        string spawnsPath,
        IReadOnlyDictionary<string, RuntimeRoom> rooms,
        IReadOnlyDictionary<RuntimeRoomKey, RuntimeRoom> roomsByKey,
        IReadOnlyDictionary<int, RuntimeRoom> roomsByNumber,
        IReadOnlyDictionary<int, RuntimeNpcDefinition> npcs)
    {
        if (!File.Exists(spawnsPath))
        {
            throw new FileNotFoundException("Generated NPC spawn file was not found.", spawnsPath);
        }

        var npcsByGroup = npcs.Values
            .Where(npc => npc.GroupId > 0)
            .GroupBy(npc => npc.GroupId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RuntimeNpcDefinition>)group.OrderBy(npc => npc.LegacyId).ToArray());

        var spawns = new List<RuntimeNpcSpawn>();
        using var document = JsonDocument.Parse(File.ReadAllText(spawnsPath));
        foreach (var element in EnumerateGeneratedRecords(document.RootElement, spawnsPath))
        {
            var spawn = RuntimeNpcSpawn.FromJson(element, npcs, npcsByGroup);
            if (string.IsNullOrWhiteSpace(spawn.Id) || string.IsNullOrWhiteSpace(spawn.RoomId))
            {
                continue;
            }

            if (spawn.SourceMapId <= 0 || spawn.SourceRoomNumber <= 0)
            {
                throw new InvalidOperationException($"NPC spawn '{spawn.Id}' is missing map-qualified primary room identity.");
            }

            // Spawn identity is map-qualified. Never collapse duplicate room numbers across maps.
            roomsByKey.TryGetValue(new RuntimeRoomKey(spawn.SourceMapId, spawn.SourceRoomNumber), out var primaryRoom);

            if (primaryRoom is null && !string.IsNullOrWhiteSpace(spawn.RoomId))
            {
                rooms.TryGetValue(spawn.RoomId, out primaryRoom);
            }

            // Legacy room-number fallback is allowed only when that number is unique in the active world.
            if (primaryRoom is null)
            {
                roomsByNumber.TryGetValue(spawn.SourceRoomNumber, out primaryRoom);
            }

            if (primaryRoom is not null)
            {
                spawn.ResolvedRuntimeRoomId = primaryRoom.Id;
            }

            // A linked spawn room is a same-map shared population-area key. It does not
            // need to own an independent spawn record. Preserve the qualified key even
            // when that map is not yet present in the current generated room slice.
            spawns.Add(spawn);
        }

        if (spawns.Count == 0)
        {
            throw new InvalidOperationException($"Generated NPC spawn file did not contain any loadable spawn records: {spawnsPath}");
        }

        return spawns;
    }

    private static string? ReadGeneratedCharacterStartRoom(string projectRoot, WorldRuntimeDefinition definition)
    {
        var candidates = new[]
        {
            Path.Combine(projectRoot, definition.DataRoot, "world_manifest.json"),
            Path.Combine(projectRoot, definition.DataRoot, "compile_manifest.json")
        };

        foreach (var path in candidates.Where(File.Exists))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            foreach (var key in new[] { "character_start_room", "new_character_start_room", "characterStartRoom", "newCharacterStartRoom" })
            {
                if (TryReadRoomId(root, key, out var roomId))
                {
                    return roomId;
                }
            }

            if (root.TryGetProperty("character_spawning", out var spawning) && spawning.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "start_room", "new_character_start_room", "startRoom", "newCharacterStartRoom" })
                {
                    if (TryReadRoomId(spawning, key, out var roomId))
                    {
                        return roomId;
                    }
                }
            }
        }

        // Deliberately do not treat the legacy generic start_room field as a character spawn.
        // In the current generated Classic slice it identifies Town Gates and is only suitable
        // for historical world/smoke diagnostics until Librarium-backed character-start evidence exists.
        return null;
    }

    private static bool TryReadRoomId(JsonElement element, string propertyName, out string roomId)
    {
        roomId = string.Empty;
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        roomId = value.GetString()?.Trim() ?? string.Empty;
        return roomId.Length > 0;
    }

    private static IEnumerable<JsonElement> EnumerateGeneratedRecords(JsonElement root, string sourcePath)
    {
        return root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("records", out var recordsElement) && recordsElement.ValueKind == JsonValueKind.Array => recordsElement.EnumerateArray(),
            _ => throw new InvalidOperationException($"Generated world file must contain a JSON array or generated records array: {sourcePath}")
        };
    }

    private static void ApplyGeneratedExits(
        string exitsPath,
        IDictionary<string, RuntimeRoom> rooms,
        IDictionary<RuntimeRoomKey, RuntimeRoom> roomsByKey,
        IDictionary<int, RuntimeRoom> roomsByNumber)
    {
        var exitsByRoom = new Dictionary<RuntimeRoomKey, List<RuntimeExit>>();
        using var document = JsonDocument.Parse(File.ReadAllText(exitsPath));
        foreach (var exitElement in EnumerateGeneratedRecords(document.RootElement, exitsPath))
        {
            var sourceKey = RuntimeExit.ReadSourceKey(exitElement);
            var exit = RuntimeExit.FromGeneratedRecord(exitElement);
            if (!sourceKey.IsValid || !exit.TargetKey.IsValid || string.IsNullOrWhiteSpace(exit.Direction))
            {
                continue;
            }

            if (!roomsByKey.ContainsKey(sourceKey))
            {
                throw new InvalidOperationException($"Generated exit source '{sourceKey}' does not resolve to a map-qualified room.");
            }

            if (!exitsByRoom.TryGetValue(sourceKey, out var roomExits))
            {
                roomExits = new List<RuntimeExit>();
                exitsByRoom[sourceKey] = roomExits;
            }

            var existingIndex = roomExits.FindIndex(candidate => string.Equals(candidate.Direction, exit.Direction, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0) roomExits[existingIndex] = exit; else roomExits.Add(exit);
        }

        foreach (var pair in exitsByRoom)
        {
            var existingRoom = roomsByKey[pair.Key];
            var mergedRoom = existingRoom.WithExits(pair.Value);
            rooms[mergedRoom.Id] = mergedRoom;
            roomsByKey[pair.Key] = mergedRoom;
            if (roomsByNumber.TryGetValue(pair.Key.RoomNumber, out var unique) && unique.Id.Equals(existingRoom.Id, StringComparison.OrdinalIgnoreCase))
                roomsByNumber[pair.Key.RoomNumber] = mergedRoom;
        }
    }

}

public sealed class RuntimeWorld
{
    private readonly IReadOnlyDictionary<string, RuntimeRoom> _rooms;
    private readonly IReadOnlyDictionary<RuntimeRoomKey, RuntimeRoom> _roomsByKey;
    private readonly IReadOnlyDictionary<int, RuntimeRoom> _roomsByNumber;

    public RuntimeWorld(
        WorldRuntimeDefinition definition,
        IReadOnlyDictionary<string, RuntimeRoom> rooms,
        IReadOnlyDictionary<RuntimeRoomKey, RuntimeRoom> roomsByKey,
        IReadOnlyDictionary<int, RuntimeRoom> roomsByNumber,
        string evidencePath,
        string exitsEvidencePath,
        string triggersEvidencePath,
        string npcEvidencePath,
        string spawnEvidencePath,
        string? generatedCharacterStartRoomId,
        IReadOnlyDictionary<int, RuntimeNpcDefinition> npcs,
        IReadOnlyList<RuntimeNpcSpawn> spawns,
        IReadOnlyList<RuntimeMovementTrigger> triggers,
        GeneratedWorldDataCatalog dataCatalog)
    {
        Definition = definition;
        _rooms = rooms;
        _roomsByKey = roomsByKey;
        _roomsByNumber = roomsByNumber;
        EvidencePath = evidencePath;
        ExitsEvidencePath = exitsEvidencePath;
        TriggersEvidencePath = triggersEvidencePath;
        NpcEvidencePath = npcEvidencePath;
        SpawnEvidencePath = spawnEvidencePath;
        GeneratedCharacterStartRoomId = generatedCharacterStartRoomId;
        Npcs = npcs;
        Spawns = spawns;
        Triggers = triggers;
        TriggersByRoomAndDirection = triggers
            .Where(trigger => trigger.SourceKey.IsValid)
            .GroupBy(trigger => $"{trigger.FromMapId}:{trigger.FromRoomNumber}:{trigger.Direction}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RuntimeMovementTrigger>)group.ToArray(), StringComparer.OrdinalIgnoreCase);
        SpawnsByRoom = spawns.Where(spawn => spawn.PrimaryRoomAvailable)
            .GroupBy(spawn => spawn.ResolvedRuntimeRoomId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RuntimeNpcSpawn>)group.ToArray(), StringComparer.OrdinalIgnoreCase);
        DataCatalog = dataCatalog;
    }

    public WorldRuntimeDefinition Definition { get; }
    public string EvidencePath { get; }
    public string ExitsEvidencePath { get; }
    public string TriggersEvidencePath { get; }
    public string NpcEvidencePath { get; }
    public string SpawnEvidencePath { get; }
    public string? GeneratedCharacterStartRoomId { get; }
    public IReadOnlyDictionary<int, RuntimeNpcDefinition> Npcs { get; }
    public IReadOnlyList<RuntimeNpcSpawn> Spawns { get; }
    public IReadOnlyList<RuntimeMovementTrigger> Triggers { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<RuntimeMovementTrigger>> TriggersByRoomAndDirection { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<RuntimeNpcSpawn>> SpawnsByRoom { get; }
    public int NpcCount => Npcs.Count;
    public int SpawnCount => Spawns.Count;
    public int TriggerCount => Triggers.Count;
    public int ExitCount => _rooms.Values.Sum(room => room.Exits.Count);
    public int ResolvedTriggerTargetCount => Triggers.Count(trigger => trigger.TargetRoomExists);
    public int ResolvedSpawnCount => Spawns.Count(spawn => spawn.FullyResolved);
    public int UnresolvedSpawnCount => SpawnCount - ResolvedSpawnCount;
    public int GroupSpawnCount => Spawns.Count(spawn => spawn.MonsterGroupId > 0);
    public int BossSpawnCount => Spawns.Count(spawn => spawn.BossLegacyId > 0);
    public int UnresolvedGroupCount => Spawns.Count(spawn => !spawn.GroupResolved);
    public int UnresolvedBossCount => Spawns.Count(spawn => !spawn.BossResolved);
    public GeneratedWorldDataCatalog DataCatalog { get; }
    public int RoomCount => _rooms.Count;

    public RuntimeRoom? ResolveGeneratedCharacterStartRoom() =>
        string.IsNullOrWhiteSpace(GeneratedCharacterStartRoomId) ? null : FindRoomById(GeneratedCharacterStartRoomId);

    public RuntimeRoom? ResolveDiagnosticRoom() =>
        string.IsNullOrWhiteSpace(Definition.DiagnosticRoom) ? null : FindRoomById(Definition.DiagnosticRoom);

    public RuntimeRoom? FindRoomById(string id) => _rooms.TryGetValue(id, out var room) ? room : null;

    public RuntimeRoom? FindRoom(int mapId, int roomNumber) => _roomsByKey.TryGetValue(new RuntimeRoomKey(mapId, roomNumber), out var room) ? room : null;

    // Compatibility lookup succeeds only when the room number is globally unique.
    public RuntimeRoom? FindRoomByNumber(int roomNumber) => _roomsByNumber.TryGetValue(roomNumber, out var room) ? room : null;

    public IReadOnlyList<RuntimeRoom> AllRooms() => _rooms.Values.ToArray();

    public IReadOnlyList<RuntimeNpcSpawn> FindSpawnsByRoom(string roomId) =>
        SpawnsByRoom.TryGetValue(roomId, out var spawns) ? spawns : Array.Empty<RuntimeNpcSpawn>();

    public IReadOnlyList<RuntimeMovementTrigger> FindMovementTriggers(int mapId, int roomNumber, string direction) =>
        TriggersByRoomAndDirection.TryGetValue($"{mapId}:{roomNumber}:{NormalizeDirection(direction)}", out var triggers)
            ? triggers
            : Array.Empty<RuntimeMovementTrigger>();

    private static string NormalizeDirection(string value) => value.Trim().ToUpperInvariant() switch
    {
        "NORTH" => "N", "SOUTH" => "S", "EAST" => "E", "WEST" => "W",
        "UP" => "U", "DOWN" => "D", var direction => direction
    };
}
