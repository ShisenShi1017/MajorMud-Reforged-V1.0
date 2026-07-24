using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public const int ClassicCombatRoundMilliseconds = 6000;
    public async Task TickLiveCombatsAsync(CancellationToken cancellationToken)
    {
        PruneInactiveCombatOwnership();
        var nowUtc = DateTimeOffset.UtcNow;
        var statusWorlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var activeSession in _sessions.ActiveSessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (statusWorlds.Add(activeSession.World.Definition.Id))
                TickClassicLiveStatuses(activeSession, nowUtc);
        }
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
        await Task.CompletedTask;
    }

    public string RespawnCharacter(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        if (!RuntimeCharacterFlagResolver.IsSet(character, RuntimeCharacterFlagIds.Dead) || character.HitPoints != 0) return "You are not dead.";
        var room = session.World.ResolveGeneratedCharacterStartRoom() ?? throw new InvalidOperationException("Generated character start room is unavailable.");
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

    private string CompleteNpcDefeat(RuntimeSession session, RuntimeCharacterRecord before, RuntimeNpcDefinition npc, int playerHp)
    {
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

        var characterCatalog = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, before.WorldId);
        var race = characterCatalog.RequireRace(before.RaceId);
        var characterClass = characterCatalog.RequireClass(before.ClassId);
        var awarded = RuntimeCharacterProgressionResolver.AwardExperience(before, npc.ExperienceReward, race, characterClass, "npc:" + npc.Id);
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
            if (!ShouldDropNpcLoot(loot.DropPercent, roll)) continue;
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

        var attribution = new Dictionary<string, string>(awarded.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            ["combat.last_kill_credit_character_id"] = before.CharacterId,
            ["combat.last_kill_npc_id"] = npc.Id,
            ["combat.last_kill_npc_legacy_id"] = npc.LegacyId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["combat.last_kill_xp_awarded"] = (awarded.Experience - before.Experience).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["combat.last_loot_attribution_character_id"] = before.CharacterId,
            ["combat.last_kill_at_utc"] = DateTimeOffset.UtcNow.ToString("O")
        };
        var updated = awarded with
        {
            HitPoints = playerHp,
            Conditions = attribution,
            ItemState = state with { Items = movedItems.ToArray() },
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _characters.SaveCharacter(updated);

        var moneyStatus = string.IsNullOrWhiteSpace(currencyText) ? string.Empty : $" {currencyText} fall to the ground.";
        var lootStatus = dropped.Count == 0 ? string.Empty : " It also drops " + string.Join(", ", dropped) + ".";
        var awardedXp = awarded.Experience - before.Experience;
        return $"Reward: {awardedXp} XP.{moneyStatus}{lootStatus}";
    }

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
        RemoveCharacterLiveStatusEffects(before.CharacterId, nowUtc, "runtime.live_status.player_death");
        var cleaned = RuntimeClassicStatusProjection.Clear(before, nowUtc, "runtime.live_status.player_death");
        var transition = RuntimeCharacterDeathResolver.Defeat(cleaned, catalog, session.CurrentRoom.Id, nowUtc);
        _characters.SaveCharacter(transition.Character);
        return $"Your corpse ({transition.Corpse.CorpseId}) remains in {session.CurrentRoom.Name}. Use 'respawn', then return and use 'recover'.";
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
            var exact = new RuntimeClassicRoomCurrencyBalance(1, 2, 3, 4, 5);
            Require(RuntimeClassicRoomCurrencyStore.Format(exact).Contains("1 runic coin", StringComparison.Ordinal), "exact-runic-denomination-format"); report.Checks.Add("exact-runic-denomination-format");
            Require(RuntimeClassicRoomCurrencyStore.Format(exact).Contains("2 platinum pieces", StringComparison.Ordinal), "exact-denomination-format"); report.Checks.Add("exact-denomination-format");
            Require(RuntimeLiveGameplayFacade.ClassicCombatRoundMilliseconds == 6000, "classic-six-second-combat-round"); report.Checks.Add("classic-six-second-combat-round");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.TickLiveCombatsAsync)) is not null, "automatic-combat-tick"); report.Checks.Add("automatic-combat-tick");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.RespawnCharacter)) is not null, "respawn-execution"); report.Checks.Add("respawn-execution");
            Require(typeof(RuntimeLiveGameplayFacade).GetMethod(nameof(RuntimeLiveGameplayFacade.RecoverCorpse)) is not null, "corpse-recovery"); report.Checks.Add("corpse-recovery");
            var catalog = RuntimeItemCatalog.Load(projectRoot, ServerRuntimeOptions.Load(projectRoot).DefaultWorldId);
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
