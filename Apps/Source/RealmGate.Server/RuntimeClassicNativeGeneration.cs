namespace RealmGate.Server;

/// <summary>
/// Original WCCMMUD ordinary-monster generation boundary. Native generated
/// monsters are new instances chosen at admission time, never fixed NPC slots
/// that must respawn as the same monster or teleport to an assigned home.
/// Direct/boss/scripted/summoned NPCs retain their separate lifecycle owners.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    // These are native generated-population counters, not current-room occupancy.
    // The original SYSOP RESET commands can zero them without killing monsters.
    // Source-equivalent Borland CRT bounded random draws for the native
    // monster selector. The process-wide interleaving with other host gameplay
    // RNG owners remains a separate parity boundary.
    private readonly RuntimeClassicWorldgroupPrng _nativeSpawnPrng =
        RuntimeClassicGlobalRngContractAuthority.CreateGameplayThreadStream(DateTimeOffset.Now);

    private readonly Dictionary<string, int> _nativeRoomGeneratedCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _nativeAreaPopulationCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _nativeRoomLastKilledUtc = new(StringComparer.OrdinalIgnoreCase);

    private bool UsesNativeClassicGeneration(RuntimeNpcSpawn spawn) =>
        _world.Definition.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase)
        && spawn.HasQualifiedMonsterIndexRange
        && spawn.MonsterGroupId > 0
        && spawn.BossDefinition is null;

    private static bool IsNativeClassicGeneratedNpc(RuntimeLivingNpcState npc) =>
        npc.Metadata.TryGetValue("native_classic_generated_monster", out var raw)
        && raw.Equals("true", StringComparison.OrdinalIgnoreCase);

    internal static bool NativeRoomHasCapacity(int generatedCount, int nativeCap, int occupiedMonsterSlots) =>
        nativeCap > 0 && generatedCount < nativeCap && occupiedMonsterSlots < 15;

    internal static bool NativeMonsterWithinGameLimit(int gameLimit, int existingAlive) =>
        gameLimit <= 0 || existingAlive < gameLimit;

    private int ReadNativeRoomGenerationCount(string roomId) =>
        _nativeRoomGeneratedCounts.TryGetValue(roomId, out var count) ? count : 0;

    private int ReadNativeAreaPopulationCount(string areaKey) =>
        _nativeAreaPopulationCounts.TryGetValue(areaKey, out var count) ? count : 0;

    private RuntimeNpcLifecycleTransition? AdmitNativeClassicMonsterForSpawnLocked(
        RuntimeNpcSpawn spawn, string roomId, DateTimeOffset nowUtc, long? worldTick)
    {
        if (!UsesNativeClassicGeneration(spawn) || spawn.GroupDefinitions.Count == 0)
            return null;
        if (_world.FindRoomById(roomId) is null)
            return null;

        // Native room +0x606 generated count is limited by byte +0x55C.
        // A zero cap means no ordinary generated monster is admitted.
        // WCCMMUD room +0x400 holds 15 active monster slots as a
        // second independent admission check.
        var occupiedMonsterSlots = _npcs.Values.Count(npc => npc.Alive
            && npc.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase));
        if (!NativeRoomHasCapacity(ReadNativeRoomGenerationCount(roomId),
                spawn.NativeRoomGenerationCap, occupiedMonsterSlots))
            return null;

        // Use the designated native area controller, never a guessed minimum
        // across rooms. An unresolved native controller is not invented here.
        if (spawn.HasLinkedPopulationArea && spawn.NativeAreaControllerResolved
            && ReadNativeAreaPopulationCount(spawn.PopulationAreaKey) >= spawn.NativeAreaControllerCap)
            return null;

        if (spawn.RespawnMinutes > 0
            && _nativeRoomLastKilledUtc.TryGetValue(roomId, out var lastKilled)
            && nowUtc < RuntimeMajorMudSpawnTimingCatalog.ResolveNativeRoomDelayEligibilityUtc(
                lastKilled, spawn.RespawnMinutes))
            return null;

        // Selection is performed before the individual monster game-limit
        // gate, as in WCCMMUD!_generate_monster. Never reroll a rejected
        // limited monster as an invented alternative.
        var selected = SelectNativeIndexedLairCandidate(
            spawn.GroupDefinitions, () => _nativeSpawnPrng.Genrdn(1, 100));
        if (selected is null)
            return null;
        var livingSameType = _npcs.Values.Count(npc => npc.Alive
            && npc.NpcId.Equals(selected.Id, StringComparison.OrdinalIgnoreCase));
        if (!NativeMonsterWithinGameLimit(selected.GameLimit, livingSameType))
            return null;

        var ai = _world.NpcAiCatalog.FindBySpawnId(spawn.Id);
        if (ai is null)
            throw new InvalidOperationException($"No generated NPC AI relationship for native spawn '{spawn.Id}'.");

        // Fresh identity every generation. HomeRoomId is retained solely as
        // the legacy record's originating-room bookkeeping reference; ordinary
        // monsters neither return to it nor respawn at it.
        var instanceId = $"native:{Guid.NewGuid():N}";
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["native_classic_generated_monster"] = "true",
            ["native_classic_origin_room"] = roomId,
            ["native_classic_area_key"] = spawn.PopulationAreaKey,
            ["source_room_reference"] = FormatSourceRoomReference(spawn),
            ["resolved_runtime_room_id"] = roomId,
            ["source_map_id"] = spawn.SourceMapId.ToString(),
            ["source_room_number"] = spawn.SourceRoomNumber.ToString(),
            ["alignment"] = selected.Alignment,
            ["disposition"] = ResolveDisposition(selected, spawn),
            ["combat_hostility_state"] = "idle",
            ["npc_ai_authority"] = "generated:spawns.json:native-classic",
            ["npc_ai_schema_version"] = _world.NpcAiCatalog.SchemaVersion,
            ["npc_ai_room_reference"] = ai.RoomReference,
            ["npc_ai_monster_group_id"] = ai.MonsterGroupId.ToString(),
            ["npc_ai_respawn_minutes"] = ai.RespawnMinutes.ToString(),
            ["npc_ai_interaction_model"] = ai.InteractionModel,
            ["npc_respawn_timer_mode"] = "native-room-generation",
            ["npc_respawn_timer_effective_minutes"] = spawn.RespawnMinutes.ToString(),
            ["npc_lifecycle_owner"] = "classic-native-room-generation",
            ["npc_lifecycle_state"] = "spawned",
            ["see_hidden"] = selected.SeeHidden ? "1" : "0",
            ["animal"] = selected.Animal ? "1" : "0",
            ["charm_level"] = selected.CharmLevel.ToString(),
            ["charm_resistance"] = selected.CharmResistance.ToString(),
            ["follow_value"] = selected.FollowValue.ToString(),
            ["regen_time"] = selected.RegenTime.ToString(),
            ["hp_regen"] = selected.HitPointRegen.ToString(),
            ["respawn_hours"] = selected.RespawnHours.ToString(),
            ["undead"] = selected.Undead ? "1" : "0",
            ["non_living"] = selected.NonLiving ? "1" : "0",
            ["negated_ability_ids"] = string.Join(",", selected.NegatedAbilityIds),
            ["npc_legacy_id"] = selected.LegacyId.ToString(),
            ["guarded_npc_legacy_ids"] = string.Join(",", selected.GuardedNpcLegacyIds),
            ["resist_water"] = selected.WaterResistance.ToString(),
            ["spell_immunity_level"] = selected.SpellImmunityLevel.ToString(),
            ["non_attack_item_legacy_id"] = selected.NonAttackItemLegacyId.ToString(),
            ["game_limit"] = selected.GameLimit.ToString(),
            ["gender"] = selected.Gender.ToString(),
            ["monster_type"] = selected.MonsterType.ToString(),
            ["experience_multiplier"] = selected.ExperienceMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["classic_active_slot"] = NextClassicActiveSlotLocked().ToString()
        };

        var hp = Math.Max(1, selected.HitPoints);
        var state = new RuntimeLivingNpcState(instanceId, spawn.Id, selected.Id,
            selected.Name, roomId, roomId, hp, hp, true, null,
            ai.InteractionModel, spawn.RespawnMinutes, metadata);
        _npcs.Add(instanceId, state);
        _npcAi[instanceId] = new RuntimeNpcAiState(
            instanceId, "idle", null, roomId, roomId, 0, nowUtc);

        _nativeRoomGeneratedCounts[roomId] = checked(ReadNativeRoomGenerationCount(roomId) + 1);
        if (spawn.HasLinkedPopulationArea && spawn.NativeAreaControllerResolved)
            _nativeAreaPopulationCounts[spawn.PopulationAreaKey] =
                checked(ReadNativeAreaPopulationCount(spawn.PopulationAreaKey) + 1);

        PublishLivingWorldEventLocked("npc-native-generated", instanceId, roomId, nowUtc);
        return new RuntimeNpcLifecycleTransition(RuntimeNpcLifecycleTransitionKind.Respawned,
            instanceId, spawn.Id, roomId, nowUtc, worldTick,
            "native-room-generated-new-identity",
            $"npc-native-generated:{instanceId}:{spawn.Id}");
    }

    private IReadOnlyList<RuntimeNpcLifecycleTransition> AdmitNativeClassicRoomGenerationLocked(
        string roomId, DateTimeOffset nowUtc, long? worldTick)
    {
        if (!_world.Definition.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase)
            || !_world.SpawnsByRoom.TryGetValue(roomId, out var spawns))
            return Array.Empty<RuntimeNpcLifecycleTransition>();

        var admitted = new List<RuntimeNpcLifecycleTransition>();
        foreach (var spawn in spawns)
        {
            var transition = AdmitNativeClassicMonsterForSpawnLocked(spawn, roomId, nowUtc, worldTick);
            if (transition is not null)
                admitted.Add(transition);
        }
        return admitted;
    }

    private void RecordNativeClassicMonsterRemovalLocked(RuntimeLivingNpcState npc, DateTimeOffset nowUtc)
    {
        if (!IsNativeClassicGeneratedNpc(npc)) return;
        // Native removal uses the generating room, not the NPC's wandering
        // destination, for the counter and Last-Killed mutation.
        var roomId = npc.Metadata.TryGetValue("native_classic_origin_room", out var origin)
            && !string.IsNullOrWhiteSpace(origin) ? origin : npc.HomeRoomId;
        _nativeRoomGeneratedCounts[roomId] = Math.Max(0, ReadNativeRoomGenerationCount(roomId) - 1);
        _nativeRoomLastKilledUtc[roomId] = nowUtc;
        var spawn = _world.FindSpawnById(npc.SpawnId);
        if (spawn is not null && spawn.HasLinkedPopulationArea && spawn.NativeAreaControllerResolved)
            _nativeAreaPopulationCounts[spawn.PopulationAreaKey] =
                Math.Max(0, ReadNativeAreaPopulationCount(spawn.PopulationAreaKey) - 1);
    }

    /// <summary>
    /// Native SYSOP RESET ROOM zeros the generated-room count and does not kill
    /// existing monsters or immediately trigger generation.
    /// </summary>
    public bool ResetNativeClassicRoomPopulation(string roomId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_world.Definition.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase)
                || _world.FindRoomById(roomId) is null) return false;
            _nativeRoomGeneratedCounts[roomId] = 0;
            PublishLivingWorldEventLocked("native-sysop-reset-room-counter", roomId, roomId, nowUtc);
            Save();
            return true;
        }
    }

    /// <summary>
    /// Native SYSOP RESET AREA zeros the designated controller population
    /// counter without resetting each linked room's count or killing monsters.
    /// </summary>
    public bool ResetNativeClassicAreaPopulation(string roomId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_world.Definition.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase))
                return false;
            var room = _world.FindRoomById(roomId);
            if (room is null) return false;

            // A SYSOP can stand in a non-generating room within an area.
            // Resolve its native controller by map/linked-area identity even
            // when this particular room has no generated spawn record.
            var anchor = room.EnvironmentLinkedSpawnRoom;
            if (anchor <= 0) return false;
            var spawn = _world.Spawns.FirstOrDefault(x =>
                x.SourceMapId == room.MapId
                && x.LinkedSpawnRoomNumber == anchor
                && x.NativeAreaControllerResolved);
            if (spawn is null) return false;
            _nativeAreaPopulationCounts[spawn.PopulationAreaKey] = 0;
            PublishLivingWorldEventLocked("native-sysop-reset-area-counter",
                spawn.PopulationAreaKey, roomId, nowUtc);
            Save();
            return true;
        }
    }

    private void PruneOrphanedNativeDeadMonstersAfterRestore()
    {
        // If a crash left a dead dynamic NPC without a persisted corpse,
        // never retain an immortal dead slot. The original native game
        // regenerates from room admission instead.
        foreach (var npc in _npcs.Values
            .Where(npc => !npc.Alive && IsNativeClassicGeneratedNpc(npc)
                && !_corpses.ContainsKey("corpse:" + npc.InstanceId)).ToArray())
        {
            _npcs.Remove(npc.InstanceId);
            _npcAi.Remove(npc.InstanceId);
        }
    }

    private void RebuildNativeCountersFromSurvivorsLocked()
    {
        _nativeRoomGeneratedCounts.Clear();
        _nativeAreaPopulationCounts.Clear();
        foreach (var npc in _npcs.Values.Where(npc => npc.Alive && IsNativeClassicGeneratedNpc(npc)))
        {
            var origin = npc.Metadata.TryGetValue("native_classic_origin_room", out var value)
                && !string.IsNullOrWhiteSpace(value) ? value : npc.HomeRoomId;
            _nativeRoomGeneratedCounts[origin] = checked(ReadNativeRoomGenerationCount(origin) + 1);
            var spawn = _world.FindSpawnById(npc.SpawnId);
            if (spawn is not null && spawn.HasLinkedPopulationArea && spawn.NativeAreaControllerResolved)
                _nativeAreaPopulationCounts[spawn.PopulationAreaKey] =
                    checked(ReadNativeAreaPopulationCount(spawn.PopulationAreaKey) + 1);
        }
    }
}
