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
        var roomsByNumber = new Dictionary<int, RuntimeRoom>();
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
                if (room.RoomNumber > 0)
                {
                    roomsByNumber[room.RoomNumber] = room;
                }
            }
        }

        if (rooms.Count == 0)
        {
            throw new InvalidOperationException($"World rooms file did not contain any loadable room records: {roomsPath}");
        }

        var exitsPath = Path.Combine(projectRoot, definition.DataRoot, definition.ExitsFile);
        if (File.Exists(exitsPath))
        {
            ApplyGeneratedExits(exitsPath, rooms, roomsByNumber);
        }

        var npcsPath = Path.Combine(projectRoot, definition.DataRoot, definition.NpcsFile);
        var spawnsPath = Path.Combine(projectRoot, definition.DataRoot, definition.SpawnsFile);
        var npcs = LoadNpcs(npcsPath);
        var spawns = LoadSpawns(spawnsPath, rooms, npcs);

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
            roomsByNumber,
            roomsPath,
            exitsPath,
            npcsPath,
            spawnsPath,
            generatedCharacterStartRoomId,
            npcs,
            spawns,
            catalog);
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

            var primaryRoom = rooms.Values.FirstOrDefault(room =>
                room.MapId == spawn.SourceMapId && room.RoomNumber == spawn.SourceRoomNumber);
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
        IDictionary<int, RuntimeRoom> roomsByNumber)
    {
        var exitsByRoomNumber = new Dictionary<int, List<RuntimeExit>>();
        using var document = JsonDocument.Parse(File.ReadAllText(exitsPath));
        foreach (var exitElement in EnumerateGeneratedRecords(document.RootElement, exitsPath))
        {
            var fromRoomNumber = RuntimeExit.ReadFromRoomNumber(exitElement);
            var exit = RuntimeExit.FromGeneratedRecord(exitElement);
            if (fromRoomNumber <= 0 || string.IsNullOrWhiteSpace(exit.Direction) || exit.TargetRoomNumber <= 0)
            {
                continue;
            }

            if (!exitsByRoomNumber.TryGetValue(fromRoomNumber, out var roomExits))
            {
                roomExits = new List<RuntimeExit>();
                exitsByRoomNumber[fromRoomNumber] = roomExits;
            }

            var existingIndex = roomExits.FindIndex(candidate => string.Equals(candidate.Direction, exit.Direction, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                roomExits[existingIndex] = exit;
            }
            else
            {
                roomExits.Add(exit);
            }
        }

        foreach (var pair in exitsByRoomNumber)
        {
            if (!roomsByNumber.TryGetValue(pair.Key, out var existingRoom))
            {
                continue;
            }

            var mergedRoom = existingRoom.WithExits(pair.Value);
            rooms[mergedRoom.Id] = mergedRoom;
            roomsByNumber[pair.Key] = mergedRoom;
        }
    }
}

public sealed class RuntimeWorld
{
    private readonly IReadOnlyDictionary<string, RuntimeRoom> _rooms;
    private readonly IReadOnlyDictionary<int, RuntimeRoom> _roomsByNumber;

    public RuntimeWorld(
        WorldRuntimeDefinition definition,
        IReadOnlyDictionary<string, RuntimeRoom> rooms,
        IReadOnlyDictionary<int, RuntimeRoom> roomsByNumber,
        string evidencePath,
        string exitsEvidencePath,
        string npcEvidencePath,
        string spawnEvidencePath,
        string? generatedCharacterStartRoomId,
        IReadOnlyDictionary<int, RuntimeNpcDefinition> npcs,
        IReadOnlyList<RuntimeNpcSpawn> spawns,
        GeneratedWorldDataCatalog dataCatalog)
    {
        Definition = definition;
        _rooms = rooms;
        _roomsByNumber = roomsByNumber;
        EvidencePath = evidencePath;
        ExitsEvidencePath = exitsEvidencePath;
        NpcEvidencePath = npcEvidencePath;
        SpawnEvidencePath = spawnEvidencePath;
        GeneratedCharacterStartRoomId = generatedCharacterStartRoomId;
        Npcs = npcs;
        Spawns = spawns;
        SpawnsByRoom = spawns.Where(spawn => spawn.PrimaryRoomAvailable)
            .GroupBy(spawn => spawn.ResolvedRuntimeRoomId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RuntimeNpcSpawn>)group.ToArray(), StringComparer.OrdinalIgnoreCase);
        DataCatalog = dataCatalog;
    }

    public WorldRuntimeDefinition Definition { get; }
    public string EvidencePath { get; }
    public string ExitsEvidencePath { get; }
    public string NpcEvidencePath { get; }
    public string SpawnEvidencePath { get; }
    public string? GeneratedCharacterStartRoomId { get; }
    public IReadOnlyDictionary<int, RuntimeNpcDefinition> Npcs { get; }
    public IReadOnlyList<RuntimeNpcSpawn> Spawns { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<RuntimeNpcSpawn>> SpawnsByRoom { get; }
    public int NpcCount => Npcs.Count;
    public int SpawnCount => Spawns.Count;
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

    public RuntimeRoom? FindRoomByNumber(int roomNumber) => _roomsByNumber.TryGetValue(roomNumber, out var room) ? room : null;

    public IReadOnlyList<RuntimeNpcSpawn> FindSpawnsByRoom(string roomId) =>
        SpawnsByRoom.TryGetValue(roomId, out var spawns) ? spawns : Array.Empty<RuntimeNpcSpawn>();
}
