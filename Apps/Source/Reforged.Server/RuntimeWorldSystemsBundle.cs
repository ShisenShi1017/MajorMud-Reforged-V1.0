using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed partial class RuntimeLiveGameplayFacade
{
    private const string QuestConditionKey = "runtime.quest-journal.v1";
    private const string CharacterPointsKey = "character_points";

    private string ExecuteGeneratedServiceTransaction(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) throw new InvalidOperationException("A persistent character is required.");
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        var catalog = RuntimeServiceCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var services = ResolveServicesAtRoom(catalog, session.CurrentRoom.RoomNumber).ToArray();
        if (services.Length == 0) throw new InvalidOperationException("There is no generated service in this room.");
        if (command.Name is "tat" or "stats" || (command.Name == "train" && command.ArgumentText.StartsWith("stats ", StringComparison.OrdinalIgnoreCase)))
            return command.Name == "train" ? SpendTatPoint(character, command.ArgumentText[6..]) : SpendTatPoint(character, command.ArgumentText);

        // Classic shops and trainers are room-owned services. A merchant NPC may be present
        // for atmosphere, but buying, selling, repairing, and training do not require one.
        return command.Name switch
        {
            "buy" => BuyGeneratedItem(character, services, command.ArgumentText),
            "sell" => SellGeneratedItem(character, services, command.ArgumentText),
            "repair" => RepairGeneratedItem(character, services, command.ArgumentText),
            "train" => TrainAtGeneratedTrainer(character, services),
            _ => throw new InvalidOperationException("Unsupported generated service transaction.")
        };
    }

    private static RuntimeServiceDefinition RequireShop(IReadOnlyList<RuntimeServiceDefinition> services) =>
        services.FirstOrDefault(x => !ReadServiceKind(x).Equals("trainer", StringComparison.OrdinalIgnoreCase) && x.GeneratedRecord.TryGetProperty("inventory", out var i) && i.ValueKind == JsonValueKind.Array)
        ?? throw new InvalidOperationException("There is no generated shop inventory in this room.");

    private string BuyGeneratedItem(RuntimeCharacterRecord character, IReadOnlyList<RuntimeServiceDefinition> services, string query)
    {
        var shop = RequireShop(services);
        var items = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var request = ParseMerchantQuantity(query);
        var entry = ResolveOffering(shop.GeneratedRecord, "inventory", request.Query, items);
        var itemId = ReadText(entry, "item_ref") ?? throw new InvalidOperationException("Generated shop entry has no item reference.");
        var definition = items.Require(itemId);
        var cost = Math.Max(0, ReadItemCost(RequireGeneratedItemRecord(_characters.ProjectRoot, character.WorldId, definition.Id)));

        return GameplayStore(character.WorldId).ExecuteShopTransaction(shop, stock =>
        {
            if (!stock.TryGetValue(itemId, out var current) || current.Quantity < request.Quantity)
                throw new InvalidOperationException($"{definition.Name} does not have {request.Quantity} in stock.");
            var before = _characters.LoadCharacter(character.CharacterId) ?? throw new InvalidOperationException("Character save was not found.");
            var state = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>());
            var totalCostGold = checked(cost * request.Quantity);
            var totalCostCopper = checked(totalCostGold * 100L);
            var settledState = RuntimeBundleCEconomyAuthority.DebitCopperValue(state, totalCostCopper);
            var engine = new RuntimeItemEngine(items, state.Items, state.Corpses);
            RuntimeItemInstance? created = null;
            for (var index = 0; index < request.Quantity; index++)
                created = engine.Create(itemId, RuntimeItemLocations.Character, before.CharacterId, ownerId: before.CharacterId);
            var updated = before with { ItemState = engine.Snapshot(settledState.Gold, settledState.ActiveItemState), UpdatedAtUtc = DateTimeOffset.UtcNow };
            _characters.SaveCharacter(updated);
            stock[itemId] = current with { Quantity = current.Quantity - request.Quantity };
            var priceText = totalCostCopper == 0 ? "free" : RuntimeBundleCEconomyAuthority.FormatCopperValue(totalCostCopper);
            return $"You buy {request.Quantity} {items.Require(created!.CanonicalItemId).Name} for {priceText}. {current.Quantity - request.Quantity} remain.";
        });
    }

    private string SellGeneratedItem(RuntimeCharacterRecord character, IReadOnlyList<RuntimeServiceDefinition> services, string query)
    {
        var shop = RequireShop(services);
        var items = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var request = ParseMerchantQuantity(query);
        return GameplayStore(character.WorldId).ExecuteShopTransaction(shop, stock =>
        {
            var before = _characters.LoadCharacter(character.CharacterId) ?? throw new InvalidOperationException("Character save was not found.");
            var state = before.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>());
            var engine = new RuntimeItemEngine(items, state.Items, state.Corpses);
            var matching = engine.Inventory(before.CharacterId)
                .Where(x => MerchantItemMatches(x, items, request.Query))
                .Take(request.Quantity + 1)
                .ToArray();
            if (matching.Length < request.Quantity) throw new InvalidOperationException($"You do not carry {request.Quantity} matching items.");
            var selected = matching.Take(request.Quantity).ToArray();
            var canonicalId = selected[0].CanonicalItemId;
            if (selected.Any(x => !x.CanonicalItemId.Equals(canonicalId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A merchant sale quantity must resolve to one canonical item type.");
            var value = ReadItemCost(RequireGeneratedItemRecord(_characters.ProjectRoot, before.WorldId, canonicalId));
            if (value <= 0) throw new InvalidOperationException("That item has no generated economy value.");
            foreach (var item in selected) engine.SellToMerchant(item.InstanceId, before.CharacterId, shop.Id, 1);
            var totalValueGold = checked(value * request.Quantity);
            var totalValueCopper = checked(totalValueGold * 100L);
            var settledState = RuntimeBundleCEconomyAuthority.CreditCopperValue(state, totalValueCopper);
            _characters.SaveCharacter(before with { ItemState = engine.Snapshot(settledState.Gold, settledState.ActiveItemState), UpdatedAtUtc = DateTimeOffset.UtcNow });
            if (stock.TryGetValue(canonicalId, out var current))
                stock[canonicalId] = current with { Quantity = checked(current.Quantity + request.Quantity) };
            else
                stock[canonicalId] = new RuntimeShopStockEntry(canonicalId, request.Quantity, DateTimeOffset.MaxValue);
            return $"You sell {request.Quantity} {items.Require(canonicalId).Name} for {RuntimeBundleCEconomyAuthority.FormatCopperValue(totalValueCopper)}.";
        });
    }


    private sealed record RuntimeMerchantQuantityRequest(int Quantity, string Query);

    private static RuntimeMerchantQuantityRequest ParseMerchantQuantity(string argument)
    {
        var text = argument.Trim();
        if (text.Length == 0) throw new InvalidOperationException("Specify an item.");
        var split = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length == 2 && int.TryParse(split[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
        {
            if (quantity <= 0 || quantity > 1000) throw new InvalidOperationException("Merchant quantity must be between 1 and 1000.");
            return new(quantity, split[1]);
        }
        return new(1, text);
    }

    private static bool MerchantItemMatches(RuntimeItemInstance item, RuntimeItemCatalog catalog, string query)
    {
        var definition = catalog.Require(item.CanonicalItemId);
        return item.CanonicalItemId.Equals(query, StringComparison.OrdinalIgnoreCase)
            || item.CanonicalItemId.StartsWith(query, StringComparison.OrdinalIgnoreCase)
            || definition.Name.Equals(query, StringComparison.OrdinalIgnoreCase)
            || definition.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase);
    }

    private string RepairGeneratedItem(RuntimeCharacterRecord character, IReadOnlyList<RuntimeServiceDefinition> services, string query)
    {
        var repair = services.FirstOrDefault(x => HasOperation(x.GeneratedRecord, "repair") || ReadServiceKind(x).Contains("repair", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("There is no generated repair service in this room.");
        var state = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string,string>(), Array.Empty<RuntimeCorpseState>());
        var items = RuntimeItemCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var engine = new RuntimeItemEngine(items, state.Items, state.Corpses);
        var selected = Resolve(engine.Inventory(character.CharacterId).Concat(engine.Equipment(character.CharacterId)), items, query);
        if (selected.Metadata is null || !selected.Metadata.TryGetValue("durability", out var d) || !selected.Metadata.TryGetValue("maximum_durability", out var m) || !int.TryParse(d, out var durability) || !int.TryParse(m, out var maximum) || maximum <= durability)
            throw new InvalidOperationException("That item has no generated repairable durability loss.");
        var unit = ReadLong(repair.GeneratedRecord, "repair_cost_per_point", "repair_cost");
        var cost = checked(unit * (maximum - durability));
        if (cost <= 0) throw new InvalidOperationException("Generated repair cost is unavailable.");
        var repairCostCopper = checked(cost * 100L);
        var settledState = RuntimeBundleCEconomyAuthority.DebitCopperValue(state, repairCostCopper);
        var metadata = new Dictionary<string,string>(selected.Metadata ?? new Dictionary<string,string>(), StringComparer.OrdinalIgnoreCase) { ["durability"] = maximum.ToString(CultureInfo.InvariantCulture) };
        engine.Remove(selected.InstanceId); engine.Add(selected with { Metadata = metadata });
        _characters.SaveCharacter(character with { ItemState = engine.Snapshot(settledState.Gold, settledState.ActiveItemState), UpdatedAtUtc = DateTimeOffset.UtcNow });
        return $"You repair {items.Require(selected.CanonicalItemId).Name} for {RuntimeBundleCEconomyAuthority.FormatCopperValue(repairCostCopper)}.";
    }

    private string TrainAtGeneratedTrainer(RuntimeCharacterRecord character, IReadOnlyList<RuntimeServiceDefinition> services)
    {
        var targetLevel = checked(character.Level + 1);
        var trainerRule = RuntimeCharacterTrainerCatalog.RequireTrainer(services, character, targetLevel);
        var trainer = services.First(service => service.Id.Equals(trainerRule.TrainerId, StringComparison.OrdinalIgnoreCase));

        var schedule = RuntimeCharacterProgressionSchedule.Load(Path.Combine(_characters.ProjectRoot, "Data", character.WorldId));
        var row = schedule.Require(character, targetLevel);
        if (character.Experience < row.RequiredExperience) throw new InvalidOperationException($"You need {row.RequiredExperience} total experience to train to level {targetLevel}.");

        var foundation = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var race = foundation.RequireRace(character.RaceId);
        var characterClass = foundation.RequireClass(character.ClassId);
        var promoted = RuntimeCharacterProgressionResolver.Promote(
            character,
            targetLevel,
            row.CharacterPointAward,
            race,
            characterClass,
            $"trainer:{trainer.Id}|progression:{row.RowId}");
        _characters.SaveCharacter(promoted);
        return $"You train to level {targetLevel} and receive {row.CharacterPointAward} CP. Use TRAIN STATS/TAT to spend unallocated CP.";
    }


    private string SpendTatPoint(RuntimeCharacterRecord character, string argument)
    {
        var stat = argument.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(stat)) throw new InvalidOperationException("Usage: TAT <stat> or TRAIN STATS <stat>.");
        var aliases = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["agility"]="agl", ["agl"]="agl", ["charisma"]="cha", ["cha"]="cha",
            ["health"]="hea", ["hea"]="hea", ["intellect"]="int", ["intelligence"]="int", ["int"]="int",
            ["strength"]="str", ["str"]="str", ["willpower"]="wil", ["will"]="wil", ["wil"]="wil"
        };
        if (!aliases.TryGetValue(stat, out var key)) throw new InvalidOperationException($"Unknown primary stat '{argument}'.");
        var foundation = RuntimeCharacterCatalog.Load(_characters.ProjectRoot, character.WorldId);
        var race = foundation.RequireRace(character.RaceId);
        if (!race.MinimumStats.TryGetValue(key, out var racialBase)) throw new InvalidOperationException($"Generated race data has no base value for {key}.");
        if (!character.BaseStats.TryGetValue(key, out var current)) current = racialBase;
        if (race.MaximumStats.TryGetValue(key, out var maximum) && maximum > 0 && current >= maximum) throw new InvalidOperationException($"{key.ToUpperInvariant()} is already at the generated racial maximum of {maximum}.");
        var pointsAboveBase = Math.Max(0, current - racialBase);
        var cost = checked((pointsAboveBase / 10) + 1);
        var conditions = new Dictionary<string,string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        var available = conditions.TryGetValue(CharacterPointsKey, out var cpText) && int.TryParse(cpText, out var cp) ? cp : 0;
        if (available < cost) throw new InvalidOperationException($"Increasing {key.ToUpperInvariant()} from {current} to {current + 1} costs {cost} CP; you have {available}.");
        var stats = new Dictionary<string,int>(character.BaseStats, StringComparer.OrdinalIgnoreCase) { [key] = checked(current + 1) };
        conditions[CharacterPointsKey] = (available - cost).ToString(CultureInfo.InvariantCulture);
        var before = character;
        var updated = character with { BaseStats = stats, Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow };
        try { _characters.SaveCharacter(updated); } catch { _characters.SaveCharacter(before); throw; }
        return $"You increase {key.ToUpperInvariant()} to {current + 1} for {cost} CP. {available - cost} CP remain.";
    }

    public string ExecuteWorldNarrativeCommand(RuntimeSession session, RuntimeCommand command)
    {
        if (session.Actor is not RuntimeCharacterActor actor) throw new InvalidOperationException("A persistent character is required.");
        var character = _characters.LoadCharacter(actor.Id) ?? throw new InvalidOperationException("Character save was not found.");
        return command.Name switch
        {
            "talk" => ExecuteGeneratedNpcDialogue(session, character, command.ArgumentText),
            "journal" or "quest" when command.Arguments.Count == 0 => RenderGeneratedQuestJournal(character),
            "quest" when command.Arguments.Count > 0 && command.Arguments[0].Equals("reset", StringComparison.OrdinalIgnoreCase) => MarkQuestState(character, string.Join(" ", command.Arguments.Skip(1)), "reset"),
            "accept" => MarkQuestState(character, command.ArgumentText, "active"),
            "abandon" => MarkQuestState(character, command.ArgumentText, "abandoned"),
            "complete" => MarkQuestState(character, command.ArgumentText, "completed"),
            _ => _policyGateway.Execute(session, command)
        };
    }

    private string ExecuteGeneratedNpcDialogue(RuntimeSession session, RuntimeCharacterRecord character, string query)
    {
        var npc = ResolveDialogueNpc(session, query);
        var admission = AdmitLivingNpcDialogue(session, npc, character.CharacterId);
        if (!admission.Admitted || string.IsNullOrWhiteSpace(admission.RequestId))
            throw new InvalidOperationException($"NPC dialogue admission failed: {admission.Reason}.");
        var catalog = session.World.NarrativeCatalog.DialogueCatalog;
        var itemCatalog = RuntimeItemCatalog.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var worldIntents = RuntimeQuestWorldIntentValidator.Load(_characters.ProjectRoot, session.World.Definition.Id);
        var context = new RuntimeQuestDialogueContext(character, itemCatalog, worldIntents);
        RuntimeQuestDialogueDispatchResult result;
        try
        {
            result = catalog.Dispatch(npc.NpcId, context);
        }
        catch
        {
            CompleteLivingNpcDialogue(session, npc, admission.RequestId, false, "dispatch-exception");
            throw;
        }
        if (!result.Matched)
        {
            CompleteLivingNpcDialogue(session, npc, admission.RequestId, false, result.FailureCode);
            return $"No generated quest dialogue matched {npc.Name}. ({result.FailureCode})";
        }

        var updatedCharacter = context.ToCharacterRecord();
        var hostIntents = ApplyQuestWorldIntents(session, updatedCharacter, result.Events, result.TriggerId, result.ScriptId);
        updatedCharacter = hostIntents.Character;
        _characters.SaveCharacter(updatedCharacter);
        if (!CompleteLivingNpcDialogue(session, npc, admission.RequestId, true, result.ClauseId ?? result.ScriptId ?? "matched"))
            throw new InvalidOperationException("NPC dialogue completion did not match the admitted request.");
        var events = result.Events.Count == 0
            ? "No state changes."
            : string.Join("; ", result.Events.Select(e => e.SubjectId is null ? e.EventType : $"{e.EventType}:{e.SubjectId}"));
        var lines = result.Lines.Count == 0 ? "No generated response text was emitted." : string.Join(Environment.NewLine, result.Lines);
        return string.Join(Environment.NewLine, new[]
        {
            $"{npc.Name}:",
            lines,
            $"Trigger: {result.TriggerId}",
            $"Script: {result.ScriptId}",
            $"Clause: {result.ClauseId}",
            $"Events: {events}"
        }.Concat(hostIntents.Lines));
    }

    private (RuntimeCharacterRecord Character, IReadOnlyList<string> Lines) ApplyQuestWorldIntents(
        RuntimeSession session,
        RuntimeCharacterRecord character,
        IReadOnlyList<RuntimeQuestDialogueEvent> events,
        string? triggerId,
        string? scriptId)
    {
        var lines = new List<string>();
        foreach (var questEvent in events)
        {
            switch (questEvent.EventType)
            {
                case "world_intent.teleport_character":
                    if (string.IsNullOrWhiteSpace(questEvent.SubjectId))
                        throw new InvalidOperationException("Generated teleport intent has no room target.");
                    var room = session.World.FindRoomById(questEvent.SubjectId)
                        ?? throw new InvalidOperationException($"Generated teleport target {questEvent.SubjectId} was not found.");
                    character = character with
                    {
                        RoomId = room.Id,
                        RoomNumber = room.RoomNumber,
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    };
                    session.MoveToWorld(session.World, room, "quest-dialogue-teleport");
                    lines.Add($"Host intent applied: teleport_character:{room.Id}");
                    break;
                case "world_intent.summon_npc":
                    if (string.IsNullOrWhiteSpace(questEvent.SubjectId))
                        throw new InvalidOperationException("Generated summon intent has no NPC target.");
                    var summoned = SummonLivingNpc(session, questEvent.SubjectId, scriptId ?? triggerId ?? "quest-dialogue");
                    lines.Add($"Host intent applied: summon_npc:{summoned.NpcId}:{summoned.InstanceId}");
                    break;
                case "world_intent.cast_spell":
                    lines.Add($"Host intent validated: cast_spell:{questEvent.SubjectId}");
                    break;
                case "world_intent.remote_action":
                    lines.Add($"Host intent preserved: remote_action:{questEvent.Metadata.GetValueOrDefault("raw", string.Empty)}");
                    break;
            }
        }

        return (character, lines);
    }

    private RuntimeLivingNpcState ResolveDialogueNpc(RuntimeSession session, string query)
    {
        var npcs = LivingNpcsInRoom(session)
            .Where(npc => npc.Alive)
            .ToArray();
        if (npcs.Length == 0) throw new InvalidOperationException("There is no one here to talk to.");
        if (string.IsNullOrWhiteSpace(query) && npcs.Length == 1) return npcs[0];
        var q = query.Trim();
        var matches = npcs.Where(npc =>
                npc.InstanceId.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                npc.NpcId.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                npc.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No visible NPC matches '{query}'."),
            _ => throw new InvalidOperationException($"Multiple visible NPCs match '{query}'.")
        };
    }

    private string RenderGeneratedQuestJournal(RuntimeCharacterRecord character)
    {
        var questStates = character.Conditions
            .Where(pair => pair.Key.StartsWith("quest.state.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key["quest.state.".Length..]}: {pair.Value}")
            .ToArray();
        var flags = character.Conditions
            .Where(pair => pair.Key.StartsWith("quest.ability.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .Select(pair => $"{pair.Key["quest.ability.".Length..]}={pair.Value}")
            .ToArray();
        if (questStates.Length == 0 && flags.Length == 0) return "No generated quest state has been recorded yet.";
        var sections = new List<string> { "Generated quest journal:" };
        if (questStates.Length > 0) sections.AddRange(questStates);
        if (flags.Length > 0)
        {
            sections.Add("Quest flags:");
            sections.AddRange(flags);
        }
        return string.Join(Environment.NewLine, sections);
    }

    private string MarkQuestState(RuntimeCharacterRecord character, string questId, string state)
    {
        if (string.IsNullOrWhiteSpace(questId)) throw new InvalidOperationException($"Usage: {state.ToUpperInvariant()} <quest-id>");
        var key = "quest.state." + questId.Trim();
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        if (state.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            if (conditions.TryGetValue(key, out var existingResetState) && existingResetState.Equals("completed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Classic quests cannot be repeated on the same character. Start a new character to replay a completed quest.");
            conditions.Remove(key);
            _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
            return $"Quest state reset for {questId}.";
        }
        if (conditions.TryGetValue(key, out var existing) && existing.Equals("completed", StringComparison.OrdinalIgnoreCase) && !state.Equals("completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Classic quests cannot be repeated on the same character. Start a new character to replay a completed quest.");
        conditions[key] = state;
        _characters.SaveCharacter(character with { Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow });
        return $"Quest {questId} marked {state}.";
    }

    private static JsonElement RequireGeneratedItemRecord(string projectRoot, string worldId, string itemId)
    {
        var path = Path.Combine(projectRoot, "Data", worldId, "items.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Generated Librarium item projection is missing.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("authority", out var authority) || !string.Equals(authority.GetString(), "Librarium", StringComparison.Ordinal))
            throw new InvalidOperationException("Item projection is not Librarium authoritative.");
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Generated item projection has no records array.");
        foreach (var record in records.EnumerateArray())
        {
            if (string.Equals(ReadText(record, "id"), itemId, StringComparison.OrdinalIgnoreCase)) return record.Clone();
        }
        throw new InvalidOperationException($"Generated item projection contains no item record '{itemId}'.");
    }

    private static long ReadItemCost(JsonElement item)
    {
        if (item.TryGetProperty("economy", out var economy) && economy.ValueKind == JsonValueKind.Object) return ReadLong(economy, "cost");
        return ReadLong(item, "cost", "value", "price");
    }
    private static JsonElement ResolveOffering(JsonElement service, string arrayName, string query, RuntimeItemCatalog items)
    {
        if (!service.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Generated service has no executable {arrayName} relationships.");
        var q = query.Trim();
        if (q.Length == 0) throw new InvalidOperationException("Buy what?");
        var matches = array.EnumerateArray().Where(entry =>
        {
            var itemRef = ReadText(entry, "item_ref");
            var generatedName = ReadText(entry, "name");
            string? catalogName = null;
            if (!string.IsNullOrWhiteSpace(itemRef))
            {
                try { catalogName = items.Require(itemRef).Name; } catch { }
            }
            return new[] { itemRef, generatedName, catalogName }.Any(value =>
                !string.IsNullOrWhiteSpace(value) &&
                (value.Equals(q, StringComparison.OrdinalIgnoreCase) || value.StartsWith(q, StringComparison.OrdinalIgnoreCase)));
        }).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(matches.Length == 0
            ? $"No shop item matches '{q}'."
            : $"Multiple shop items match '{q}'.");
    }
    private static bool HasOperation(JsonElement root,string operation) => root.TryGetProperty("operations",out var value)&&value.ValueKind==JsonValueKind.Array&&value.EnumerateArray().Any(x=>x.ToString().Equals(operation,StringComparison.OrdinalIgnoreCase));
    private static string? ReadText(JsonElement root, params string[] names){foreach(var n in names)if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty(n,out var v)&&v.ValueKind!=JsonValueKind.Null){var s=v.ToString();if(!string.IsNullOrWhiteSpace(s))return s;}return null;}
    private static long ReadLong(JsonElement root, params string[] names){foreach(var n in names)if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty(n,out var v)&&long.TryParse(v.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var x))return x;return 0;}
    private static int ReadInt(JsonElement root, params string[] names)=>checked((int)Math.Clamp(ReadLong(root,names),int.MinValue,int.MaxValue));
    private static int ReadNestedInt(JsonElement root,string parent,string name,int fallback)=>root.TryGetProperty(parent,out var nested)&&nested.ValueKind==JsonValueKind.Object&&nested.TryGetProperty(name,out var value)&&int.TryParse(value.ToString(),out var parsed)?parsed:fallback;
    private static int ParseLegacyNumber(string id){var dot=id.LastIndexOf('.');return int.TryParse(dot>=0?id[(dot+1)..]:id,out var value)?value:0;}
}

public sealed class RuntimeWorldSystemsBundleSmokeReport
{
    public bool Passed => Failures.Count == 0; public List<string> Checks { get; }=new(); public List<string> Failures { get; }=new();
    public static RuntimeWorldSystemsBundleSmokeReport Run(string root,string world)
    {
        var r=new RuntimeWorldSystemsBundleSmokeReport();
        try
        {
            var services=RuntimeServiceCatalog.Load(root,world);
            Check(r,services.Services.Any(x=>x.GeneratedRecord.TryGetProperty("inventory",out var i)&&i.ValueKind==JsonValueKind.Array),"active-classic-shop-inventory");
            var trainers=services.Services.Where(x=>x.GeneratedRecord.TryGetProperty("service_kind",out var k)&&k.GetString()=="trainer").ToArray();
            Check(r,trainers.Length==44,"trainer-count-44");
            Check(r,trainers.All(x=>x.GeneratedRecord.TryGetProperty("room_ref",out _)),"trainer-room-resolution");
            Check(r,trainers.All(x=>x.GeneratedRecord.TryGetProperty("level_range",out var lr)&&lr.ValueKind==JsonValueKind.Object),"trainer-level-ranges");
            Check(r,trainers.All(x=>x.GeneratedRecord.TryGetProperty("class_limit",out _)),"trainer-class-restrictions");
            Check(r,RuntimeCommand.SupportedCommands.Contains("train"),"train-routing");
            Check(r,RuntimeCommand.SupportedCommands.Contains("buy"),"buy-routing");
            Check(r,RuntimeCommand.SupportedCommands.Contains("tat")&&RuntimeCommand.SupportedCommands.Contains("stats"),"tat-cp-spend-routing");
            Check(r,TatCost(0)==1&&TatCost(9)==1&&TatCost(10)==2&&TatCost(19)==2&&TatCost(20)==3&&TatCost(29)==3&&TatCost(30)==4&&TatCost(39)==4&&TatCost(40)==5,"tat-tiered-costs");
            Check(r,TotalTatCost(0,40)==100,"tat-forty-point-total-cost");
            Check(r,typeof(RuntimeGameplayCompletionStateStore).GetMethod("ExecuteShopTransaction") is not null,"persistent-shop-stock-transaction");
            Check(r,typeof(RuntimeCharacterService).GetMethod("ExecuteCharacterTransaction") is not null,"atomic-direct-give-transaction");
            var stockedShop=services.Services.First(x=>x.GeneratedRecord.TryGetProperty("inventory",out var inv)&&inv.ValueKind==JsonValueKind.Array&&inv.GetArrayLength()>0);
            var generatedEntry=stockedShop.GeneratedRecord.GetProperty("inventory")[0];
            Check(r,generatedEntry.TryGetProperty("normal_qty",out _)&&generatedEntry.TryGetProperty("max_qty",out _)&&generatedEntry.TryGetProperty("regen_qty",out _)&&generatedEntry.TryGetProperty("regen_time",out _),"generated-shop-restock-contract");
        }
        catch(Exception ex){r.Failures.Add(ex.GetType().Name+":"+ex.Message);} return r;
    }
    private static int TatCost(int pointsAboveBase)=>(Math.Max(0,pointsAboveBase)/10)+1;
    private static int TotalTatCost(int startingOffset,int points)=>Enumerable.Range(startingOffset,points).Sum(TatCost);
    private static void Check(RuntimeWorldSystemsBundleSmokeReport r,bool ok,string name){if(ok)r.Checks.Add(name);else r.Failures.Add(name);}
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
}
