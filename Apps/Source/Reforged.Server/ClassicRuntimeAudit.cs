using System.Text;
using System.Text.Json;

namespace Reforged.Server;

public static class ClassicRuntimeAudit
{
    private static readonly string[] RequiredRoomQueries =
    {
        "Village Entrance",
        "Weapons Shop",
        "Arena",
        "Adventurer's Guild",
        "room.map1.2150"
    };

    public static string RenderCompilerOutputAudit(string projectRoot, WorldRuntimeDefinition definition)
    {
        var dataRoot = Path.Combine(projectRoot, definition.DataRoot);
        var rooms = ReadRecords(Path.Combine(dataRoot, definition.RoomsFile));
        var npcs = ReadRecords(Path.Combine(dataRoot, definition.NpcsFile));
        var spawns = ReadRecords(Path.Combine(dataRoot, definition.SpawnsFile));
        var exits = ReadRecords(Path.Combine(dataRoot, definition.ExitsFile));
        var items = ReadRecords(Path.Combine(dataRoot, "items.json"));
        var interactions = ReadRecords(Path.Combine(dataRoot, "room_interactions.json"));
        var quests = ReadRecords(Path.Combine(dataRoot, "quests.json"));
        var triggers = ReadRecords(Path.Combine(dataRoot, definition.TriggersFile));
        var shops = ReadRecords(Path.Combine(dataRoot, "shops.json"));
        var banks = ReadRecords(Path.Combine(dataRoot, "banks.json"));

        var text = new StringBuilder();
        text.AppendLine("========== CLASSIC COMPILER OUTPUT AUDIT ==========");
        text.AppendLine($"DataRoot: {dataRoot}");
        text.AppendLine($"Rooms: {rooms.Count}");
        text.AppendLine($"Rooms with descriptions: {rooms.Count(HasDescription)}");
        text.AppendLine($"Rooms without descriptions: {rooms.Count - rooms.Count(HasDescription)}");
        text.AppendLine($"NPC definitions: {npcs.Count}");
        text.AppendLine($"Monster definitions: {npcs.Count(IsMonsterDefinition)}");
        text.AppendLine($"Spawn groups: {spawns.Select(ReadSpawnGroup).Where(value => value > 0).Distinct().Count()}");
        text.AppendLine($"Spawn records: {spawns.Count}");
        text.AppendLine($"Items: {items.Count}");
        text.AppendLine($"Objects: {interactions.Count}");
        text.AppendLine($"Doors: {exits.Count(IsDoor)}");
        text.AppendLine($"Quests: {quests.Count}");
        text.AppendLine($"Triggers: {triggers.Count}");
        text.AppendLine($"Shops: {shops.Count}");
        text.AppendLine($"Banks: {banks.Count}");
        text.AppendLine($"Trainers: {shops.Concat(banks).Count(IsTrainer)}");
        text.AppendLine("Required room probes:");
        foreach (var query in RequiredRoomQueries)
        {
            var match = FindRoomRecord(rooms, query);
            text.AppendLine(match is null
                ? $"  {query}: NOT FOUND in Data\\Classic rooms"
                : $"  {query}: {ReadString(match.Value, "id")} descLength={ReadString(match.Value, "description").Length}");
        }
        if (rooms.Count > 0 && rooms.Count(HasDescription) == 0)
            text.AppendLine("STOP: room descriptions do not exist in Data\\Classic; compiler/import output is the first failure layer.");
        text.AppendLine("===================================================");
        return text.ToString();
    }

    public static string RenderRuntimeAudit(string projectRoot, RuntimeWorld world, RuntimeLivingWorldService living)
    {
        var dataRoot = Path.Combine(projectRoot, world.Definition.DataRoot);
        var services = TryLoadServices(projectRoot, world.Definition.Id);
        var quests = TryLoadNarrative(projectRoot, world.Definition.Id, "quests");
        var triggers = ReadRecords(Path.Combine(dataRoot, world.Definition.TriggersFile));
        var live = living.Capture();
        var gates = living.CaptureGates();
        var allExits = world.AllRooms().SelectMany(room => room.Exits).ToArray();

        var text = new StringBuilder();
        text.AppendLine("========== CLASSIC RUNTIME AUDIT ==========");
        text.AppendLine($"World: {world.Definition.Id} [{world.Definition.Type}]");
        text.AppendLine($"Rooms loaded: {world.RoomCount}");
        text.AppendLine($"Rooms with descriptions: {world.AllRooms().Count(room => !string.IsNullOrWhiteSpace(room.Description))}");
        text.AppendLine($"NPC definitions: {world.NpcCount}");
        text.AppendLine($"Monster definitions: {world.Npcs.Values.Count(IsMonsterDefinition)}");
        text.AppendLine($"Spawn groups: {world.Spawns.Select(spawn => spawn.MonsterGroupId).Where(value => value > 0).Distinct().Count()}");
        text.AppendLine($"Spawn records: {world.SpawnCount}");
        text.AppendLine($"Live NPC instances: {live.Npcs.Count}");
        text.AppendLine($"Live monster instances: {live.Npcs.Count(IsLiveMonster)}");
        text.AppendLine($"Ground objects: {CountGeneratedGroundObjects(projectRoot, world.Definition.Id)}");
        text.AppendLine($"Doors: {gates.Count(gate => gate.GateKind.Equals("door", StringComparison.OrdinalIgnoreCase))}");
        text.AppendLine($"Shops: {services?.Services.Count(service => service.ServiceKind.Equals("shop", StringComparison.OrdinalIgnoreCase)) ?? 0}");
        text.AppendLine($"Banks: {services?.Services.Count(service => service.ServiceKind.Equals("bank", StringComparison.OrdinalIgnoreCase)) ?? 0}");
        text.AppendLine($"Trainers: {services?.Services.Count(IsTrainer) ?? 0}");
        text.AppendLine($"Quests: {quests?.Count ?? 0}");
        text.AppendLine($"Triggers: {triggers.Count}");
        text.AppendLine($"Classic runtime families validated: {world.DataCatalog.Families.Count}");
        text.AppendLine($"Active runtime commands: {world.CommandCatalog.ActiveCommandCount}");
        text.AppendLine($"Generated command evidence records: {world.CommandCatalog.GeneratedEvidenceCount}");
        text.AppendLine($"Generated commands activated: {world.CommandCatalog.ActivatedGeneratedCommandCount}");
        text.AppendLine($"Generated commands quarantined: {world.CommandCatalog.QuarantinedGeneratedCommandCount}");
        text.AppendLine($"NPC AI schema: {world.NpcAiCatalog.SchemaVersion}");
        text.AppendLine($"NPC AI relationships: {world.NpcAiCatalog.RelationshipCount}/{world.NpcAiCatalog.DeclaredSpawnCount}");
        text.AppendLine($"NPC AI active capability declarations: {world.NpcAiCatalog.ActiveCapabilities.Count}");
        text.AppendLine($"NPC AI native-blocked capability declarations: {world.NpcAiCatalog.BlockedNativeCapabilities.Count}");
        text.AppendLine($"NPC AI catalog diagnostics: {world.NpcAiCatalog.Diagnostics.Count}");
        text.AppendLine($"Status-effect definitions: {world.StatusEffectCatalog.DefinitionCount}/{world.StatusEffectCatalog.DeclaredRecordCount}");
        text.AppendLine($"Status-effect spell relationships: {world.StatusEffectCatalog.SpellRelationshipCount}");
        text.AppendLine($"Status-effect NPC relationships: {world.StatusEffectCatalog.NpcRelationshipCount}");
        text.AppendLine($"Status effects runtime-authoritative: {world.StatusEffectCatalog.RuntimeAuthoritativeCount}");
        text.AppendLine($"Status effects quarantined: {world.StatusEffectCatalog.QuarantinedCount}");
        text.AppendLine($"Status-effect catalog diagnostics: {world.StatusEffectCatalog.Diagnostics.Count}");
        text.AppendLine($"Loot-rule definitions: {world.LootRuleCatalog.DefinitionCount}/{world.LootRuleCatalog.DeclaredRecordCount}");
        text.AppendLine($"Loot rules runtime-authoritative: {world.LootRuleCatalog.RuntimeAuthoritativeCount}");
        text.AppendLine($"Loot rules quarantined: {world.LootRuleCatalog.QuarantinedCount}");
        text.AppendLine($"Loot-rule catalog diagnostics: {world.LootRuleCatalog.Diagnostics.Count}");
        text.AppendLine($"Unresolved room references: {allExits.Count(exit => exit.HasBlockedDestination || world.FindRoom(exit.TargetMapId, exit.TargetRoomNumber) is null)}");
        text.AppendLine($"Unresolved NPC references: {world.UnresolvedGroupCount + world.UnresolvedBossCount}");
        text.AppendLine($"Unresolved spawn references: {world.Spawns.Count(spawn => !spawn.PrimaryRoomAvailable || !spawn.FullyResolved)}");
        text.AppendLine($"Spawn placement validation failures: {living.CaptureSpawnPlacementIssues().Count}");
        text.AppendLine("Required room probes:");
        foreach (var query in RequiredRoomQueries)
        {
            var room = FindRuntimeRoom(world, query);
            text.AppendLine(room is null
                ? $"  {query}: NOT FOUND in runtime world"
                : $"  {query}: {room.Id} descLength={room.Description.Length} spawnRecords={world.FindSpawnsByRoom(room.Id).Count} liveNpcs={living.NpcsInRoom(room.Id).Count}");
        }
        text.AppendLine("===========================================");
        return text.ToString();
    }

    public static string RenderRoomInspection(string projectRoot, RuntimeWorld world, RuntimeLivingWorldService living, string query, string? renderedRoom = null)
    {
        var room = FindRuntimeRoom(world, query);
        if (room is null)
            return $"audit room \"{query}\": room not found by id, exact/partial name, or room number.";

        var dataRoom = FindRoomRecord(ReadRecords(Path.Combine(projectRoot, world.Definition.DataRoot, world.Definition.RoomsFile)), room.Id)
            ?? FindRoomRecord(ReadRecords(Path.Combine(projectRoot, world.Definition.DataRoot, world.Definition.RoomsFile)), room.Name)
            ?? FindRoomRecord(ReadRecords(Path.Combine(projectRoot, world.Definition.DataRoot, world.Definition.RoomsFile)), room.RoomNumber.ToString());
        var dataDescription = dataRoom is null ? string.Empty : ReadString(dataRoom.Value, "description");
        var liveNpcs = living.NpcsInRoom(room.Id);
        var liveMonsters = liveNpcs.Where(IsLiveMonster).ToArray();
        var liveNonMonsters = liveNpcs.Where(npc => !IsLiveMonster(npc)).ToArray();
        var spawnRecords = world.FindSpawnsByRoom(room.Id);
        var gates = living.CaptureGates().Where(gate => gate.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        var services = TryLoadServices(projectRoot, world.Definition.Id)?.Services.Where(service => ServiceInRoom(service, room.RoomNumber)).ToArray()
            ?? Array.Empty<RuntimeServiceDefinition>();
        var triggers = CountGeneratedRoomRecords(projectRoot, world, world.Definition.TriggersFile, room);
        var scripts = CountGeneratedRoomRecords(projectRoot, world, "scripts.json", room);
        var roomRendererOutput = RoomRenderer.Render(world, room);
        var effectiveRender = renderedRoom ?? roomRendererOutput;

        var text = new StringBuilder();
        text.AppendLine($"========== CLASSIC ROOM AUDIT: {query} ==========");
        text.AppendLine($"Room title: {room.Name}");
        text.AppendLine($"Runtime room ID: {room.Id}");
        text.AppendLine($"Map ID: {room.MapId}");
        text.AppendLine($"Numeric room ID: {room.RoomNumber}");
        text.AppendLine($"Room NPC definition: {room.NpcDefinition}");
        text.AppendLine($"Description length: {room.Description.Length}");
        text.AppendLine($"Description preview: {Preview(room.Description)}");
        text.AppendLine($"Description exists in Data\\Classic: {YesNo(!string.IsNullOrWhiteSpace(dataDescription))}");
        text.AppendLine($"Description loaded into runtime: {YesNo(!string.IsNullOrWhiteSpace(room.Description))}");
        text.AppendLine($"Room renderer outputs description: {YesNo(!string.IsNullOrWhiteSpace(room.Description) && effectiveRender.Contains(room.Description, StringComparison.Ordinal))}");
        text.AppendLine($"Doors: {gates.Count(gate => gate.GateKind.Equals("door", StringComparison.OrdinalIgnoreCase))}");
        text.AppendLine("Exit flags:");
        foreach (var exit in room.Exits.OrderBy(exit => exit.Direction, StringComparer.OrdinalIgnoreCase))
            text.AppendLine($"  {exit.Direction}: target={exit.TargetKey}; gate={exit.GateKind}; qualifier={exit.Qualifier}; blocked={exit.HasBlockedDestination}");
        text.AppendLine("Spawn groups:");
        foreach (var group in spawnRecords.Select(spawn => spawn.MonsterGroupId).Where(value => value > 0).Distinct().OrderBy(value => value))
            text.AppendLine($"  group={group}; definitions={world.Npcs.Values.Count(npc => npc.GroupId == group)}");
        text.AppendLine($"Spawn records: {spawnRecords.Count}");
        foreach (var spawn in spawnRecords.Take(20))
            text.AppendLine($"  {spawn.Id}: group={spawn.MonsterGroupId}; boss={spawn.BossLegacyId}; definitions={(spawn.BossDefinition is not null ? 1 : spawn.GroupDefinitions.Count)}; room={spawn.ResolvedRuntimeRoomId}");
        if (spawnRecords.Count > 20) text.AppendLine($"  ... {spawnRecords.Count - 20} additional spawn record(s)");
        text.AppendLine($"Live monsters: {liveMonsters.Length}");
        foreach (var npc in liveMonsters.Take(20))
            text.AppendLine(RenderLiveNpcAuditLine(world, npc));
        if (liveMonsters.Length > 20) text.AppendLine($"  ... {liveMonsters.Length - 20} additional live monster(s)");
        text.AppendLine($"NPCs: {liveNonMonsters.Length}");
        foreach (var npc in liveNonMonsters.Take(20))
            text.AppendLine(RenderLiveNpcAuditLine(world, npc));
        if (liveNonMonsters.Length > 20) text.AppendLine($"  ... {liveNonMonsters.Length - 20} additional NPC(s)");
        text.AppendLine("Displayed entity placement proof:");
        foreach (var npc in liveNpcs.Take(40))
            text.AppendLine(RenderLiveNpcAuditLine(world, npc));
        if (liveNpcs.Count > 40) text.AppendLine($"  ... {liveNpcs.Count - 40} additional displayed entity/entities");
        text.AppendLine($"Objects: {living.ObjectsInRoom(room.Id).Count}");
        text.AppendLine($"Triggers: {triggers}");
        text.AppendLine($"Scripts: {scripts}");
        text.AppendLine($"Services: {services.Length}");
        foreach (var service in services)
            text.AppendLine($"  {service.Id}: kind={service.ServiceKind}; name={ReadString(service.GeneratedRecord, "name")}");
        text.AppendLine("=================================================");
        return text.ToString();
    }

    public static string RenderSpawnVerification(RuntimeWorld world, RuntimeLivingWorldService living)
    {
        var live = living.Capture().Npcs.GroupBy(npc => npc.SpawnId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var text = new StringBuilder();
        text.AppendLine("========== CLASSIC SPAWN VERIFICATION ==========");
        var issues = living.CaptureSpawnPlacementIssues();
        text.AppendLine($"PlacementValidationFailures: {issues.Count}");
        foreach (var issue in issues.Take(40))
            text.AppendLine($"UNRESOLVED\t{issue.SpawnId}\t{issue.SourceRoomReference}\t{issue.ResolvedRuntimeRoomId}\t{issue.Reason}");
        if (issues.Count > 40) text.AppendLine($"UNRESOLVED\t... {issues.Count - 40} additional placement issue(s)");
        text.AppendLine("SpawnId\tRoom\tDefinitionCount\tSpawnQuantity\tLiveInstancesCreated");
        foreach (var spawn in world.Spawns.OrderBy(spawn => spawn.ResolvedRuntimeRoomId, StringComparer.OrdinalIgnoreCase).ThenBy(spawn => spawn.Id, StringComparer.OrdinalIgnoreCase))
        {
            var definitions = spawn.BossDefinition is not null ? 1 : spawn.GroupDefinitions.Count;
            live.TryGetValue(spawn.Id, out var instances);
            text.AppendLine($"{spawn.Id}\t{spawn.ResolvedRuntimeRoomId}\t{definitions}\t{ResolveSpawnQuantity(spawn)}\t{instances}");
        }
        text.AppendLine("================================================");
        return text.ToString();
    }

    private static IReadOnlyList<JsonElement> ReadRecords(string path)
    {
        if (!File.Exists(path)) return Array.Empty<JsonElement>();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var records = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("record_parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, relative.Replace('/', Path.DirectorySeparatorChar));
                records.AddRange(ReadRecords(partPath));
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            records.AddRange(root.EnumerateArray().Select(record => record.Clone()));
        }
        else if (root.ValueKind == JsonValueKind.Object &&
                 root.TryGetProperty("records", out var nested) &&
                 nested.ValueKind == JsonValueKind.Array)
        {
            records.AddRange(nested.EnumerateArray().Select(record => record.Clone()));
        }
        return records;
    }

    private static RuntimeRoom? FindRuntimeRoom(RuntimeWorld world, string query)
    {
        var value = (query ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (world.FindRoomById(value) is { } byId) return byId;
        if (int.TryParse(value, out var roomNumber) && world.FindRoomByNumber(roomNumber) is { } byNumber) return byNumber;
        return world.AllRooms()
            .Where(room => room.Name.Equals(value, StringComparison.OrdinalIgnoreCase) || room.Name.Contains(value, StringComparison.OrdinalIgnoreCase))
            .OrderBy(room => room.Name.Length)
            .ThenBy(room => room.MapId)
            .ThenBy(room => room.RoomNumber)
            .FirstOrDefault();
    }

    private static JsonElement? FindRoomRecord(IReadOnlyList<JsonElement> rooms, string query)
    {
        var value = (query ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value)) return null;
        foreach (var room in rooms)
        {
            var id = ReadString(room, "id");
            var name = ReadString(room, "name");
            var number = ReadInt(room, "room_number", ReadInt(room, "roomNumber", ReadInt(room, "legacy_id", 0)));
            if (id.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                number.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
                return room;
        }
        return null;
    }

    private static RuntimeServiceCatalog? TryLoadServices(string projectRoot, string worldId)
    {
        try { return RuntimeServiceCatalog.Load(projectRoot, worldId); }
        catch { return null; }
    }

    private static RuntimeNarrativeCatalog? TryLoadNarrative(string projectRoot, string worldId, string family)
    {
        try { return RuntimeNarrativeCatalog.Load(projectRoot, worldId, family); }
        catch { return null; }
    }

    private static int CountGeneratedGroundObjects(string projectRoot, string worldId)
    {
        var dataRoot = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
        var placements=Path.Combine(dataRoot,"room_item_placements.json");
        if(File.Exists(placements))return ReadRecords(placements).Count;
        return ReadRecords(Path.Combine(dataRoot, "items.json")).Count(record =>
            HasAnyProperty(record, "room_id", "room_ref", "room_number", "location_room", "spawn_room"));
    }

    private static int CountGeneratedRoomRecords(string projectRoot, RuntimeWorld world, string fileName, RuntimeRoom room)
    {
        var path = Path.Combine(projectRoot, world.Definition.DataRoot, fileName);
        return ReadRecords(path).Count(record => RecordMatchesRoom(record, room));
    }

    private static bool RecordMatchesRoom(JsonElement record, RuntimeRoom room)
    {
        var id = ReadString(record, "room_id", ReadString(record, "from_room_id", ReadString(record, "room_ref")));
        if (id.Equals(room.Id, StringComparison.OrdinalIgnoreCase)) return true;
        var roomNumber = ReadInt(record, "room_number", ReadInt(record, "from_room_number", ReadInt(record, "from_room", 0)));
        var mapId = ReadInt(record, "map_id", ReadInt(record, "from_map_number", room.MapId));
        return roomNumber == room.RoomNumber && mapId == room.MapId;
    }

    private static bool ServiceInRoom(RuntimeServiceDefinition service, int roomNumber) =>
        TryReadServiceRoomNumber(service.GeneratedRecord, out var generatedRoom) && generatedRoom == roomNumber;

    private static bool TryReadServiceRoomNumber(JsonElement record, out int roomNumber)
    {
        roomNumber = 0;
        var property = record.TryGetProperty("room_number", out var direct)
            ? direct
            : record.TryGetProperty("room_ref", out var reference)
                ? reference
                : default;
        if (property.ValueKind == JsonValueKind.Undefined) return false;
        if (property.ValueKind == JsonValueKind.Number) return property.TryGetInt32(out roomNumber);
        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.StartsWith("room.", StringComparison.OrdinalIgnoreCase)) text = text[5..];
        return int.TryParse(text, out roomNumber);
    }

    private static bool HasDescription(JsonElement record) => !string.IsNullOrWhiteSpace(ReadString(record, "description"));
    private static bool IsDoor(JsonElement record) => ReadString(record, "gate_kind").Equals("door", StringComparison.OrdinalIgnoreCase);
    private static bool IsTrainer(JsonElement record) => ReadString(record, "service_kind").Contains("trainer", StringComparison.OrdinalIgnoreCase);
    private static bool IsTrainer(RuntimeServiceDefinition service) => ReadString(service.GeneratedRecord, "service_kind").Contains("trainer", StringComparison.OrdinalIgnoreCase);
    private static bool IsMonsterDefinition(JsonElement record) => ReadNestedInt(record, "combat", "hp") > 1;
    private static bool IsMonsterDefinition(RuntimeNpcDefinition npc) => npc.HitPoints > 1 && !npc.Name.Contains("trainer", StringComparison.OrdinalIgnoreCase) && !npc.Name.Contains("shop", StringComparison.OrdinalIgnoreCase);
    private static bool IsLiveMonster(RuntimeLivingNpcState npc) => ResolveLiveDisposition(npc).Equals("hostile", StringComparison.OrdinalIgnoreCase);
    private static int ReadSpawnGroup(JsonElement record) => record.TryGetProperty("spawn_model", out var model) && model.ValueKind == JsonValueKind.Object ? ReadInt(model, "monster_type", 0) : 0;
    private static int ResolveSpawnQuantity(RuntimeNpcSpawn spawn) => spawn.BossDefinition is not null ? 1 : Math.Max(0, spawn.GroupDefinitions.Count);

    private static string RenderLiveNpcAuditLine(RuntimeWorld world, RuntimeLivingNpcState npc)
    {
        var spawn = world.FindSpawnById(npc.SpawnId);
        var source = npc.Metadata.TryGetValue("source_room_reference", out var sourceRoom) ? sourceRoom : FormatSourceRoomReference(spawn);
        var resolved = spawn?.ResolvedRuntimeRoomId ?? (npc.Metadata.TryGetValue("resolved_runtime_room_id", out var resolvedRoom) ? resolvedRoom : string.Empty);
        var alignment = npc.Metadata.TryGetValue("alignment", out var value) ? value : string.Empty;
        var disposition = ResolveLiveDisposition(npc);
        var hostility = npc.Metadata.TryGetValue("combat_hostility_state", out var state) ? state : "unknown";
        return $"  {npc.Name}: definition={npc.NpcId}; spawn={npc.SpawnId}; sourceRoom={source}; resolvedRoom={resolved}; liveRoom={npc.RoomId}; alignment={alignment}; disposition={disposition}; combatHostility={hostility}; hp={npc.CurrentHitPoints}/{npc.MaximumHitPoints}";
    }

    private static string FormatSourceRoomReference(RuntimeNpcSpawn? spawn) =>
        spawn is null ? string.Empty : spawn.SourceMapId > 0 && spawn.SourceRoomNumber > 0 ? new RuntimeRoomKey(spawn.SourceMapId, spawn.SourceRoomNumber).ToString() : spawn.RoomId;

    private static string ResolveLiveDisposition(RuntimeLivingNpcState npc)
    {
        if (npc.Metadata.TryGetValue("disposition", out var disposition) && !string.IsNullOrWhiteSpace(disposition))
            return disposition;
        if (npc.Metadata.TryGetValue("alignment", out var alignment) && alignment.Contains("evil", StringComparison.OrdinalIgnoreCase))
            return "hostile";
        if (npc.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) ||
            npc.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase) ||
            npc.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase))
            return "friendly";
        return "neutral";
    }

    private static bool HasAnyProperty(JsonElement record, params string[] names) => names.Any(name => record.TryGetProperty(name, out _));
    private static string YesNo(bool value) => value ? "YES" : "NO";
    private static string Preview(string text) => string.IsNullOrWhiteSpace(text) ? "<empty>" : text.Length <= 120 ? text : text[..120] + "...";
    private static string ReadString(JsonElement element, string name, string fallback = "") => JsonValueReader.ReadString(element, name, fallback);
    private static int ReadInt(JsonElement element, string name, int fallback) => JsonValueReader.ReadInt(element, name, fallback);
    private static int ReadNestedInt(JsonElement element, string objectName, string name)
    {
        return element.TryGetProperty(objectName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? ReadInt(nested, name, 0)
            : 0;
    }
}
