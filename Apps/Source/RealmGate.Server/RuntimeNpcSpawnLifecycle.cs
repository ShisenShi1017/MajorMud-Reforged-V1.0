namespace RealmGate.Server;

public enum RuntimeNpcLifecycleTransitionKind
{
    EnterRespawnWait,
    Respawned,
    RespawnBlocked,
    RespawnSuppressed
}

public sealed record RuntimeNpcSpawnSlotSnapshot(
    string SpawnId,
    string HomeRoomId,
    int MaterializedCapacity,
    int AliveCount,
    int WaitingToRespawnCount,
    int RespawnMinutes,
    string Owner,
    IReadOnlyList<string> InstanceIds);

public sealed record RuntimeNpcLifecycleTransition(
    RuntimeNpcLifecycleTransitionKind Kind,
    string InstanceId,
    string SpawnId,
    string RoomId,
    DateTimeOffset OccurredAtUtc,
    long? WorldTick,
    string Reason,
    string EventCode);

public sealed record RuntimeNpcLifecycleTickResult(
    DateTimeOffset NowUtc,
    long? WorldTick,
    IReadOnlyList<RuntimeNpcLifecycleTransition> Transitions,
    IReadOnlyList<RuntimeNpcSpawnSlotSnapshot> SpawnSlots);

public sealed record RuntimeNpcSpawnSlotControl(
    string SpawnId,
    bool Enabled,
    string Reason,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> InstanceIds);

public sealed record RuntimeNpcSpawnSlotResetResult(
    string SpawnId,
    DateTimeOffset RequestedAtUtc,
    string Reason,
    bool SpawnSlotEnabled,
    IReadOnlyList<string> AffectedInstanceIds,
    IReadOnlyList<RuntimeNpcLifecycleTransition> Transitions);

public sealed record RuntimeNpcRespawnBlock(
    string InstanceId,
    string SpawnId,
    string HomeRoomId,
    string Reason,
    DateTimeOffset BlockedAtUtc,
    long? WorldTick);

/// <summary>
/// Owns evidence-backed runtime spawn slots and the death/wait/respawn boundary.
/// Generated spawn records determine home room; RuntimeMajorMudSpawnTimingCatalog resolves
/// the best-supported timer source/unit. Population chance, native wall-clock admission,
/// native group rolls, room-entry admission, and retry cadence remain separately evidence-gated.
/// </summary>
public sealed partial class RuntimeLivingWorldService
{
    public IReadOnlyList<RuntimeNpcSpawnSlotSnapshot> CaptureNpcSpawnSlots()
    {
        lock (_gate) return CaptureNpcSpawnSlotsLocked();
    }


    public IReadOnlyList<RuntimeNpcSpawnSlotControl> CaptureNpcSpawnSlotControls()
    {
        lock (_gate)
        {
            return _npcs.Values
                .GroupBy(x => x.SpawnId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var ordered = group.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
                    var first = ordered[0];
                    var enabled = !IsSpawnSlotDisabled(first.Metadata);
                    return new RuntimeNpcSpawnSlotControl(
                        group.Key,
                        enabled,
                        ReadMetadata(first.Metadata, "npc_spawn_slot_control_reason", enabled ? "generated-slot-enabled" : "host-policy-disabled"),
                        ReadDateTimeOffset(first.Metadata, "npc_spawn_slot_control_updated_at_utc") ?? DateTimeOffset.MinValue,
                        ordered.Select(x => x.InstanceId).ToArray());
                }).ToArray();
        }
    }

    public bool SetNpcSpawnSlotEnabled(string spawnId, bool enabled, DateTimeOffset nowUtc, string reason)
    {
        if (string.IsNullOrWhiteSpace(spawnId)) return false;
        lock (_gate)
        {
            var sourceSpawn = _world.FindSpawnById(spawnId);
            if (sourceSpawn is not null && UsesNativeClassicGeneration(sourceSpawn))
                return false; // Dynamic native spawns are not fixed slots.
            var members = _npcs.Values
                .Where(x => x.SpawnId.Equals(spawnId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (members.Length == 0) return false;

            foreach (var current in members)
            {
                var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    ["npc_spawn_slot_control_reason"] = NormalizeReason(reason, enabled ? "host-policy-enabled" : "host-policy-disabled"),
                    ["npc_spawn_slot_control_updated_at_utc"] = nowUtc.ToString("O")
                };
                if (enabled)
                {
                    metadata.Remove("npc_spawn_slot_disabled");
                    if (!current.Alive && metadata.ContainsKey("npc_respawn_suppressed_at_utc"))
                    {
                        metadata.Remove("npc_respawn_suppressed_at_utc");
                        metadata["npc_lifecycle_state"] = "waiting_to_respawn";
                        metadata["npc_respawn_scheduled_at_utc"] = nowUtc.ToString("O");
                        _npcs[current.InstanceId] = current with { RespawnAtUtc = nowUtc, Metadata = metadata };
                    }
                    else _npcs[current.InstanceId] = current with { Metadata = metadata };
                }
                else
                {
                    metadata["npc_spawn_slot_disabled"] = "true";
                    if (!current.Alive)
                    {
                        metadata["npc_lifecycle_state"] = "respawn_suppressed";
                        metadata["npc_respawn_suppressed_at_utc"] = nowUtc.ToString("O");
                        metadata.Remove("npc_respawn_scheduled_at_utc");
                        _npcs[current.InstanceId] = current with { RespawnAtUtc = null, Metadata = metadata };
                    }
                    else _npcs[current.InstanceId] = current with { Metadata = metadata };
                }
            }
            PublishLivingWorldEventLocked(enabled ? "npc-spawn-slot-enabled" : "npc-spawn-slot-disabled", spawnId, string.Empty, nowUtc);
            Save();
            SaveLivingWorldCompletionLocked();
            return true;
        }
    }

    public RuntimeNpcSpawnSlotResetResult? ResetNpcSpawnSlot(string spawnId, DateTimeOffset nowUtc, string reason, long? worldTick = null)
    {
        if (string.IsNullOrWhiteSpace(spawnId)) return null;
        if (worldTick is < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
        lock (_gate)
        {
            var sourceSpawn = _world.FindSpawnById(spawnId);
            if (sourceSpawn is not null && UsesNativeClassicGeneration(sourceSpawn))
                return null; // Native generation has no fixed slots to reset.
            var members = _npcs.Values
                .Where(x => x.SpawnId.Equals(spawnId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (members.Length == 0) return null;

            var normalizedReason = NormalizeReason(reason, "host-policy-spawn-slot-reset");
            var enabled = !IsSpawnSlotDisabled(members[0].Metadata);
            var transitions = new List<RuntimeNpcLifecycleTransition>();
            foreach (var original in members)
            {
                if (original.Alive)
                {
                    var transition = EnterNpcRespawnWaitLocked(original.InstanceId, nowUtc, normalizedReason, worldTick);
                    if (transition is not null) transitions.Add(transition);
                    continue;
                }

                var metadata = new Dictionary<string,string>(original.Metadata, StringComparer.OrdinalIgnoreCase);
                ClearTransientNpcExecutionMetadata(metadata);
                ClearRespawnBlockMetadata(metadata);
                metadata["npc_spawn_slot_reset_requested_at_utc"] = nowUtc.ToString("O");
                metadata["npc_spawn_slot_reset_reason"] = normalizedReason;
                if (worldTick is long resetTick) metadata["npc_spawn_slot_reset_world_tick"] = resetTick.ToString();
                else metadata.Remove("npc_spawn_slot_reset_world_tick");

                if (enabled)
                {
                    metadata["npc_lifecycle_state"] = "waiting_to_respawn";
                    metadata["npc_respawn_scheduled_at_utc"] = nowUtc.ToString("O");
                    metadata.Remove("npc_respawn_suppressed_at_utc");
                    _npcs[original.InstanceId] = original with { RespawnAtUtc = nowUtc, Metadata = metadata };
                }
                else
                {
                    metadata["npc_lifecycle_state"] = "respawn_suppressed";
                    metadata["npc_respawn_suppressed_at_utc"] = nowUtc.ToString("O");
                    metadata.Remove("npc_respawn_scheduled_at_utc");
                    _npcs[original.InstanceId] = original with { RespawnAtUtc = null, Metadata = metadata };
                }
            }

            PublishLivingWorldEventLocked("npc-spawn-slot-reset-requested", spawnId, string.Empty, nowUtc);
            Save();
            SaveLivingWorldCompletionLocked();
            return new RuntimeNpcSpawnSlotResetResult(spawnId, nowUtc, normalizedReason, enabled,
                members.Select(x => x.InstanceId).ToArray(), transitions);
        }
    }

    public RuntimeNpcLifecycleTickResult RequestNativeRoomGenerationReset(string roomId, DateTimeOffset nowUtc, long? worldTick = null)
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Room id is required.", nameof(roomId));
        lock (_gate)
        {
            if (_world.Definition.Id.Equals("Classic", StringComparison.OrdinalIgnoreCase))
            {
                // Native SYSOP RESET ROOM changes only the generated-count
                // word; existing monsters remain alive and no generation is
                // invoked as a side effect of the reset command.
                if (_world.FindRoomById(roomId.Trim()) is not null)
                {
                    _nativeRoomGeneratedCounts[roomId.Trim()] = 0;
                    PublishLivingWorldEventLocked("native-sysop-reset-room-counter",
                        roomId.Trim(), roomId.Trim(), nowUtc);
                    Save();
                }
                return new RuntimeNpcLifecycleTickResult(nowUtc, worldTick,
                    Array.Empty<RuntimeNpcLifecycleTransition>(), CaptureNpcSpawnSlotsLocked());
            }

            // Native SYSOP RESET ROOM clears the room's generated-monster count; it does not
            // kill active monsters. Reforged therefore leaves living instances untouched and
            // makes only absent/dead materialized slots immediately generation-eligible.
            foreach (var current in _npcs.Values.Where(npc => !npc.Alive
                         && npc.HomeRoomId.Equals(roomId.Trim(), StringComparison.OrdinalIgnoreCase)
                         && !IsSpawnSlotDisabled(npc.Metadata)).ToArray())
            {
                var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    ["npc_lifecycle_state"] = "waiting_to_respawn",
                    ["npc_respawn_scheduled_at_utc"] = nowUtc.ToString("O"),
                    ["native_sysop_reset_room_requested_at_utc"] = nowUtc.ToString("O")
                };
                _npcs[current.InstanceId] = current with { RespawnAtUtc = nowUtc, Metadata = metadata };
            }
            PublishLivingWorldEventLocked("native-sysop-reset-room-generation", roomId.Trim(), roomId.Trim(), nowUtc);
            var result = TickNpcSpawnLifecycleLocked(nowUtc, worldTick, roomId.Trim());
            Save();
            SaveLivingWorldCompletionLocked();
            return result;
        }
    }

    public RuntimeNpcLifecycleTransition? EnterNpcRespawnWait(string instanceId, DateTimeOffset nowUtc, string reason)
    {
        lock (_gate)
        {
            var transition = EnterNpcRespawnWaitLocked(instanceId, nowUtc, reason, null);
            if (transition is not null)
            {
                Save();
                SaveLivingWorldCompletionLocked();
            }
            return transition;
        }
    }

    public IReadOnlyList<RuntimeNpcRespawnBlock> GetBlockedNpcRespawns()
    {
        lock (_gate)
        {
            return _npcs.Values
                .Where(x => !x.Alive && x.Metadata.ContainsKey("npc_respawn_block_reason"))
                .OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .Select(x => new RuntimeNpcRespawnBlock(
                    x.InstanceId,
                    x.SpawnId,
                    x.HomeRoomId,
                    ReadMetadata(x.Metadata, "npc_respawn_block_reason", "unknown"),
                    ReadDateTimeOffset(x.Metadata, "npc_respawn_blocked_at_utc") ?? DateTimeOffset.MinValue,
                    ReadLongMetadata(x.Metadata, "npc_respawn_blocked_world_tick")))
                .ToArray();
        }
    }

    public bool RetryBlockedNpcRespawn(string instanceId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_npcs.TryGetValue(instanceId, out var current) || current.Alive
                || IsNativeClassicGeneratedNpc(current)
                || !current.Metadata.ContainsKey("npc_respawn_block_reason")) return false;
            if (_world.FindRoomById(current.HomeRoomId) is null) return false;

            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            ClearRespawnBlockMetadata(metadata);
            metadata["npc_lifecycle_state"] = "waiting_to_respawn";
            metadata["npc_respawn_scheduled_at_utc"] = nowUtc.ToString("O");
            metadata["npc_respawn_retry_requested_at_utc"] = nowUtc.ToString("O");
            _npcs[instanceId] = current with { RespawnAtUtc = nowUtc, Metadata = metadata };
            PublishLivingWorldEventLocked("npc-respawn-retry-requested", instanceId, current.HomeRoomId, nowUtc);
            Save();
            SaveLivingWorldCompletionLocked();
            return true;
        }
    }

    private RuntimeNpcLifecycleTransition? EnterNpcRespawnWaitLocked(string instanceId, DateTimeOffset nowUtc, string reason, long? worldTick)
    {
        if (!_npcs.TryGetValue(instanceId, out var current) || !current.Alive) return null;

        if (IsNativeClassicGeneratedNpc(current))
        {
            // Death releases the native room/area counts. The next generation
            // attempt chooses a fresh eligible identity; no NPC respawn slot.
            var nativeMetadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            ClearTransientNpcExecutionMetadata(nativeMetadata);
            ClearRespawnBlockMetadata(nativeMetadata);
            ClearNpcTimedStatuses(nativeMetadata);
            nativeMetadata["npc_lifecycle_state"] = "native-generated-dead";
            nativeMetadata["npc_respawn_block_reason"] = "native-room-generation-replaces-fixed-slot";
            nativeMetadata["npc_last_death_at_utc"] = nowUtc.ToString("O");
            nativeMetadata["npc_last_despawn_reason"] = NormalizeReason(reason, "native-monster-removed");
            var dead = current with { Alive = false, CurrentHitPoints = 0,
                RespawnAtUtc = null, Metadata = nativeMetadata };
            _npcs[instanceId] = dead;
            RecordNativeClassicMonsterRemovalLocked(current, nowUtc);
            // Retain the dead identity only until its corpse expires. A later
            // native generation produces an independent monster instance.
            RecordNpcDeathLocked(current, nowUtc, nowUtc.AddMinutes(5));
            PublishLivingWorldEventLocked("npc-native-monster-removed", instanceId, current.RoomId, nowUtc);
            return CreateTransition(RuntimeNpcLifecycleTransitionKind.EnterRespawnWait,
                dead, dead.RoomId, nowUtc, worldTick,
                "native-monster-removed",
                $"npc-native-monster-removed:{dead.InstanceId}:{dead.SpawnId}");
        }

        var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
        ClearTransientNpcExecutionMetadata(metadata);
        ClearRespawnBlockMetadata(metadata);
        ClearNpcTimedStatuses(metadata);
        var suppressed = IsSpawnSlotDisabled(metadata);
        var due = suppressed ? (DateTimeOffset?)null : ResolveNpcRespawnEligibilityUtc(current, nowUtc);
        metadata["npc_lifecycle_state"] = suppressed ? "respawn_suppressed" : "waiting_to_respawn";
        metadata["npc_lifecycle_owner"] = "world-runtime-spawn-slot";
        metadata["npc_last_despawn_reason"] = NormalizeReason(reason, "runtime-despawn");
        if (due is DateTimeOffset scheduled) metadata["npc_respawn_scheduled_at_utc"] = scheduled.ToString("O");
        else
        {
            metadata.Remove("npc_respawn_scheduled_at_utc");
            metadata["npc_respawn_suppressed_at_utc"] = nowUtc.ToString("O");
        }
        metadata["npc_last_death_at_utc"] = nowUtc.ToString("O");
        if (worldTick is long tick) metadata["npc_last_death_world_tick"] = tick.ToString();
        else metadata.Remove("npc_last_death_world_tick");
        var waiting = current with
        {
            CurrentHitPoints = 0,
            Alive = false,
            RespawnAtUtc = due,
            Metadata = metadata
        };
        _npcs[instanceId] = waiting;
        var transition = CreateTransition(suppressed ? RuntimeNpcLifecycleTransitionKind.RespawnSuppressed : RuntimeNpcLifecycleTransitionKind.EnterRespawnWait, waiting, waiting.RoomId,
            nowUtc, worldTick, metadata["npc_last_despawn_reason"], suppressed
                ? $"npc-respawn-suppressed:{waiting.InstanceId}:{waiting.SpawnId}"
                : $"npc-respawn-wait:{waiting.InstanceId}:{due:O}");
        RecordNpcDeathLocked(current, nowUtc, due ?? nowUtc);
        PublishLivingWorldEventLocked(suppressed ? "npc-respawn-suppressed" : "npc-respawn-wait", waiting.InstanceId, waiting.RoomId, nowUtc);
        return transition;
    }

    public RuntimeNpcLifecycleTickResult TickNpcSpawnLifecycle(DateTimeOffset nowUtc, long? worldTick = null)
    {
        if (worldTick is < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
        lock (_gate)
        {
            // Explicit diagnostic/admin tick admits every due slot. Native gameplay uses
            // two admission paths into the same _generate_monster owner: room entry and the
            // five-second background monster-create scan of active user rooms.
            var result = TickNpcSpawnLifecycleLocked(nowUtc, worldTick, admissionRoomId: null);
            if (result.Transitions.Count > 0)
            {
                Save();
                SaveLivingWorldCompletionLocked();
            }
            return result;
        }
    }

    public RuntimeNpcLifecycleTickResult AdmitDueNpcRespawnsForRoom(string roomId, DateTimeOffset nowUtc, long? worldTick = null)
    {
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Room id is required.", nameof(roomId));
        if (worldTick is < 0) throw new ArgumentOutOfRangeException(nameof(worldTick));
        lock (_gate)
        {
            var result = TickNpcSpawnLifecycleLocked(nowUtc, worldTick, roomId.Trim());
            if (result.Transitions.Count > 0)
            {
                Save();
                SaveLivingWorldCompletionLocked();
            }
            return result;
        }
    }

    private RuntimeNpcLifecycleTickResult TickNpcSpawnLifecycleLocked(DateTimeOffset nowUtc, long? worldTick, string? admissionRoomId = null)
    {
        var transitions = new List<RuntimeNpcLifecycleTransition>();
        foreach (var current in _npcs.Values
                     .Where(x => !x.Alive && x.RespawnAtUtc is not null && x.RespawnAtUtc <= nowUtc
                         && (admissionRoomId is null || x.HomeRoomId.Equals(admissionRoomId, StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(x => x.RespawnAtUtc)
                     .ThenBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                     .ToArray())
        {
            if (IsSpawnSlotDisabled(current.Metadata))
            {
                var suppressedMetadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    ["npc_lifecycle_state"] = "respawn_suppressed",
                    ["npc_respawn_suppressed_at_utc"] = nowUtc.ToString("O")
                };
                suppressedMetadata.Remove("npc_respawn_scheduled_at_utc");
                var suppressed = current with { RespawnAtUtc = null, Metadata = suppressedMetadata };
                _npcs[current.InstanceId] = suppressed;
                transitions.Add(CreateTransition(RuntimeNpcLifecycleTransitionKind.RespawnSuppressed, suppressed, suppressed.HomeRoomId,
                    nowUtc, worldTick, "spawn-slot-disabled", $"npc-respawn-suppressed:{suppressed.InstanceId}:{suppressed.SpawnId}"));
                PublishLivingWorldEventLocked("npc-respawn-suppressed", suppressed.InstanceId, suppressed.HomeRoomId, nowUtc);
                continue;
            }
            if (_world.FindRoomById(current.HomeRoomId) is null)
            {
                if (!current.Metadata.ContainsKey("npc_respawn_block_reason"))
                {
                    var blockedMetadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase)
                    {
                        ["npc_lifecycle_state"] = "respawn_blocked",
                        ["npc_respawn_block_reason"] = "home-room-unresolved",
                        ["npc_respawn_blocked_at_utc"] = nowUtc.ToString("O")
                    };
                    if (worldTick is long blockedTick) blockedMetadata["npc_respawn_blocked_world_tick"] = blockedTick.ToString();
                    var blocked = current with { RespawnAtUtc = null, Metadata = blockedMetadata };
                    _npcs[current.InstanceId] = blocked;
                    transitions.Add(CreateTransition(RuntimeNpcLifecycleTransitionKind.RespawnBlocked, blocked, blocked.HomeRoomId,
                        nowUtc, worldTick, "home-room-unresolved", $"npc-respawn-blocked:{blocked.InstanceId}:home-room-unresolved"));
                    PublishLivingWorldEventLocked("npc-respawn-blocked", blocked.InstanceId, blocked.HomeRoomId, nowUtc);
                }
                continue;
            }

            // Do not replenish a linked-area monster slot while its native area cap is full.
            // Evaluate the origin spawn, not the monster's current room after wandering.
            var originSpawn = _world.FindSpawnById(current.SpawnId);
            if (originSpawn is not null && IsLinkedAreaPopulationAtCapacity(originSpawn))
                continue;

            var metadata = new Dictionary<string,string>(current.Metadata, StringComparer.OrdinalIgnoreCase);
            ClearTransientNpcExecutionMetadata(metadata);
            var generation = metadata.TryGetValue("npc_spawn_generation", out var generationText)
                && int.TryParse(generationText, out var parsedGeneration) ? checked(parsedGeneration + 1) : 1;
            metadata["npc_spawn_generation"] = generation.ToString();
            metadata["npc_lifecycle_state"] = "spawned";
            metadata["npc_lifecycle_owner"] = "world-runtime-spawn-slot";
            metadata["npc_last_respawn_at_utc"] = nowUtc.ToString("O");
            if (worldTick is long tick) metadata["npc_last_respawn_world_tick"] = tick.ToString();
            else metadata.Remove("npc_last_respawn_world_tick");
            metadata.Remove("npc_respawn_scheduled_at_utc");
            metadata.Remove("npc_respawn_retry_requested_at_utc");
            metadata.Remove("npc_respawn_suppressed_at_utc");
            metadata.Remove("npc_last_despawn_reason");
            ClearRespawnBlockMetadata(metadata);

            var respawned = current with
            {
                Alive = true,
                RoomId = current.HomeRoomId,
                CurrentHitPoints = current.MaximumHitPoints,
                RespawnAtUtc = null,
                Metadata = metadata
            };
            _npcs[current.InstanceId] = respawned;
            var transition = CreateTransition(RuntimeNpcLifecycleTransitionKind.Respawned, respawned, respawned.RoomId,
                nowUtc, worldTick, "generated-respawn-delay-elapsed", $"npc-respawn:{current.InstanceId}:{current.HomeRoomId}");
            transitions.Add(transition);
            PublishLivingWorldEventLocked("npc-respawned", current.InstanceId, current.HomeRoomId, nowUtc);
        }
        // Native _generate_monster is invoked on room entry and on the
        // background active-user-room scan. Never resurrect a fixed NPC slot.
        if (admissionRoomId is not null)
            transitions.AddRange(AdmitNativeClassicRoomGenerationLocked(admissionRoomId, nowUtc, worldTick));
        return new(nowUtc, worldTick, transitions, CaptureNpcSpawnSlotsLocked());
    }

    private IReadOnlyList<RuntimeNpcSpawnSlotSnapshot> CaptureNpcSpawnSlotsLocked()
    {
        return _npcs.Values
            .GroupBy(x => x.SpawnId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
                var first = ordered[0];
                return new RuntimeNpcSpawnSlotSnapshot(
                    group.Key,
                    first.HomeRoomId,
                    ordered.Length,
                    ordered.Count(x => x.Alive),
                    ordered.Count(x => !x.Alive && x.RespawnAtUtc is not null),
                    first.RespawnMinutes,
                    "RuntimeLivingWorldService",
                    ordered.Select(x => x.InstanceId).ToArray());
            }).ToArray();
    }

    private static DateTimeOffset ResolveNpcRespawnEligibilityUtc(RuntimeLivingNpcState current, DateTimeOffset nowUtc)
    {
        var mode = ReadMetadata(current.Metadata, "npc_respawn_timer_mode", string.Empty);
        var nativeRoomDelay = mode.Equals("generated-room-delay-zero-immediate", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("generated-lair-delay-native-minutes", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("direct-room-delay-native-minutes", StringComparison.OrdinalIgnoreCase)
            // Upgrade compatibility for snapshots written before CodeUpdate0368.
            || mode.Equals("generated-room-delay-zero", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("generated-lair-delay-minutes", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("unverified-direct-room-delay-minutes", StringComparison.OrdinalIgnoreCase);
        return nativeRoomDelay
            ? RuntimeMajorMudSpawnTimingCatalog.ResolveNativeRoomDelayEligibilityUtc(nowUtc, Math.Max(0, current.RespawnMinutes))
            : nowUtc.AddMinutes(Math.Max(0, current.RespawnMinutes));
    }

    private static RuntimeNpcLifecycleTransition CreateTransition(RuntimeNpcLifecycleTransitionKind kind,
        RuntimeLivingNpcState state, string roomId, DateTimeOffset nowUtc, long? worldTick, string reason, string eventCode) =>
        new(kind, state.InstanceId, state.SpawnId, roomId, nowUtc, worldTick, reason, eventCode);

    private static string NormalizeReason(string? reason, string fallback) =>
        string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();

    private static string ReadMetadata(IReadOnlyDictionary<string,string> metadata, string key, string fallback) =>
        metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static DateTimeOffset? ReadDateTimeOffset(IReadOnlyDictionary<string,string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static long? ReadLongMetadata(IReadOnlyDictionary<string,string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && long.TryParse(value, out var parsed) ? parsed : null;


    private static bool IsSpawnSlotDisabled(IReadOnlyDictionary<string,string> metadata) =>
        metadata.TryGetValue("npc_spawn_slot_disabled", out var value)
        && bool.TryParse(value, out var disabled) && disabled;

    private static void ClearRespawnBlockMetadata(IDictionary<string,string> metadata)
    {
        metadata.Remove("npc_respawn_block_reason");
        metadata.Remove("npc_respawn_blocked_at_utc");
        metadata.Remove("npc_respawn_blocked_world_tick");
    }

    private static void ClearTransientNpcExecutionMetadata(IDictionary<string,string> metadata)
    {
        foreach (var key in new[]
        {
            "current_target_id", "interaction_request", "combat_hostility_state",
            "pending_ai_action_id", "pending_ai_action_kind", "pending_ai_action_target_id",
            "pending_ai_action_request", "pending_ai_action_claim_consumer", "pending_ai_action_claim_id",
            "pending_ai_action_claimed_at_tick", "pending_ai_action_delivery_count",
            "pending_ai_action_last_failure", "pending_ai_action_failed_at_tick", "pending_ai_action_quarantined"
        }) metadata.Remove(key);
        metadata["combat_hostility_state"] = "idle";
    }
}

public sealed class RuntimeNpcSpawnLifecycleSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeNpcSpawnLifecycleSmokeReport InspectContract()
    {
        var report = new RuntimeNpcSpawnLifecycleSmokeReport();
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.CaptureNpcSpawnSlots)) is not null, "spawn-slot-catalog-exposed");
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.EnterNpcRespawnWait)) is not null, "respawn-wait-transition-exposed");
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.TickNpcSpawnLifecycle)) is not null, "respawn-tick-exposed");
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.AdmitDueNpcRespawnsForRoom)) is not null, "room-entry-respawn-admission-exposed");
        Require(report, typeof(RuntimeNpcSpawnSlotSnapshot).GetProperty(nameof(RuntimeNpcSpawnSlotSnapshot.MaterializedCapacity)) is not null, "materialized-capacity-owned");
        Require(report, typeof(RuntimeNpcLifecycleTransition).GetProperty(nameof(RuntimeNpcLifecycleTransition.WorldTick)) is not null, "world-tick-audit-exposed");
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.GetBlockedNpcRespawns)) is not null, "blocked-respawn-catalog-exposed");
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.RetryBlockedNpcRespawn)) is not null, "exact-blocked-respawn-retry-exposed");
        Require(report, typeof(RuntimeNpcRespawnBlock).GetProperty(nameof(RuntimeNpcRespawnBlock.Reason)) is not null, "blocked-respawn-reason-owned");
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.CaptureNpcSpawnSlotControls)) is not null, "spawn-slot-control-catalog-exposed");
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.SetNpcSpawnSlotEnabled)) is not null, "exact-spawn-slot-control-exposed");
        Require(report, typeof(RuntimeNpcSpawnSlotControl).GetProperty(nameof(RuntimeNpcSpawnSlotControl.Enabled)) is not null, "spawn-slot-enabled-state-owned");
        Require(report, typeof(RuntimeLivingWorldService).GetMethod(nameof(RuntimeLivingWorldService.ResetNpcSpawnSlot)) is not null, "exact-spawn-slot-reset-exposed");
        Require(report, typeof(RuntimeNpcSpawnSlotResetResult).GetProperty(nameof(RuntimeNpcSpawnSlotResetResult.AffectedInstanceIds)) is not null, "spawn-slot-reset-result-owned");
        return report;
    }

    private static void Require(RuntimeNpcSpawnLifecycleSmokeReport report, bool condition, string name)
    {
        if (condition) report.Checks.Add(name); else report.Failures.Add(name);
    }
}
