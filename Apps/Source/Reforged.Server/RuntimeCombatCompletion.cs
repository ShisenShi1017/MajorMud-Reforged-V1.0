using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public const int ClassicCombatRoundMilliseconds = 5000;
    public const int ClassicSpellSystemRoundMilliseconds = 3000;

    public async Task TickLiveStatusesAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var statusWorlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var activeSession in _sessions.ActiveSessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (statusWorlds.Add(activeSession.World.Definition.Id))
                TickClassicLiveStatuses(activeSession, nowUtc);
        }
        await Task.CompletedTask;
    }

    public async Task TickLiveCombatsAsync(CancellationToken cancellationToken)
    {
        PruneInactiveCombatOwnership();
        foreach (var session in _sessions.ActiveSessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_liveCombats.ContainsKey(session.Id)) continue;
            try
            {
                var result = ExecuteLiveCombatRound(session);
                if (!string.IsNullOrWhiteSpace(result)) session.EnqueueMessage(result);
            }
            catch (Exception ex)
            {
                session.EnqueueMessage($"Combat tick blocked: {ex.Message}");
            }
        }
        foreach (var encounter in _livePvpEncounters.Values.OrderBy(value => value.EncounterId, StringComparer.OrdinalIgnoreCase).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = ExecuteLivePvpRound(encounter);
                var attacker = _sessions.ActiveSessions.FirstOrDefault(session => session.Id.Equals(encounter.AttackerSessionId, StringComparison.OrdinalIgnoreCase));
                if (attacker is not null && !string.IsNullOrWhiteSpace(result)) attacker.EnqueueMessage(result);
            }
            catch (Exception ex)
            {
                EndLivePvpEncounter(encounter);
                foreach (var participant in _sessions.ActiveSessions.Where(session => session.Id.Equals(encounter.AttackerSessionId, StringComparison.OrdinalIgnoreCase) || session.Id.Equals(encounter.DefenderSessionId, StringComparison.OrdinalIgnoreCase)))
                    participant.EnqueueMessage($"PvP combat tick blocked: {ex.Message}\n*Combat Off*");
            }
        }
        await Task.CompletedTask;
    }

    public string RespawnCharacter(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        if (!RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Dead) || character.HitPoints != 0) return "You are not dead.";
        if (RuntimeClassicCharacterProgressionFormula.ReadLives(character) <= 0)
            return "You have no lives remaining. This character is permanently dead.";

        var alignment = character.SemanticState?.Alignment;
        var reputation = alignment?.Reputation
            ?? (character.Conditions.TryGetValue("alignment", out var alignmentText) ? alignmentText : string.Empty);
        var evilPoints = alignment?.EvilPoints
            ?? (character.Conditions.TryGetValue("evil_points", out var rawEvil) && int.TryParse(rawEvil, out var parsedEvil) ? parsedEvil : 0);
        var outlawOrWorse = evilPoints >= 40 || reputation.Equals("Outlaw", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Criminal", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Villain", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Villian", StringComparison.OrdinalIgnoreCase)
            || reputation.Equals("Fiend", StringComparison.OrdinalIgnoreCase);
        var roomNumber = outlawOrWorse ? 142 : 2189;
        var room = session.World.FindRoom(1, roomNumber)
            ?? throw new InvalidOperationException($"Decoded Classic death-respawn room 1/{roomNumber} is unavailable.");
        var updated = RuntimeCharacterDeathResolver.Respawn(character, room.Id, room.RoomNumber, DateTimeOffset.UtcNow);
        _characters.SaveCharacter(updated);
        session.MoveToWorld(session.World, room, "combat-respawn");
        _sessions.BroadcastRoom(session, $"{actor.DisplayName} returns to life.", includeSource: false);
        return $"You return to life at {room.Name} with {updated.HitPoints}/{updated.MaximumHitPoints} HP.";
    }

    public string RecoverCorpse(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        if (RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Dead)) return "You must respawn before recovering your corpse.";
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        try
        {
            var updated = RuntimeCharacterDeathResolver.RecoverCorpse(character, catalog, session.CurrentRoom.Id, DateTimeOffset.UtcNow, out var recovery);
            _characters.SaveCharacter(updated);
            _sessions.BroadcastRoom(session, $"{actor.DisplayName} recovers a corpse.", includeSource: false);
            return $"You recover {recovery.RootItemsRecovered} item(s) and {recovery.GoldRecovered} gold from your corpse.";
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not in the current room", StringComparison.OrdinalIgnoreCase))
        {
            return "Your corpse is not here.";
        }
    }

    private string CompleteNpcDefeat(RuntimeSession session, RuntimeLiveCombatEncounter defeatedEncounter, RuntimeCharacterRecord before, RuntimeNpcDefinition npc, int playerHp)
    {
        // Native runtime observation shows monster XP is shared by characters who are actively
        // fighting that exact monster at the instant it dies, not by party membership. Snapshot
        // the eligible combatants before any encounter is removed.
        var experienceParticipants = ResolveNpcExperienceParticipants(session, defeatedEncounter);
        var state = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());

        // Decoded monster currency fields are denomination amounts, not a drop percentage.
        // Preserve the authored values exactly and never fabricate money for zero-value NPCs.
        var currencyText = RuntimeClassicRoomCurrencyStore.AddExact(
            _characters.ProjectRoot,
            before.WorldId,
            session.CurrentRoom.Id,
            npc.RunicReward,
            npc.PlatinumReward,
            npc.GoldReward,
            npc.SilverReward,
            npc.CopperReward);

        var dropped = new List<string>();
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, before.WorldId);
        var movedItems = state.Items
            .Where(item => !RuntimeClassicRoomCurrencyStore.IsLegacyCurrency(item))
            .Select(item =>
            {
                var belongsToNpc = string.Equals(item.LocationKind, RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(item.LocationId, npc.Id, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.OwnerId, npc.Id, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.OwnerId, "npc:" + npc.Id, StringComparison.OrdinalIgnoreCase));
                if (!belongsToNpc) return item;
                try { dropped.Add(catalog.Require(item.CanonicalItemId).Name); } catch { dropped.Add(item.CanonicalItemId); }
                return item with
                {
                    OwnerId = null,
                    LocationKind = RuntimeItemLocations.Room,
                    LocationId = session.CurrentRoom.Id,
                    EquippedSlot = null
                };
            }).ToList();

        foreach (var loot in npc.Loot.OrderBy(x => x.ItemLegacyId))
        {
            RuntimeItemDefinition definition;
            try { definition = catalog.Require(loot.CanonicalItemId); }
            catch
            {
                // Decoded relationship has no canonical generated item. Quarantine it rather than
                // inventing an item or crashing the authoritative death/loot transaction.
                continue;
            }
            var roll = RollNpcLootPercent(before.CharacterId, npc.Id, loot.CanonicalItemId, before.Experience, session.CurrentRoom.Id);
            if (!session.World.LootRuleCatalog.ShouldDrop(loot.DropPercent, roll)) continue;
            movedItems.Add(new RuntimeItemInstance(
                $"npc-loot:{npc.LegacyId}:{Guid.NewGuid():N}",
                loot.CanonicalItemId,
                null,
                RuntimeItemLocations.Room,
                session.CurrentRoom.Id,
                null,
                Math.Max(0, definition.DefaultCharges),
                1,
                null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["npc_source_id"] = npc.Id,
                    ["npc_source_legacy_id"] = npc.LegacyId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["loot_drop_percent"] = loot.DropPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["loot_roll"] = roll.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["loot_credit_character_id"] = before.CharacterId,
                    ["loot_authority"] = "decoded-public_reference_0139.loot_named"
                }));
            dropped.Add(definition.Name);
        }

        var attribution = new Dictionary<string, string>(before.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            ["combat.last_kill_credit_character_id"] = before.CharacterId,
            ["combat.last_kill_npc_id"] = npc.Id,
            ["combat.last_kill_npc_legacy_id"] = npc.LegacyId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["combat.last_loot_attribution_character_id"] = before.CharacterId,
            ["combat.last_kill_at_utc"] = DateTimeOffset.UtcNow.ToString("O")
        };
        var updated = before with
        {
            HitPoints = playerHp,
            Conditions = attribution,
            ItemState = state with { Items = movedItems.ToArray() },
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _characters.SaveCharacter(updated);

        var distribution = DistributeNpcDefeatExperience(session, defeatedEncounter, npc, experienceParticipants);
        var awardedXp = distribution.Awards.GetValueOrDefault(before.CharacterId);
        var moneyStatus = string.IsNullOrWhiteSpace(currencyText) ? string.Empty : $" {currencyText} fall to the ground.";
        var lootStatus = dropped.Count == 0 ? string.Empty : " It also drops " + string.Join(", ", dropped) + ".";
        return $"You gain {awardedXp} experience.\n*Combat Off*{moneyStatus}{lootStatus}";
    }

    internal static (long Share, long Remainder) SplitClassicNpcExperience(long totalExperience, int activeCombatants)
    {
        if (totalExperience < 0) throw new ArgumentOutOfRangeException(nameof(totalExperience));
        if (activeCombatants <= 0) throw new ArgumentOutOfRangeException(nameof(activeCombatants));
        return (totalExperience / activeCombatants, totalExperience % activeCombatants);
    }

    private RuntimeSession[] ResolveNpcExperienceParticipants(RuntimeSession source, RuntimeLiveCombatEncounter defeatedEncounter)
    {
        var participants = _sessions.ActiveSessions
            .Where(candidate => candidate.Actor is RuntimeCharacterActor)
            .Where(candidate => candidate.World.Definition.Id.Equals(defeatedEncounter.WorldId, StringComparison.OrdinalIgnoreCase))
            .Where(candidate => candidate.CurrentRoom.Id.Equals(defeatedEncounter.RoomId, StringComparison.OrdinalIgnoreCase))
            .Where(candidate => _liveCombats.TryGetValue(candidate.Id, out var active)
                && active.NpcInstanceId.Equals(defeatedEncounter.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // The killing session is necessarily an active combatant at this point. This fallback is
        // defensive only; it must not turn nearby party members into XP recipients.
        return participants.Length > 0 ? participants : new[] { source };
    }

    private RuntimeNpcExperienceDistribution DistributeNpcDefeatExperience(
        RuntimeSession source,
        RuntimeLiveCombatEncounter defeatedEncounter,
        RuntimeNpcDefinition npc,
        IReadOnlyList<RuntimeSession> participants)
    {
        var split = SplitClassicNpcExperience(npc.ExperienceReward, participants.Count);
        var awards = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var gangs = new RuntimeClassicGangStateService(_characters.ProjectRoot);
        var nowUtc = DateTimeOffset.UtcNow;

        foreach (var participant in participants)
        {
            if (participant.Actor is not RuntimeCharacterActor actor) continue;
            var character = _characters.LoadCharacter(actor.Id)
                ?? throw new InvalidOperationException($"Character save was not found for active combatant '{actor.Id}'.");

            // Race/class EXP percentages are progression-cost modifiers, not multipliers on
            // monster XP earned. Apply the native shared amount directly.
            var awarded = RuntimeCharacterProgressionResolver.AwardExperience(
                character,
                split.Share,
                $"npc:{npc.Id}|active-combat-split={participants.Count}");
            var conditions = new Dictionary<string, string>(awarded.Conditions, StringComparer.OrdinalIgnoreCase)
            {
                ["combat.last_kill_xp_awarded"] = split.Share.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["combat.last_kill_xp_npc_id"] = npc.Id,
                ["combat.last_kill_xp_participant_count"] = participants.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["combat.last_kill_xp_remainder"] = split.Remainder.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["combat.last_kill_xp_remainder_policy"] = split.Remainder == 0 ? "none" : "native-recipient-unresolved-unassigned",
                ["combat.last_kill_xp_at_utc"] = nowUtc.ToString("O")
            };
            awarded = awarded with { Conditions = conditions, UpdatedAtUtc = nowUtc };
            _characters.SaveCharacter(awarded);
            awards[actor.Id] = split.Share;

            if (split.Share > 0 && gangs.FindByMember(actor.Id) is not null)
                gangs.AwardMemberExperience(actor.Id, split.Share);

            if (_liveCombats.TryGetValue(participant.Id, out var activeEncounter)
                && activeEncounter.NpcInstanceId.Equals(defeatedEncounter.NpcInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                ReleaseCombatEngagement(participant, activeEncounter, "npc-defeat-xp-distribution");
                _liveCombats.TryRemove(participant.Id, out _);
            }

            if (!participant.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase))
                participant.EnqueueMessage($"You gain {split.Share} experience.\n*Combat Off*");
        }

        return new RuntimeNpcExperienceDistribution(awards, split.Share, split.Remainder, participants.Count);
    }

    private sealed record RuntimeNpcExperienceDistribution(
        IReadOnlyDictionary<string, long> Awards,
        long Share,
        long Remainder,
        int ParticipantCount);

    internal static bool ShouldDropNpcLoot(int dropPercent, int roll)
    {
        if (dropPercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(dropPercent));
        if (roll is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll <= dropPercent;
    }

    internal static int RollNpcLootPercent(string characterId, string npcId, string canonicalItemId, long experienceBeforeKill, string roomId)
    {
        var key = $"{characterId}|{npcId}|{canonicalItemId}|{experienceBeforeKill}|{roomId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return 1 + (int)(BitConverter.ToUInt32(bytes, 0) % 100);
    }

    private string CompletePlayerDefeat(RuntimeSession session, RuntimeCharacterRecord before)
    {
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, before.WorldId);
        var nowUtc = DateTimeOffset.UtcNow;
        _ = RuntimeDecodedDeathTextAuthority.Resolve(before, catalog,
            RuntimeClassicNativeTextblockPayloadAuthority.Load(_characters.ProjectRoot));
        RemoveCharacterLiveStatusEffects(before.CharacterId, nowUtc, "runtime.live_status.player_death");
        var cleaned = RuntimeClassicStatusProjection.Clear(before, nowUtc, "runtime.live_status.player_death");
        var transition = RuntimeCharacterDeathResolver.Defeat(cleaned, catalog, session.CurrentRoom.Id, nowUtc);
        var defeated = ScatterDeathGearFromDecodedRoomSpell(session, transition.Character, transition.Corpse.CorpseId);
        _characters.SaveCharacter(defeated.Character);
        var livesRemaining = RuntimeClassicCharacterProgressionFormula.ReadLives(defeated.Character);
        if (livesRemaining <= 0)
            return $"Your corpse ({transition.Corpse.CorpseId}) remains in {session.CurrentRoom.Name}. You have no lives remaining; this character is permanently dead.";
        if (defeated.ScatteredRootItems > 0)
            return $"Your dropped gear was scattered from {session.CurrentRoom.Name} into {defeated.DestinationRoomCount} decoded destination room(s). Your corpse ({transition.Corpse.CorpseId}) remains here with any recoverable gold. {livesRemaining} lives remain. Use 'respawn'.";
        return $"Your corpse ({transition.Corpse.CorpseId}) remains in {session.CurrentRoom.Name}. {livesRemaining} lives remain. Use 'respawn', then return and use 'recover'.";
    }

    private (RuntimeCharacterRecord Character, int ScatteredRootItems, int DestinationRoomCount) ScatterDeathGearFromDecodedRoomSpell(
        RuntimeSession session, RuntimeCharacterRecord defeated, string corpseId)
    {
        if (session.CurrentRoom.EnvironmentRoomSpellId <= 0 || defeated.ItemState is null)
            return (defeated, 0, 0);
        var spells = RuntimeSpellCatalog.Load(_characters.ProjectRoot, defeated.WorldId);
        var roomSpell = spells.Spells.SingleOrDefault(spell =>
            int.TryParse(spell.LegacyId, out var legacyId) && legacyId == session.CurrentRoom.EnvironmentRoomSpellId);
        var scatter = roomSpell?.DecodedAbilities.FirstOrDefault(ability => ability.AbilityId == 157);
        if (roomSpell is null || scatter is null) return (defeated, 0, 0);

        var destinationNumbers = scatter.Value > 0
            ? new[] { scatter.Value }
            : Enumerable.Range(roomSpell.MinimumEffect, Math.Max(0, roomSpell.MaximumEffect - roomSpell.MinimumEffect + 1)).ToArray();
        var destinations = destinationNumbers.Select(number => session.World.FindRoom(session.CurrentRoom.MapId, number))
            .Where(room => room is not null).Select(room => room!).DistinctBy(room => room.Id).ToArray();
        if (destinations.Length == 0)
            throw new InvalidOperationException($"Decoded ScatterItems room spell {roomSpell.LegacyId} has no valid same-map destination rooms.");

        var state = defeated.ItemState;
        var roots = state.Items.Where(item => item.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)
            && item.LocationId.Equals(corpseId, StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        if (roots.Length == 0) return (defeated, 0, destinations.Length);
        var living = Living(session);
        for (var index = 0; index < roots.Length; index++)
            living.DropGroundItemTree(state.Items, roots[index].InstanceId, destinations[index % destinations.Length].Id);

        var scattered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(roots.Select(root => root.InstanceId));
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!scattered.Add(id)) continue;
            foreach (var child in state.Items.Where(item => item.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase)
                && item.LocationId.Equals(id, StringComparison.OrdinalIgnoreCase))) pending.Push(child.InstanceId);
        }
        var remaining = state.Items.Where(item => !scattered.Contains(item.InstanceId)).ToArray();
        return (defeated with { ItemState = state with { Items = remaining } }, roots.Length, destinations.Length);
    }
}

public sealed class RuntimeCombatCompletionSmokeReport
{
    public bool Passed => Failures.Count == 0;
    public List<string> Checks { get; } = new();
    public List<string> Failures { get; } = new();

    public static RuntimeCombatCompletionSmokeReport Run(string projectRoot)
    {
        var report = new RuntimeCombatCompletionSmokeReport();
        try
        {
            var root = ServerDataRootResolver.ResolveDefault(projectRoot).DataRoot;
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "npcs.json")));
            var records = doc.RootElement.GetProperty("records").EnumerateArray().ToArray();
            Require(records.Any(x => x.TryGetProperty("rewards", out var r) && JsonValueReader.ReadInt(r, "xp_base", 0) >= 0), "generated-xp-rewards"); report.Checks.Add("generated-xp-rewards");
            Require(records.All(x => x.TryGetProperty("rewards", out var r)
                && JsonValueReader.ReadInt(r, "copper", -1) >= 0
                && JsonValueReader.ReadInt(r, "silver", -1) >= 0
                && JsonValueReader.ReadInt(r, "gold", -1) >= 0
                && JsonValueReader.ReadInt(r, "platinum", -1) >= 0
                && JsonValueReader.ReadInt(r, "runic", -1) >= 0), "generated-denomination-rewards"); report.Checks.Add("generated-denomination-rewards");
            Require(records.Any(x => x.TryGetProperty("rewards", out var r)
                && JsonValueReader.ReadInt(r, "copper", 0) == 0
                && JsonValueReader.ReadInt(r, "silver", 0) == 0
                && JsonValueReader.ReadInt(r, "gold", 0) == 0
                && JsonValueReader.ReadInt(r, "platinum", 0) == 0
                && JsonValueReader.ReadInt(r, "runic", 0) == 0), "zero-currency-npcs-preserved"); report.Checks.Add("zero-currency-npcs-preserved");
            Require(records.Any(x => x.TryGetProperty("public_reference_0139", out var pr)
                && pr.TryGetProperty("fields", out var fields)
                && fields.TryGetProperty("loot_named", out var loot)
                && loot.ValueKind == JsonValueKind.Array
                && loot.GetArrayLength() > 0), "decoded-npc-loot-relationships"); report.Checks.Add("decoded-npc-loot-relationships");
            Require(RuntimeLiveGameplayFacade.ShouldDropNpcLoot(1, 1), "loot-one-percent-inclusive-success"); report.Checks.Add("loot-one-percent-inclusive-success");
            Require(!RuntimeLiveGameplayFacade.ShouldDropNpcLoot(1, 2), "loot-one-percent-inclusive-failure"); report.Checks.Add("loot-one-percent-inclusive-failure");
            Require(RuntimeLiveGameplayFacade.ShouldDropNpcLoot(100, 100), "loot-hundred-percent-inclusive-success"); report.Checks.Add("loot-hundred-percent-inclusive-success");
            var oddSplit = RuntimeLiveGameplayFacade.SplitClassicNpcExperience(525, 2);
            Require(oddSplit.Share == 262 && oddSplit.Remainder == 1, "active-combat-xp-integer-split-525-by-2"); report.Checks.Add("active-combat-xp-integer-split-525-by-2");
            var banditSplit = RuntimeLiveGameplayFacade.SplitClassicNpcExperience(125, 2);
            Require(banditSplit.Share == 62 && banditSplit.Remainder == 1, "active-combat-xp-integer-split-125-by-2"); report.Checks.Add("active-combat-xp-integer-split-125-by-2");
            var evenSplit = RuntimeLiveGameplayFacade.SplitClassicNpcExperience(500, 2);
            Require(evenSplit.Share == 250 && evenSplit.Remainder == 0, "active-combat-xp-even-split-500-by-2"); report.Checks.Add("active-combat-xp-even-split-500-by-2");
            var exact = new RuntimeClassicRoomCurrencyBalance(1, 2, 3, 4, 5);
            Require(RuntimeClassicRoomCurrencyStore.Format(exact).Contains("1 runic coin", StringComparison.Ordinal), "exact-runic-denomination-format"); report.Checks.Add("exact-runic-denomination-format");
            Require(RuntimeClassicRoomCurrencyStore.Format(exact).Contains("2 platinum pieces", StringComparison.Ordinal), "exact-denomination-format"); report.Checks.Add("exact-denomination-format");
            Require(RuntimeLiveGameplayFacade.ClassicCombatRoundMilliseconds == 5000, "classic-five-second-combat-round"); report.Checks.Add("classic-five-second-combat-round");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.TickLiveCombatsAsync)) is not null, "automatic-combat-tick"); report.Checks.Add("automatic-combat-tick");
            Require(RuntimeLiveGameplayFacade.ClassicSpellSystemRoundMilliseconds == 3000, "classic-three-second-spell-system-round"); report.Checks.Add("classic-three-second-spell-system-round");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.TickLiveStatusesAsync)) is not null, "automatic-spell-system-tick"); report.Checks.Add("automatic-spell-system-tick");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.RespawnCharacter)) is not null, "respawn-execution"); report.Checks.Add("respawn-execution");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.RecoverCorpse)) is not null, "corpse-recovery"); report.Checks.Add("corpse-recovery");
            var catalog = RuntimeItemCatalog.Load(projectRoot, ServerRuntimeOptions.Load(projectRoot).DefaultWorldId);
            var deathTextItems = catalog.Items.Where(item => item.EquipmentAbilityModifiers.ContainsKey(155)).ToArray();
            var native = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
            Require(deathTextItems.Length == 1 && deathTextItems[0].Id == "item.1097"
                && deathTextItems[0].EquipmentAbilityModifiers[155] == 1643,
                "decoded-death-text-single-item-1097-target-1643");
            report.Checks.Add("decoded-death-text-single-item-1097-target-1643");
            Require(native.TryResolveTextblock(1643, out var deathTextCandidate)
                && !RuntimeDecodedDeathTextAuthority.IsDeathPayload(deathTextCandidate),
                "decoded-death-text-1643-semantic-mismatch-quarantined");
            report.Checks.Add("decoded-death-text-1643-semantic-mismatch-quarantined");
            var engine = new RuntimeItemEngine(catalog);
            var corpse = engine.CreateCorpse("character.test", "room.test", 5);
            Require(engine.Corpses.Any(x => x.CorpseId == corpse.CorpseId), "corpse-creation"); report.Checks.Add("corpse-creation");
        }
        catch (Exception ex) { report.Failures.Add(ex.Message); }
        return report;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
}
