using System.Collections.Concurrent;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeLivingNpcState(
    string InstanceId,
    string SpawnId,
    string NpcId,
    string Name,
    string RoomId,
    string HomeRoomId,
    int CurrentHitPoints,
    int MaximumHitPoints,
    bool Alive,
    DateTimeOffset? RespawnAtUtc,
    string InteractionModel,
    int RespawnMinutes,
    IReadOnlyDictionary<string,string> Metadata);

public sealed record RuntimeLivingWorldObjectDefinition(
    string Id,
    string RoomId,
    string Kind,
    string Name,
    string? Direction,
    string? KeyItemId,
    bool InitiallyOpen,
    bool InitiallyLocked,
    string? TargetRoomId,
    IReadOnlyDictionary<string,string> Metadata);

public sealed record RuntimeLivingWorldObjectState(
    string Id,
    bool Open,
    bool Locked,
    bool Activated,
    DateTimeOffset UpdatedAtUtc);

public sealed record RuntimeLivingWorldSnapshot(
    string WorldId,
    IReadOnlyList<RuntimeLivingNpcState> Npcs,
    IReadOnlyList<RuntimeLivingWorldObjectState> Objects,
    DateTimeOffset SavedAtUtc);

public sealed record RuntimeSpawnPlacementIssue(
    string SpawnId,
    string SourceRoomReference,
    string ResolvedRuntimeRoomId,
    string Reason);

/// <summary>
/// Authoritative shared runtime state for generated NPC spawns and generated world interaction objects.
/// The service never invents NPCs, routes, doors, keys, hazards, or triggers: all definitions are loaded
/// from generated Data/&lt;world&gt; files. Runtime state is persisted separately under Saves/Worlds.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    public bool ReplaceNpcMetadata(string instanceId, IReadOnlyDictionary<string, string> metadata)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current)) return false;
            _npcs[current.InstanceId] = current with { Metadata = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase) };
            Save();
            return true;
        }
    }

    private readonly object _gate = new();
    private readonly string _projectRoot;
    private readonly RuntimeWorld _world;
    private readonly string _persistenceRoot;
    private readonly Dictionary<string,RuntimeLivingNpcState> _npcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeLivingWorldObjectDefinition> _objectDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeLivingWorldObjectState> _objects = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RuntimeSpawnPlacementIssue> _spawnPlacementIssues = [];

    public RuntimeLivingWorldService(string projectRoot, RuntimeWorld world, string? persistenceRoot = null)
    {
        _projectRoot = projectRoot;
        _world = world;
        _persistenceRoot = string.IsNullOrWhiteSpace(persistenceRoot) ? Path.Combine(projectRoot,"Saves","Worlds") : persistenceRoot;
        LoadGeneratedNpcInstances();
        LoadGeneratedWorldObjects();
        InitializeLivingWorldCompletion();
        Restore();
        RestoreLivingWorldCompletion();
    }

    public string WorldId => _world.Definition.Id;
    public int NpcInstanceCount { get { lock (_gate) return _npcs.Count; } }
    public int WorldObjectCount { get { lock (_gate) return _objectDefinitions.Count; } }
    public IReadOnlyList<RuntimeLivingWorldObjectDefinition> CaptureWorldObjectDefinitions()
    {
        lock (_gate) return _objectDefinitions.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<RuntimeSpawnPlacementIssue> CaptureSpawnPlacementIssues()
    {
        lock (_gate) return _spawnPlacementIssues.ToArray();
    }

    public IReadOnlyList<RuntimeLivingNpcState> NpcsInRoom(string roomId)
    {
        lock (_gate)
            return _npcs.Values.Where(x => x.Alive && x.RoomId.Equals(roomId,StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public RuntimeLivingNpcState SummonNpc(string npcId, string roomId, string source)
    {
        lock (_gate)
        {
            if (_world.FindRoomById(roomId) is null)
                throw new InvalidOperationException($"summon room {roomId} is not generated");
            var legacyNpcId = ParseLegacyNumber(npcId);
            if (legacyNpcId <= 0 || !_world.Npcs.TryGetValue(legacyNpcId, out var definition))
                throw new InvalidOperationException($"summon target {npcId} is not generated");

            var ordinal = _npcs.Values.Count(x => x.SpawnId.Equals(source, StringComparison.OrdinalIgnoreCase)) + 1;
            var instanceId = $"{source}:{npcId}:{ordinal}";
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source_room_reference"] = roomId,
                ["resolved_runtime_room_id"] = roomId,
                ["source_map_id"] = "0",
                ["source_room_number"] = "0",
                ["alignment"] = definition.Alignment,
                ["disposition"] = definition.Alignment.Contains("evil", StringComparison.OrdinalIgnoreCase) ? "hostile" : "neutral",
                ["combat_hostility_state"] = "idle",
                ["summoned_by"] = source,
                ["see_hidden"] = definition.SeeHidden ? "1" : "0",
                ["charm_level"] = definition.CharmLevel.ToString(),
                ["charm_resistance"] = definition.CharmResistance.ToString(),
                ["follow_value"] = definition.FollowValue.ToString(),
                ["regen_time"] = definition.RegenTime.ToString(),
                ["hp_regen"] = definition.HitPointRegen.ToString(),
                ["respawn_hours"] = definition.RespawnHours.ToString(),
                ["undead"] = definition.Undead ? "1" : "0",
                ["game_limit"] = definition.GameLimit.ToString(),
                ["gender"] = definition.Gender.ToString(),
                ["monster_type"] = definition.MonsterType.ToString(),
                ["experience_multiplier"] = definition.ExperienceMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            var state = new RuntimeLivingNpcState(
                instanceId,
                source,
                definition.Id,
                definition.Name,
                roomId,
                roomId,
                Math.Max(1, definition.HitPoints),
                Math.Max(1, definition.HitPoints),
                true,
                null,
                "summoned_quest_intent",
                0,
                metadata);
            _npcs[instanceId] = state;
            // Summoned NPCs are created after the living-world AI tables have already
            // been initialized. Register a matching AI state immediately so scheduler,
            // aggro, death, and persistence paths never index a missing instance key.
            _npcAi[instanceId] = new RuntimeNpcAiState(
                instanceId,
                "idle",
                null,
                roomId,
                roomId,
                0,
                DateTimeOffset.UtcNow);
            PublishLivingWorldEventLocked("npc-summoned", instanceId, roomId, DateTimeOffset.UtcNow);
            Save();
            SaveLivingWorldCompletionLocked();
            return state;
        }
    }

    public IReadOnlyList<RuntimeLivingWorldObjectDefinition> ObjectsInRoom(string roomId)
    {
        lock (_gate)
            return _objectDefinitions.Values.Where(x=>x.RoomId.Equals(roomId,StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool CanTraverse(RuntimeRoom room, string direction, out string failure)
    {
        var normalized = NormalizeDirection(direction);
        lock (_gate)
        {
            if (!_world.TryResolveExit(room, normalized, out var exit, out _, out failure) || exit is null)
                return false;

            // Compatibility support for older evidence catalogs. Promoted MMExplorer
            // movement authority lives directly on the map-qualified exit record.
            foreach (var trigger in _world.FindMovementTriggers(room.MapId, room.RoomNumber, normalized))
            {
                if (!trigger.TargetRoomExists || trigger.HasInvalidDestination)
                {
                    failure = $"Movement trigger {trigger.Id} blocks this route because its generated destination is unavailable.";
                    return false;
                }
            }

            var door = _objectDefinitions.Values.FirstOrDefault(x =>
                x.RoomId.Equals(room.Id,StringComparison.OrdinalIgnoreCase)
                && x.Kind.Equals("door",StringComparison.OrdinalIgnoreCase)
                && NormalizeDirection(x.Direction ?? string.Empty).Equals(normalized,StringComparison.OrdinalIgnoreCase));
            if (door is null) { failure=string.Empty; return true; }
            if (!_objects.TryGetValue(door.Id, out var state))
            {
                failure = $"{door.Name} has no runtime state.";
                return false;
            }
            if (state.Locked) { failure=$"{door.Name} is locked."; return false; }
            if (!state.Open) { failure=$"{door.Name} is closed."; return false; }
            failure=string.Empty; return true;
        }
    }


    public string DescribeExit(RuntimeRoom room, string direction)
    {
        var normalized = NormalizeDirection(direction);
        lock (_gate)
        {
            var door = _objectDefinitions.Values.FirstOrDefault(x =>
                x.RoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase)
                && x.Kind.Equals("door", StringComparison.OrdinalIgnoreCase)
                && NormalizeDirection(x.Direction ?? string.Empty).Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (door is null || !_objects.TryGetValue(door.Id, out var state)) return normalized;
            var flags = new List<string>();
            if (!state.Open) flags.Add("closed");
            if (state.Locked) flags.Add("locked");
            return flags.Count == 0 ? normalized : $"{normalized} [{string.Join(", ", flags)}]";
        }
    }

    public string Interact(RuntimeSession session, RuntimeCommand command, RuntimeItemState? inventory)
    {
        if (command.Arguments.Count==0) return $"Use: {command.Name} <object>.";
        var query=command.ArgumentText.Trim();
        lock (_gate)
        {
            var matches=_objectDefinitions.Values.Where(x=>x.RoomId.Equals(session.CurrentRoom.Id,StringComparison.OrdinalIgnoreCase)
                && (x.Id.Equals(query,StringComparison.OrdinalIgnoreCase)
                    || x.Name.Equals(query,StringComparison.OrdinalIgnoreCase)
                    || x.Name.Contains(query,StringComparison.OrdinalIgnoreCase)
                    || MatchesAlias(x, command.Name, query))).ToArray();
            if(matches.Length==0)return $"You do not see an interactive object matching '{query}' here.";
            if(matches.Length>1)return $"More than one object matches '{query}'; use its generated ID.";
            var definition=matches[0]; var before=_objects[definition.Id];
            try
            {
                var after=command.Name switch
                {
                    "open" => Open(definition,before),
                    "close" => Close(definition,before),
                    "unlock" => Unlock(definition,before,inventory),
                    "lock" => Lock(definition,before,inventory),
                    "pull" or "push" or "activate" => Activate(definition,before),
                    _ => throw new InvalidOperationException("unsupported-world-interaction")
                };
                _objects[definition.Id]=after; Save();
                return $"You {command.Name} {definition.Name}.";
            }
            catch { _objects[definition.Id]=before; throw; }
        }
    }

    public IReadOnlyList<string> Tick(DateTimeOffset nowUtc)
    {
        var events=new List<string>();
        lock (_gate)
        {
            events.AddRange(TickNpcSpawnLifecycleLocked(nowUtc, null).Transitions.Select(x => x.EventCode));
            events.AddRange(TickLivingWorldCompletionLocked(nowUtc));
            if(events.Count>0){ Save(); SaveLivingWorldCompletionLocked(); }
        }
        return events;
    }

    public bool ApplyNpcDamage(string instanceId,int damage,DateTimeOffset nowUtc)
    {
        if(damage<0)throw new ArgumentOutOfRangeException(nameof(damage));
        lock(_gate)
        {
            if(!_npcs.TryGetValue(instanceId,out var current)||!current.Alive)return false;
            var hp=Math.Max(0,current.CurrentHitPoints-damage);
            if (hp == 0)
            {
                EnterNpcRespawnWaitLocked(instanceId, nowUtc, "combat-damage", null);
                PublishLivingWorldEventLocked("npc-died", instanceId, current.RoomId, nowUtc);
            }
            else
            {
                _npcs[instanceId]=current with { CurrentHitPoints=hp };
                PublishLivingWorldEventLocked("npc-damaged", instanceId, current.RoomId, nowUtc);
            }
            Save(); SaveLivingWorldCompletionLocked(); return true;
        }
    }

    public bool ApplyNpcHealing(string instanceId,int healing,DateTimeOffset nowUtc)
    {
        if(healing<0)throw new ArgumentOutOfRangeException(nameof(healing));
        lock(_gate)
        {
            if(!_npcs.TryGetValue(instanceId,out var current)||!current.Alive)return false;
            var hp=Math.Min(current.MaximumHitPoints,current.CurrentHitPoints+healing);
            _npcs[instanceId]=current with { CurrentHitPoints=hp };
            PublishLivingWorldEventLocked("npc-healed", instanceId, current.RoomId, nowUtc);
            Save(); SaveLivingWorldCompletionLocked(); return true;
        }
    }

    public RuntimeLivingWorldSnapshot Capture()
    {
        lock(_gate)return new(WorldId,_npcs.Values.OrderBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray(),_objects.Values.OrderBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).ToArray(),DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<string> Validate()
    {
        lock(_gate)
        {
            var failures=new List<string>();
            if(_npcs.Values.Any(x=>x.CurrentHitPoints<0||x.CurrentHitPoints>x.MaximumHitPoints))failures.Add("invalid-npc-hit-points");
            if(_npcs.Values.Any(x=>!x.Alive&&x.RespawnAtUtc is null&&!x.Metadata.ContainsKey("npc_respawn_block_reason")))failures.Add("dead-npc-missing-respawn-or-block");
            if(_npcs.Values.Any(x=>_world.FindRoomById(x.RoomId) is null))failures.Add("npc-room-unresolved");
            if(_spawnPlacementIssues.Count>0)failures.Add("spawn-placement-unresolved");
            if (!_world.NpcAiCatalog.Passed) failures.Add("npc-ai-catalog-invalid");
            if (!_world.StatusEffectCatalog.Passed) failures.Add("status-effect-catalog-invalid");
            if (!_world.LootRuleCatalog.Passed) failures.Add("loot-rule-catalog-invalid");
            if (_npcs.Values.Any(x => !x.Metadata.ContainsKey("npc_ai_authority") && !x.InteractionModel.Equals("summoned_quest_intent", StringComparison.OrdinalIgnoreCase))) failures.Add("npc-ai-authority-missing");
            if(_objects.Keys.Any(x=>!_objectDefinitions.ContainsKey(x)))failures.Add("unknown-world-object-state");
            return failures;
        }
    }

    private void LoadGeneratedNpcInstances()
    {
        foreach(var spawn in _world.Spawns)
        {
            if (!TryResolveSpawnRoom(spawn, out var room, out var reason))
            {
                _spawnPlacementIssues.Add(new(spawn.Id, FormatSourceRoomReference(spawn), spawn.ResolvedRuntimeRoomId, reason));
                continue;
            }

            var definitions = ResolveMaterializedDefinitions(spawn, room);
            if (definitions.Count == 0)
            {
                _spawnPlacementIssues.Add(new(spawn.Id, FormatSourceRoomReference(spawn), spawn.ResolvedRuntimeRoomId, "spawn-has-no-resolved-npc-definition"));
                continue;
            }

            var ordinal=0;
            foreach(var npc in definitions)
            {
                ordinal++;
                var id=$"{spawn.Id}:{npc.Id}:{ordinal}";
                var aiRelationship = _world.NpcAiCatalog.FindBySpawnId(spawn.Id)
                    ?? throw new InvalidOperationException($"NPC AI relationship missing for generated spawn '{spawn.Id}'.");
                var metadata = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source_room_reference"] = FormatSourceRoomReference(spawn),
                    ["resolved_runtime_room_id"] = room.Id,
                    ["source_map_id"] = spawn.SourceMapId.ToString(),
                    ["source_room_number"] = spawn.SourceRoomNumber.ToString(),
                    ["alignment"] = npc.Alignment,
                    ["disposition"] = ResolveDisposition(npc, spawn),
                    ["combat_hostility_state"] = "idle",
                    ["npc_ai_authority"] = "generated:npc_ai_runtime.json",
                    ["npc_ai_schema_version"] = _world.NpcAiCatalog.SchemaVersion,
                    ["npc_ai_room_reference"] = aiRelationship.RoomReference,
                    ["npc_ai_monster_group_id"] = aiRelationship.MonsterGroupId.ToString(),
                    ["npc_ai_respawn_minutes"] = aiRelationship.RespawnMinutes.ToString(),
                    ["npc_ai_interaction_model"] = aiRelationship.InteractionModel,
                    ["see_hidden"] = npc.SeeHidden ? "1" : "0",
                    ["charm_level"] = npc.CharmLevel.ToString(),
                    ["charm_resistance"] = npc.CharmResistance.ToString(),
                    ["follow_value"] = npc.FollowValue.ToString(),
                    ["regen_time"] = npc.RegenTime.ToString(),
                    ["hp_regen"] = npc.HitPointRegen.ToString(),
                    ["respawn_hours"] = npc.RespawnHours.ToString(),
                    ["undead"] = npc.Undead ? "1" : "0",
                    ["game_limit"] = npc.GameLimit.ToString(),
                    ["gender"] = npc.Gender.ToString(),
                    ["monster_type"] = npc.MonsterType.ToString(),
                    ["experience_multiplier"] = npc.ExperienceMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum_area_population"] = spawn.MaximumAreaPopulation.ToString()
                };
                _npcs[id]=new(id,spawn.Id,npc.Id,npc.Name,room.Id,room.Id,
                    Math.Max(1,npc.HitPoints),Math.Max(1,npc.HitPoints),true,null,aiRelationship.InteractionModel,aiRelationship.RespawnMinutes,metadata);
            }
        }
    }

    private bool TryResolveSpawnRoom(RuntimeNpcSpawn spawn, out RuntimeRoom room, out string reason)
    {
        room = null!;
        reason = string.Empty;

        if (spawn.SourceMapId <= 0 || spawn.SourceRoomNumber <= 0)
        {
            reason = "missing-map-qualified-source-room";
            return false;
        }

        var expectedRoomId = new RuntimeRoomKey(spawn.SourceMapId, spawn.SourceRoomNumber).ToString();
        room = _world.FindRoom(spawn.SourceMapId, spawn.SourceRoomNumber)!;
        if (room is null)
        {
            reason = $"map-qualified-room-not-found:{expectedRoomId}";
            return false;
        }

        if (!room.Id.Equals(expectedRoomId, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"resolved-room-id-mismatch:{expectedRoomId}->{room.Id}";
            return false;
        }

        if (!spawn.ResolvedRuntimeRoomId.Equals(room.Id, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"spawn-runtime-room-mismatch:{spawn.ResolvedRuntimeRoomId}->{room.Id}";
            return false;
        }

        return true;
    }

    private IReadOnlyList<RuntimeNpcDefinition> ResolveMaterializedDefinitions(RuntimeNpcSpawn spawn, RuntimeRoom room)
    {
        if (spawn.BossDefinition is not null)
        {
            return new[] { spawn.BossDefinition };
        }

        var explicitIds = ReadExplicitNpcLegacyIds(room.NpcDefinition);
        if (explicitIds.Count > 0)
        {
            return explicitIds
                .Select(id => _world.Npcs.TryGetValue(id, out var npc) ? npc : null)
                .Where(npc => npc is not null)
                .Cast<RuntimeNpcDefinition>()
                .DistinctBy(npc => npc.Id, StringComparer.OrdinalIgnoreCase)
                .Take(ResolveSpawnMaterializationLimit(spawn, ReadMaximumNpcCount(room.NpcDefinition)))
                .ToArray();
        }

        return spawn.GroupDefinitions
            .OrderBy(npc => npc.LegacyId)
            .Take(ResolveSpawnMaterializationLimit(spawn, Math.Max(1, ReadMaximumNpcCount(room.NpcDefinition))))
            .ToArray();
    }

    private static int ResolveSpawnMaterializationLimit(RuntimeNpcSpawn spawn, int roomLimit)
    {
        var normalizedRoomLimit = Math.Max(1, roomLimit);
        return spawn.MaximumAreaPopulation > 0
            ? Math.Min(normalizedRoomLimit, spawn.MaximumAreaPopulation)
            : normalizedRoomLimit;
    }

    private static IReadOnlyList<int> ReadExplicitNpcLegacyIds(string npcDefinition)
    {
        if (string.IsNullOrWhiteSpace(npcDefinition)) return Array.Empty<int>();
        var payload = npcDefinition;
        var colon = payload.IndexOf(':');
        if (colon >= 0) payload = payload[(colon + 1)..];
        var bracket = payload.IndexOf('[');
        if (bracket >= 0) payload = payload[..bracket];
        return payload.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }

    private static int ReadMaximumNpcCount(string npcDefinition)
    {
        if (string.IsNullOrWhiteSpace(npcDefinition)) return 1;
        var marker = "Max";
        var index = npcDefinition.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return 1;
        var start = index + marker.Length;
        while (start < npcDefinition.Length && char.IsWhiteSpace(npcDefinition[start])) start++;
        var end = start;
        while (end < npcDefinition.Length && char.IsDigit(npcDefinition[end])) end++;
        return end > start && int.TryParse(npcDefinition[start..end], out var count) ? Math.Max(1, count) : 1;
    }

    private static string ResolveDisposition(RuntimeNpcDefinition npc, RuntimeNpcSpawn spawn)
    {
        if (spawn.InteractionModel.Contains("service", StringComparison.OrdinalIgnoreCase) ||
            spawn.InteractionModel.Contains("shop", StringComparison.OrdinalIgnoreCase) ||
            spawn.InteractionModel.Contains("trainer", StringComparison.OrdinalIgnoreCase))
        {
            return "friendly";
        }

        if (npc.Alignment.Contains("evil", StringComparison.OrdinalIgnoreCase))
        {
            return "hostile";
        }

        return "neutral";
    }

    private static string FormatSourceRoomReference(RuntimeNpcSpawn spawn) =>
        spawn.SourceMapId > 0 && spawn.SourceRoomNumber > 0
            ? new RuntimeRoomKey(spawn.SourceMapId, spawn.SourceRoomNumber).ToString()
            : spawn.RoomId;

    private void LoadGeneratedWorldObjects()
    {
        LoadDecodedExitObjects();
        LoadDecodedRoomInteractions();
    }

    private void LoadDecodedExitObjects()
    {
        var exitsPath = Path.Combine(_projectRoot, _world.Definition.DataRoot, _world.Definition.ExitsFile);
        if (!File.Exists(exitsPath)) return;
        using var document = JsonDocument.Parse(File.ReadAllText(exitsPath));
        var records = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("records", out var generated) && generated.ValueKind == JsonValueKind.Array
                ? generated
                : default;
        if (records.ValueKind != JsonValueKind.Array) return;

        foreach (var value in records.EnumerateArray())
        {
            var gateKind = JsonValueReader.ReadString(value, "gate_kind", string.Empty);
            if (!gateKind.Equals("door", StringComparison.OrdinalIgnoreCase)) continue;
            var id = JsonValueReader.ReadString(value, "id", string.Empty);
            var roomId = ReadRelationship(value, "from_room_id", "from_room");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(roomId)) continue;
            var direction = JsonValueReader.ReadString(value, "direction", string.Empty);
            var targetRoomId = ReadRelationship(value, "to_room_id", "to_room");
            var qualifier = JsonValueReader.ReadString(value, "qualifier", "Door");
            var keyItemId = string.Empty;
            if (value.TryGetProperty("prerequisites", out var prerequisites) && prerequisites.ValueKind == JsonValueKind.Object)
                keyItemId = JsonValueReader.ReadString(prerequisites, "required_item_id", string.Empty);
            var definition = new RuntimeLivingWorldObjectDefinition(
                id, roomId, "door", string.IsNullOrWhiteSpace(qualifier) ? $"{direction} door" : qualifier,
                direction, keyItemId, false, false, targetRoomId, ReadMetadata(value));
            _objectDefinitions[id] = definition;
            _objects[id] = new(id, definition.InitiallyOpen, definition.InitiallyLocked, false, DateTimeOffset.UtcNow);
        }
    }

    private void LoadDecodedRoomInteractions()
    {
        var path = Path.Combine(_projectRoot, _world.Definition.DataRoot, _world.Definition.TriggersFile);
        if (!File.Exists(path)) return;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var records = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("records", out var generated) && generated.ValueKind == JsonValueKind.Array
                ? generated
                : default;
        if (records.ValueKind != JsonValueKind.Array) return;

        foreach (var value in records.EnumerateArray())
        {
            var id = JsonValueReader.ReadString(value, "id", string.Empty);
            var roomId = ReadRelationship(value, "from_room_id", "from_room");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(roomId)) continue;
            var direction = JsonValueReader.ReadString(value, "direction", string.Empty);
            var kind = JsonValueReader.ReadString(value, "interaction_kind", "room_interaction");
            var commands = ReadStringArray(value, "commands");
            var name = commands.FirstOrDefault()
                ?? (kind.Equals("exit_action", StringComparison.OrdinalIgnoreCase)
                    ? $"{direction} exit action"
                    : kind.Replace('_', ' '));
            var definition = new RuntimeLivingWorldObjectDefinition(
                id, roomId, kind, name, direction, string.Empty, true, false, string.Empty,
                ReadMetadata(value, commands));
            _objectDefinitions[id] = definition;
            _objects[id] = new(id, true, false, false, DateTimeOffset.UtcNow);
        }
    }

    private void Restore()
    {
        var path=PersistencePath; if(!File.Exists(path))return;
        try
        {
            var snapshot=JsonSerializer.Deserialize<RuntimeLivingWorldSnapshot>(File.ReadAllText(path));
            if(snapshot is null||!snapshot.WorldId.Equals(WorldId,StringComparison.OrdinalIgnoreCase))return;
            foreach(var npc in snapshot.Npcs)
            {
                if(!_npcs.TryGetValue(npc.InstanceId,out var generated))continue;
                _npcs[npc.InstanceId]=npc with
                {
                    RoomId = generated.RoomId,
                    HomeRoomId = generated.HomeRoomId,
                    Metadata = generated.Metadata
                };
            }
            foreach(var state in snapshot.Objects)if(_objects.ContainsKey(state.Id))_objects[state.Id]=state;
        }
        catch(JsonException){ }
    }

    private void Save()
    {
        var path=PersistencePath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+".tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(Capture(),new JsonSerializerOptions{WriteIndented=true})); File.Move(temp,path,true);
    }

    private string PersistencePath=>Path.Combine(_persistenceRoot,WorldId,"living-world.json");
    private static RuntimeLivingWorldObjectState Open(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s){if(s.Locked)throw new InvalidOperationException($"{d.Name} is locked.");return s with{Open=true,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Close(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s)=>s with{Open=false,UpdatedAtUtc=DateTimeOffset.UtcNow};
    private static RuntimeLivingWorldObjectState Unlock(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s,RuntimeItemState? i){RequireKey(d,i);return s with{Locked=false,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Lock(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s,RuntimeItemState? i){RequireKey(d,i);if(s.Open)throw new InvalidOperationException($"Close {d.Name} first.");return s with{Locked=true,UpdatedAtUtc=DateTimeOffset.UtcNow};}
    private static RuntimeLivingWorldObjectState Activate(RuntimeLivingWorldObjectDefinition d,RuntimeLivingWorldObjectState s)=>s with{Activated=!s.Activated,UpdatedAtUtc=DateTimeOffset.UtcNow};
    private static void RequireKey(RuntimeLivingWorldObjectDefinition d,RuntimeItemState? inventory){if(string.IsNullOrWhiteSpace(d.KeyItemId))return;if(inventory is null||!inventory.Items.Any(x=>x.CanonicalItemId.Equals(d.KeyItemId,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException($"You need {d.KeyItemId}.");}
    private static string NormalizeDirection(string value)=>value.Trim().ToUpperInvariant() switch{"NORTH"=>"N","SOUTH"=>"S","EAST"=>"E","WEST"=>"W","NORTHEAST"=>"NE","NORTHWEST"=>"NW","SOUTHEAST"=>"SE","SOUTHWEST"=>"SW","UP"=>"U","DOWN"=>"D",var x=>x};
    private static bool ReadBool(JsonElement e,string name,bool fallback)=>e.TryGetProperty(name,out var v)?v.ValueKind==JsonValueKind.True||v.ValueKind==JsonValueKind.String&&bool.TryParse(v.GetString(),out var b)&&b:fallback;
    private static string ReadRelationship(JsonElement element,string directName,string relationshipName)
    {
        var direct=JsonValueReader.ReadString(element,directName,string.Empty);
        if(!string.IsNullOrWhiteSpace(direct))return direct;
        return element.TryGetProperty("relationships",out var relationships)&&relationships.ValueKind==JsonValueKind.Object
            ? JsonValueReader.ReadString(relationships,relationshipName,string.Empty)
            : string.Empty;
    }
    private static IReadOnlyList<string> ReadStringArray(JsonElement element,string name)=>
        element.TryGetProperty(name,out var values)&&values.ValueKind==JsonValueKind.Array
            ? values.EnumerateArray().Where(value=>value.ValueKind==JsonValueKind.String).Select(value=>value.GetString()??string.Empty).Where(value=>!string.IsNullOrWhiteSpace(value)).ToArray()
            : Array.Empty<string>();
    private static IReadOnlyDictionary<string,string> ReadMetadata(JsonElement element,IReadOnlyList<string>? commands=null)
    {
        var metadata=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var disposition=JsonValueReader.ReadString(element,"runtime_disposition",string.Empty);
        if(!string.IsNullOrWhiteSpace(disposition))metadata["runtime_disposition"]=disposition;
        if(commands is not null&&commands.Count>0)metadata["commands"]=string.Join("|",commands);
        return metadata;
    }
    private static bool MatchesAlias(RuntimeLivingWorldObjectDefinition definition,string verb,string query)
    {
        if(!definition.Metadata.TryGetValue("commands",out var encoded)||string.IsNullOrWhiteSpace(encoded))return false;
        var entered=$"{verb} {query}".Trim();
        return encoded.Split('|',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Any(alias=>alias.Equals(entered,StringComparison.OrdinalIgnoreCase)||alias.Equals(query,StringComparison.OrdinalIgnoreCase));
    }

    private static int ParseLegacyNumber(string id)
    {
        var dot = id.LastIndexOf('.');
        return int.TryParse(dot >= 0 ? id[(dot + 1)..] : id, out var value) ? value : 0;
    }
}

public sealed partial class RuntimeLiveGameplayFacade
{
    private readonly ConcurrentDictionary<string,RuntimeLivingWorldService> _livingWorlds=new(StringComparer.OrdinalIgnoreCase);
    private RuntimeLivingWorldService Living(RuntimeSession session)=>_livingWorlds.GetOrAdd(session.World.Definition.Id,_=>new RuntimeLivingWorldService(_characters.ProjectRoot,session.World));
    public IReadOnlyList<RuntimeLivingNpcState> LivingNpcsInRoom(RuntimeSession session)=>Living(session).NpcsInRoom(session.CurrentRoom.Id);
    public RuntimeLivingNpcState SummonLivingNpc(RuntimeSession session,string npcId,string source)=>Living(session).SummonNpc(npcId,session.CurrentRoom.Id,source);
    public bool CanTraverseLivingWorld(RuntimeSession session,string direction,out string failure)
    {
        var exit = session.World.FindExit(session.CurrentRoom, direction);
        if (exit is not null && exit.GateKind.Equals("hidden", StringComparison.OrdinalIgnoreCase))
        {
            RuntimeBundleDSearchRuntime.EnsureLoaded(_characters.ProjectRoot, session.World.Definition.Id);
            var discovered = RuntimeBundleDSearchRuntime.Registry.IsDiscovered(session.Actor.Id, exit.Id);
            var passable = exit.Qualifier.Contains("Passable", StringComparison.OrdinalIgnoreCase);
            if (!discovered && !passable)
            {
                failure = "You cannot find an exit in that direction.";
                return false;
            }
            if (!discovered && passable)
            {
                RuntimeBundleDSearchRuntime.Registry.Discover(session.Actor.Id, exit.Id);
                RuntimeBundleDSearchRuntime.Save(_characters.ProjectRoot, session.World.Definition.Id, new[] { session.Actor.Id });
            }
        }
        return Living(session).CanTraverse(session.CurrentRoom,direction,out failure);
    }
    public bool TryExecuteTeleporterCommand(RuntimeSession session, RuntimeCommand command, out string result)
    {
        var entered = string.Join(' ', new[] { command.Name }.Concat(command.Arguments)).Trim();
        var living = Living(session);
        var matches = living.CaptureGates().Where(g =>
            g.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase)
            && g.GateKind.Equals("text_command", StringComparison.OrdinalIgnoreCase)
            && g.Commands.Any(alias => alias.Equals(entered, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matches.Length == 0) { result = string.Empty; return false; }
        if (matches.Length > 1) { result = "The decoded command matches more than one teleporter exit."; return true; }
        var gate = matches[0];
        var decision = living.EvaluateExitGate(session.CurrentRoom, gate.Direction, new RuntimeExitGateContext(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0,
            new HashSet<string>(RuntimeBundleDSearchRuntime.Registry.GetDiscoveredTargets(session.Actor.Id), StringComparer.OrdinalIgnoreCase), entered));
        if (!decision.Allowed) { result = $"The teleporter does not activate ({decision.Code})."; return true; }
        var previous = session.CurrentRoom;
        if (!CanMove(session, out var movementFailure)) { result = movementFailure; return true; }
        if (!CanTraverseLivingWorld(session, gate.Direction, out var traversalFailure)) { result = traversalFailure; return true; }
        if (!session.TryMove(gate.Direction, out var movementResult)) { result = movementResult; return true; }
        BroadcastDeparture(session, previous.Id, gate.Direction);
        BroadcastArrival(session);
        Save(session);
        result = RenderRoom(session);
        return true;
    }

    public string DescribeLivingExit(RuntimeSession session,string direction)=>Living(session).DescribeExit(session.CurrentRoom,direction);
    public string ExecuteLivingWorldCommand(RuntimeSession session,RuntimeCommand command)=>Living(session).Interact(session,command,LoadItemState(session));
    public string RenderLivingWorldState(RuntimeSession session)
    {
        var world=Living(session);var snapshot=world.Capture();
        return $"Living world: NPC instances={snapshot.Npcs.Count}; active={snapshot.Npcs.Count(x=>x.Alive)}; exits={session.World.ExitCount}; legacy movement triggers={session.World.TriggerCount}; interaction objects={snapshot.Objects.Count}; validation={string.Join(',',world.Validate())}";
    }

    public Task TickLivingWorldsAsync(CancellationToken cancellationToken)
    {
        var now=DateTimeOffset.UtcNow;
        foreach(var world in _livingWorlds.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            world.Tick(now);
        }
        return Task.CompletedTask;
    }
}

public sealed class RuntimeLivingWorldSmokeReport
{
    public bool Passed=>Failures.Count==0;
    public int GeneratedSpawnCount{get;set;}
    public int GeneratedExitCount{get;set;}
    public int GeneratedInteractionCount{get;set;}
    public int LegacyMovementTriggerCount{get;set;}
    public int RuntimeNpcInstanceCount{get;set;}
    public int GeneratedWorldObjectCount{get;set;}
    public IReadOnlyList<string> GateKinds{get;set;}=Array.Empty<string>();
    public IReadOnlyList<string> ValidationSignals{get;set;}=Array.Empty<string>();
    public List<string> Checks{get;}=[];
    public List<string> Failures{get;}=[];

    public static RuntimeLivingWorldSmokeReport Run(string projectRoot,string worldId)
    {
        var report=new RuntimeLivingWorldSmokeReport();
        string? smokeRoot=null;
        try
        {
            var options=ServerRuntimeOptions.Load(projectRoot);
            var definition=options.Worlds.First(x=>x.Id.Equals(worldId,StringComparison.OrdinalIgnoreCase));
            var world=new RuntimeWorldLoader().Load(projectRoot,definition);
            smokeRoot=Path.Combine(Path.GetTempPath(),"reforged-living-world-smoke-"+Guid.NewGuid().ToString("N"));
            var service=new RuntimeLivingWorldService(projectRoot,world,smokeRoot);
            report.GeneratedSpawnCount=world.SpawnCount;
            report.GeneratedExitCount=world.ExitCount;
            report.GeneratedInteractionCount=CountGeneratedRecords(Path.Combine(projectRoot,definition.DataRoot,definition.TriggersFile));
            report.LegacyMovementTriggerCount=world.TriggerCount;
            report.RuntimeNpcInstanceCount=service.NpcInstanceCount;
            report.GeneratedWorldObjectCount=service.WorldObjectCount;
            report.GateKinds=service.CaptureGates().Select(x=>x.GateKind).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();

            Check(report,world.SpawnCount>0,"generated-spawn-loading");
            Check(report,world.RoomCount==CountGeneratedRecords(Path.Combine(projectRoot,definition.DataRoot,definition.RoomsFile)),"map-qualified-room-loading");
            Check(report,world.ExitCount==61976,"map-qualified-exit-loading-61976");

            var duplicate=world.AllRooms()
                .GroupBy(room=>room.RoomNumber)
                .FirstOrDefault(group=>group.Select(room=>room.MapId).Distinct().Count()>1);
            Check(report,duplicate is not null,"duplicate-room-number-found");
            if(duplicate is not null)
            {
                var copies=duplicate.OrderBy(room=>room.MapId).ToArray();
                Check(report,copies.All(room=>world.FindRoom(room.MapId,room.RoomNumber)?.Id==room.Id),"duplicate-room-number-map-separation");
                Check(report,copies.Select(room=>room.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==copies.Length,"room-identity-not-collapsed");
            }

            Check(report,report.GeneratedInteractionCount>0,"generated-room-interaction-loading");
            Check(report,world.ExitCount>world.TriggerCount,"movement-authority-from-map-qualified-exits");
            Check(report,world.AllRooms().SelectMany(room=>room.Exits).Count(exit=>exit.HasBlockedDestination)==5,"invalid-destination-exit-enforcement-5");

            var expandedCandidateCount=world.Spawns.Where(spawn=>spawn.PrimaryRoomAvailable&&spawn.FullyResolved)
                .Sum(spawn=>spawn.BossDefinition is not null ? 1 : spawn.GroupDefinitions.Count);
            Check(report,expandedCandidateCount>0,"generated-spawn-runtime-authority");
            Check(report,service.NpcInstanceCount>0&&service.NpcInstanceCount<expandedCandidateCount,"persistent-npc-instantiation");
            Check(report,service.GateCount==world.ExitCount,"generated-exit-gate-catalog");
            Check(report,report.GateKinds.Count>0&&report.GateKinds.All(x=>!string.IsNullOrWhiteSpace(x)),"generated-gate-kind-classification");
            Check(report,service.CaptureNpcAi().Count==service.NpcInstanceCount,"npc-ai-state-instantiation");
            var validation=service.Validate();
            report.ValidationSignals=validation.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            var placementIssues=service.CaptureSpawnPlacementIssues();
            Check(report,validation.All(IsClassifiedLivingWorldValidationSignal),"living-world-validator-hard-placement-signal");
            Check(report,placementIssues.All(issue=>!string.IsNullOrWhiteSpace(issue.SpawnId)&&!string.IsNullOrWhiteSpace(issue.SourceRoomReference)&&!string.IsNullOrWhiteSpace(issue.Reason)),"unresolved-spawn-placement-audit");
            var guildAndRiver=world.FindRoom(1,21);
            Check(report,guildAndRiver is not null&&guildAndRiver.Id.Equals("room.map1.21",StringComparison.OrdinalIgnoreCase),"guild-river-map-qualified-room");
            if(guildAndRiver is not null)
            {
                var visible=service.NpcsInRoom(guildAndRiver.Id).ToArray();
                Check(report,visible.Length<=1,"guild-river-no-group-five-explosion");
                Check(report,!visible.Any(x=>x.Name.Contains("storm giant",StringComparison.OrdinalIgnoreCase)),"guild-river-no-storm-giant-leak");
                Check(report,!visible.Any(x=>x.Name.Contains("woodelf",StringComparison.OrdinalIgnoreCase)),"guild-river-no-woodelf-leak");
                Check(report,visible.All(x=>x.RoomId.Equals(guildAndRiver.Id,StringComparison.OrdinalIgnoreCase)),"guild-river-visible-roomid-filter");
                Check(report,visible.All(x=>x.Metadata.TryGetValue("source_room_reference",out var source)&&source.Equals(guildAndRiver.Id,StringComparison.OrdinalIgnoreCase)),"guild-river-source-room-proof");
                Check(report,visible.All(x=>x.Metadata.TryGetValue("disposition",out var disposition)&&disposition.Equals("neutral",StringComparison.OrdinalIgnoreCase)),"guild-river-neutral-disposition");
            }

            var npc=service.Capture().Npcs.FirstOrDefault();
            if(npc is null)report.Failures.Add("npc-lifecycle-smoke-unavailable");
            else
            {
                var now=DateTimeOffset.UtcNow;
                Check(report,service.ApplyNpcDamage(npc.InstanceId,npc.MaximumHitPoints,now),"npc-death-transition");
                Check(report,service.CorpseCount==1,"npc-corpse-creation");
                Check(report,service.EventsAfter(0).Any(x=>x.Kind=="npc-died")&&service.EventsAfter(0).Any(x=>x.Kind=="corpse-created"),"multiplayer-event-sequencing");
                var due=now.AddMinutes(Math.Max(1,npc.RespawnMinutes));
                var tickEvents=service.Tick(due);
                Check(report,tickEvents.Any(x=>x.StartsWith("npc-respawn:",StringComparison.Ordinal)),"npc-respawn-scheduler");
                Check(report,tickEvents.Any(x=>x.StartsWith("corpse-expired:",StringComparison.Ordinal)),"corpse-cleanup-scheduler");
                Check(report,service.CaptureNpcAi().First(x=>x.InstanceId.Equals(npc.InstanceId,StringComparison.OrdinalIgnoreCase)).Mode=="idle","npc-ai-respawn-integration");
                var restored=new RuntimeLivingWorldService(projectRoot,world,smokeRoot);
                Check(report,restored.Capture().Npcs.Any(x=>x.InstanceId.Equals(npc.InstanceId,StringComparison.OrdinalIgnoreCase)&&x.Alive),"npc-persistence-reconnect");
                Check(report,restored.EventsAfter(0).Count>0,"scheduler-event-restart-recovery");
            }

            Check(report,typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.ExecuteLivingWorldCommand)) is not null,"world-interaction-routing");
            Check(report,typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.CanTraverseLivingWorld)) is not null,"restricted-exit-routing");
            Check(report,typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.TickLivingWorldsAsync)) is not null,"living-world-scheduler-routing");
            Check(report,RuntimeCommand.SupportedCommands.Contains("open")&&RuntimeCommand.SupportedCommands.Contains("worldstate"),"living-world-command-catalog");
            Check(report,service.WorldObjectCount>=report.GeneratedInteractionCount,"generated-world-interactions-active");
        }
        catch(Exception ex){report.Failures.Add(ex.GetType().Name+":"+ex.Message);}
        finally{if(!string.IsNullOrWhiteSpace(smokeRoot)){try{Directory.Delete(smokeRoot,true);}catch{}}}
        return report;
    }

    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
    private static void Check(RuntimeLivingWorldSmokeReport r,bool ok,string name){if(ok)r.Checks.Add(name);else r.Failures.Add(name);}
    private static bool IsClassifiedLivingWorldValidationSignal(string signal) =>
        signal.Equals("spawn-placement-unresolved",StringComparison.OrdinalIgnoreCase)
        || signal.Equals("npc-ai-catalog-invalid",StringComparison.OrdinalIgnoreCase)
        || signal.Equals("status-effect-catalog-invalid",StringComparison.OrdinalIgnoreCase)
        || signal.Equals("loot-rule-catalog-invalid",StringComparison.OrdinalIgnoreCase);
    private static int CountGeneratedRecords(string path)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        if(document.RootElement.ValueKind==JsonValueKind.Array)return document.RootElement.GetArrayLength();
        return document.RootElement.TryGetProperty("records",out var records)&&records.ValueKind==JsonValueKind.Array?records.GetArrayLength():0;
    }
}
