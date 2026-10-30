namespace RealmGate.Server;

public static class RuntimeCharacterDeathMarkers
{
    public const string State = "character.death.state";
    public const string EventId = "character.death.event_id";
    public const string CorpseId = "character.death.corpse_id";
    public const string RoomId = "character.death.room_id";
    public const string OccurredAtUtc = "character.death.occurred_at_utc";
    public const string GoldEscrow = "character.death.gold_escrow";
    public const string DroppedRootItems = "character.death.dropped_root_items";
    public const string FloorPlacedRootItems = "character.death.floor_placed_root_items";
    public const string FloorLostRootItems = "character.death.floor_lost_root_items";
    public const string RetainedRootItems = "character.death.retained_root_items";
    public const string Evidence = "character.death.evidence";
    public const string RespawnedAtUtc = "character.death.respawned_at_utc";
    public const string RespawnRoomId = "character.death.respawn_room_id";
    public const string RecoveredAtUtc = "character.death.recovered_at_utc";
    public const string RecoveredRootItems = "character.death.recovered_root_items";
    public const string RecoveredGold = "character.death.recovered_gold";
    public const string ExperienceBefore = "character.death.experience_before";
    public const string ExperienceAfter = "character.death.experience_after";
    public const string ExperienceLost = "character.death.experience_lost";
    public const string ExperiencePolicy = "character.death.experience_policy";
}

public sealed record RuntimeCharacterDeathTransition(
    RuntimeCharacterRecord Character,
    RuntimeCorpseState Corpse,
    int DroppedRootItems,
    int RetainedRootItems,
    long GoldEscrow,
    string EventId,
    DateTimeOffset OccurredAtUtc);

public sealed record RuntimeDecodedDeathTextEvidence(
    string ItemId,
    int ReferencedTextblockId,
    bool PayloadResolved,
    bool Executable,
    string Classification);

public static class RuntimeDecodedDeathTextAuthority
{
    public static IReadOnlyList<RuntimeDecodedDeathTextEvidence> Resolve(
        RuntimeCharacterRecord character, RuntimeItemCatalog items, RuntimeClassicNativeTextblockPayloadAuthority native)
    {
        var ownedIds = (character.ItemState?.Items ?? Array.Empty<RuntimeItemInstance>())
            .Where(item => item.OwnerId?.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => item.CanonicalItemId).Distinct(StringComparer.OrdinalIgnoreCase);
        return ownedIds.Select(items.Require)
            .Where(item => item.EquipmentAbilityModifiers.TryGetValue(155, out _))
            .Select(item =>
            {
                var target = item.EquipmentAbilityModifiers[155];
                var payload = string.Empty;
                var resolved = target > 0 && native.TryResolveTextblock(target, out payload);
                var executable = resolved && IsDeathPayload(payload);
                return new RuntimeDecodedDeathTextEvidence(item.Id, target, resolved, executable,
                    executable ? "native_death_payload" : resolved
                        ? "resolved_payload_semantic_mismatch_quarantined"
                        : "unresolved_target_quarantined");
            }).ToArray();
    }

    internal static bool IsDeathPayload(string payload) =>
        payload.Contains("die", StringComparison.OrdinalIgnoreCase)
        || payload.Contains("death", StringComparison.OrdinalIgnoreCase)
        || payload.Contains("killed", StringComparison.OrdinalIgnoreCase)
        || payload.Contains("slain", StringComparison.OrdinalIgnoreCase);
}

public static class RuntimeCharacterDeathResolver
{
    public const string EvidenceClassification = "STRONGLY CORRELATED";
    public const string DeadState = "dead_with_recoverable_corpse";
    public const string LegacyRespawnedState = "respawned_corpse_pending_or_recovered";
    public const string RespawnedState = "respawned_corpse_pending";
    public const string RecoveredState = "respawned_corpse_recovered";

    public static RuntimeCharacterDeathTransition Defeat(
        RuntimeCharacterRecord character,
        RuntimeItemCatalog catalog,
        string roomId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Death room id is required.", nameof(roomId));
        if (RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Dead) || character.HitPoints == 0)
            throw new InvalidOperationException("Character death transition has already been applied.");

        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var gold = RuntimeCharacterGold.Validate(state.Gold);
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
        var rootsBefore = engine.Items
            .Where(item => string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase)
                && !item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var retainedIds = rootsBefore
            .Where(item => catalog.Require(item.CanonicalItemId).RetainedOnDeath)
            .Select(item => item.InstanceId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var corpse = engine.CreateCorpse(character.CharacterId, roomId, gold);
        var dropped = rootsBefore.Length - retainedIds.Count;
        var snapshot = engine.Snapshot(0, RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(
            engine.Items, character.CharacterId, state.ActiveItemState));
        var eventId = "death." + Guid.NewGuid().ToString("N");
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var currentLives = RuntimeClassicCharacterProgressionFormula.ReadLives(character);
        if (currentLives <= 0) throw new InvalidOperationException("Character has no lives remaining.");
        var ordinaryDeathExperience = currentLives > 1
            ? RuntimeClassicDeathExperienceAuthority.PreserveOrdinaryDeathExperience(character.Experience, currentLives)
            : null;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeCharacterDeathMarkers.State] = DeadState,
            [RuntimeCharacterDeathMarkers.EventId] = eventId,
            [RuntimeCharacterDeathMarkers.CorpseId] = corpse.CorpseId,
            [RuntimeCharacterDeathMarkers.RoomId] = roomId,
            [RuntimeCharacterDeathMarkers.OccurredAtUtc] = nowUtc.ToString("O"),
            [RuntimeCharacterDeathMarkers.GoldEscrow] = gold.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [RuntimeCharacterDeathMarkers.DroppedRootItems] = dropped.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [RuntimeCharacterDeathMarkers.RetainedRootItems] = retainedIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [RuntimeCharacterDeathMarkers.Evidence] = EvidenceClassification
        };
        if (ordinaryDeathExperience is not null)
        {
            markers[RuntimeCharacterDeathMarkers.ExperienceBefore] = ordinaryDeathExperience.ExperienceBefore.ToString(System.Globalization.CultureInfo.InvariantCulture);
            markers[RuntimeCharacterDeathMarkers.ExperienceAfter] = ordinaryDeathExperience.ExperienceAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
            markers[RuntimeCharacterDeathMarkers.ExperienceLost] = ordinaryDeathExperience.ExperienceLost.ToString(System.Globalization.CultureInfo.InvariantCulture);
            markers[RuntimeCharacterDeathMarkers.ExperiencePolicy] = ordinaryDeathExperience.Classification;
        }
        else
        {
            markers.Remove(RuntimeCharacterDeathMarkers.ExperienceBefore);
            markers.Remove(RuntimeCharacterDeathMarkers.ExperienceAfter);
            markers.Remove(RuntimeCharacterDeathMarkers.ExperienceLost);
            markers.Remove(RuntimeCharacterDeathMarkers.ExperiencePolicy);
        }
        markers.Remove(RuntimeCharacterDeathMarkers.RespawnedAtUtc);
        markers.Remove(RuntimeCharacterDeathMarkers.RespawnRoomId);
        markers.Remove(RuntimeCharacterDeathMarkers.RecoveredAtUtc);
        markers.Remove(RuntimeCharacterDeathMarkers.RecoveredRootItems);
        markers.Remove(RuntimeCharacterDeathMarkers.RecoveredGold);

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        conditions["lives"] = Math.Max(0, currentLives - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var defeated = RuntimeCharacterFlagResolver.Set(
            character with
            {
                HitPoints = 0,
                ItemState = snapshot,
                Conditions = conditions,
                SemanticState = semantic with { RuntimeMarkers = markers },
                UpdatedAtUtc = nowUtc
            },
            RuntimeCharacterFlagIds.Dead,
            "runtime.character.death.defeat");

        Validate(defeated);
        return new RuntimeCharacterDeathTransition(defeated, corpse, dropped, retainedIds.Count, gold, eventId, nowUtc);
    }

    public static RuntimeCharacterRecord Respawn(RuntimeCharacterRecord character, string roomId, int roomNumber, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (!RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Dead) || character.HitPoints != 0)
            throw new InvalidOperationException("Only a dead character can respawn.");
        if (RuntimeClassicCharacterProgressionFormula.ReadLives(character) <= 0)
            throw new InvalidOperationException("This character has no lives remaining and cannot respawn.");
        if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Respawn room id is required.", nameof(roomId));

        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.EventId);
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.CorpseId);
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeCharacterDeathMarkers.State] = RespawnedState,
            [RuntimeCharacterDeathMarkers.RespawnedAtUtc] = nowUtc.ToString("O"),
            [RuntimeCharacterDeathMarkers.RespawnRoomId] = roomId,
            [RuntimeCharacterDeathMarkers.Evidence] = EvidenceClassification
        };
        var updated = RuntimeCharacterFlagResolver.Clear(
            character with
            {
                HitPoints = character.MaximumHitPoints,
                RoomId = roomId,
                RoomNumber = roomNumber,
                SemanticState = semantic with { RuntimeMarkers = markers },
                UpdatedAtUtc = nowUtc
            },
            RuntimeCharacterFlagIds.Dead,
            "runtime.character.death.respawn");
        Validate(updated);
        return updated;
    }

    public static RuntimeCharacterRecord RecoverCorpse(
        RuntimeCharacterRecord character,
        RuntimeItemCatalog catalog,
        string currentRoomId,
        DateTimeOffset nowUtc,
        out RuntimeCorpseRecoveryResult recovery)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(catalog);
        if (RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Dead) || character.HitPoints == 0)
            throw new InvalidOperationException("A dead character cannot recover a corpse before respawning.");
        if (string.IsNullOrWhiteSpace(currentRoomId)) throw new ArgumentException("Current room id is required.", nameof(currentRoomId));
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var stateMarker = RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.State);
        if (stateMarker is not (RespawnedState or LegacyRespawnedState)) throw new InvalidOperationException("The active death event has no pending recoverable corpse.");
        var corpseId = RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.CorpseId);
        var deathRoomId = RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.RoomId);
        if (!deathRoomId.Equals(currentRoomId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The active corpse is not in the current room.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
        recovery = engine.RecoverOwnedCorpse(corpseId, character.CharacterId);
        var gold = RuntimeCharacterGold.Credit(state.Gold, recovery.GoldRecovered);
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeCharacterDeathMarkers.State] = RecoveredState,
            [RuntimeCharacterDeathMarkers.RecoveredAtUtc] = nowUtc.ToString("O"),
            [RuntimeCharacterDeathMarkers.RecoveredRootItems] = recovery.RootItemsRecovered.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [RuntimeCharacterDeathMarkers.RecoveredGold] = recovery.GoldRecovered.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [RuntimeCharacterDeathMarkers.Evidence] = EvidenceClassification
        };
        var updated = character with
        {
            ItemState = engine.Snapshot(gold, RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(
                engine.Items, character.CharacterId, state.ActiveItemState)),
            SemanticState = semantic with { RuntimeMarkers = markers },
            UpdatedAtUtc = nowUtc
        };
        Validate(updated);
        return updated;
    }

    public static void Validate(RuntimeCharacterRecord character)
    {
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var dead = RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Dead);
        if (dead != (character.HitPoints == 0))
            throw new InvalidDataException("Character dead flag and zero-hit-point state diverge.");
        if (!semantic.RuntimeMarkers.TryGetValue(RuntimeCharacterDeathMarkers.State, out var state)) return;
        if (state is not (DeadState or LegacyRespawnedState or RespawnedState or RecoveredState)) throw new InvalidDataException("Character death state marker is invalid.");
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.EventId);
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.CorpseId);
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.RoomId);
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.OccurredAtUtc);
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.GoldEscrow);
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.DroppedRootItems);
        RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.RetainedRootItems);
        if (!semantic.RuntimeMarkers.TryGetValue(RuntimeCharacterDeathMarkers.Evidence, out var evidence) || evidence != EvidenceClassification)
            throw new InvalidDataException("Character death evidence marker is missing or invalid.");
        if (state == DeadState && !dead) throw new InvalidDataException("Dead death-state marker requires the dead flag.");
        if (state is LegacyRespawnedState or RespawnedState or RecoveredState)
        {
            if (dead) throw new InvalidDataException("Respawned death-state marker cannot retain the dead flag.");
            RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.RespawnedAtUtc);
            RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.RespawnRoomId);
        }
        if (state == RecoveredState)
        {
            RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.RecoveredAtUtc);
            RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.RecoveredRootItems);
            RequireDeathMarker(semantic, RuntimeCharacterDeathMarkers.RecoveredGold);
        }
    }

    private static string RequireDeathMarker(RuntimeCharacterSemanticState semantic, string key)
    {
        if (!semantic.RuntimeMarkers.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Character death marker '{key}' is missing.");
        return value;
    }
}
