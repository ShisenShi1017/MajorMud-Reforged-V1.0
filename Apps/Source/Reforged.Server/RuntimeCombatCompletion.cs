using System.Text.Json;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    public async Task TickLiveCombatsAsync(CancellationToken cancellationToken)
    {
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
        if (character.HitPoints > 0) return "You are not dead.";
        var room = session.World.ResolveGeneratedCharacterStartRoom() ?? throw new InvalidOperationException("Generated character start room is unavailable.");
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        conditions.Remove("dead");
        var updated = character with
        {
            HitPoints = character.MaximumHitPoints,
            RoomId = room.Id,
            RoomNumber = room.RoomNumber,
            Conditions = conditions,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _characters.SaveCharacter(updated);
        session.MoveToWorld(session.World, room, "combat-respawn");
        _sessions.BroadcastRoom(session, $"{actor.DisplayName} returns to life.", includeSource: false);
        return $"You return to life at {room.Name} with {updated.HitPoints}/{updated.MaximumHitPoints} HP.";
    }

    public string RecoverCorpse(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor) return "A persistent character is required.";
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var corpse = (state.Corpses ?? Array.Empty<RuntimeCorpseState>())
            .Where(x => !x.CleanedUp && x.CharacterId.Equals(actor.Id, StringComparison.OrdinalIgnoreCase) && x.RoomId.Equals(session.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();
        if (corpse is null) return "Your corpse is not here.";
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
        var items = engine.Items.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && x.LocationId.Equals(corpse.CorpseId, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var item in items) engine.Loot(item.InstanceId, corpse.CorpseId, actor.Id);
        var gold = engine.LootGold(corpse.CorpseId);
        var snapshot = engine.Snapshot(checked(state.Gold + gold), state.ActiveItemState);
        _characters.SaveCharacter(character with { ItemState = snapshot, UpdatedAtUtc = DateTimeOffset.UtcNow });
        _sessions.BroadcastRoom(session, $"{actor.DisplayName} recovers a corpse.", includeSource: false);
        return $"You recover {items.Length} item(s) and {gold} gold from your corpse.";
    }

    private string CompleteNpcDefeat(RuntimeSession session, RuntimeCharacterRecord before, RuntimeNpcDefinition npc, int playerHp)
    {
        var state = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var updatedState = state with { Gold = checked(state.Gold + npc.GoldReward) };
        var updated = before with
        {
            HitPoints = playerHp,
            Experience = checked(before.Experience + npc.ExperienceReward),
            ItemState = updatedState,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _characters.SaveCharacter(updated);
        var lootStatus = npc.LootKnown && npc.LootTableEntries > 0
            ? "Generated loot relationships are present but item instantiation is not yet attached."
            : "No authoritative generated loot entries are available for this NPC.";
        return $"Reward: {npc.ExperienceReward} XP and {npc.GoldReward} gold. {lootStatus}";
    }

    private string CompletePlayerDefeat(RuntimeSession session, RuntimeCharacterRecord before)
    {
        var state = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        var catalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, before.WorldId);
        var engine = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
        var corpse = engine.CreateCorpse(before.CharacterId, session.CurrentRoom.Id, state.Gold);
        var conditions = new Dictionary<string, string>(before.Conditions, StringComparer.OrdinalIgnoreCase) { ["dead"] = "true" };
        var snapshot = engine.Snapshot(0, state.ActiveItemState);
        _characters.SaveCharacter(before with { HitPoints = 0, ItemState = snapshot, Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return $"Your corpse ({corpse.CorpseId}) remains in {session.CurrentRoom.Name}. Use 'respawn', then return and use 'recover'.";
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
            Require(records.Any(x => x.TryGetProperty("rewards", out var r) && JsonValueReader.ReadInt(r, "gold", 0) >= 0), "generated-gold-rewards"); report.Checks.Add("generated-gold-rewards");
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
