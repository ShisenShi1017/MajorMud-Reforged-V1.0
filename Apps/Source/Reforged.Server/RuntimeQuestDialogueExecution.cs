using System.Globalization;
using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeQuestDialogueDispatchResult(
    bool Matched,
    string? TriggerId,
    string? ScriptId,
    string? ClauseId,
    IReadOnlyList<string> Lines,
    IReadOnlyList<RuntimeQuestDialogueEvent> Events,
    string? FailureCode);

public sealed record RuntimeQuestDialogueEvent(
    string EventType,
    string? SubjectId,
    long Magnitude,
    IReadOnlyDictionary<string, string> Metadata);

public sealed class RuntimeQuestDialogueCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicNarrativeScript> _scripts;
    private readonly IReadOnlyDictionary<string, JsonElement> _triggers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> _triggersByNpc;

    private RuntimeQuestDialogueCatalog(
        IReadOnlyDictionary<string, RuntimeClassicNarrativeScript> scripts,
        IReadOnlyDictionary<string, JsonElement> triggers,
        IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> triggersByNpc)
    {
        _scripts = scripts;
        _triggers = triggers;
        _triggersByNpc = triggersByNpc;
    }

    public int ScriptCount => _scripts.Count;
    public int StageScriptCount => _scripts.Values.Count(script => script.StageIndex >= 0);
    public int NpcQuestEntryTriggerCount => _triggers.Count;
    public int LinkedStageScriptCount => _triggers.Values.Sum(trigger => ReadStringArray(trigger, "entry_script_ids").Count);

    public static RuntimeQuestDialogueCatalog Load(string projectRoot, string worldId) =>
        RuntimeClassicNarrativeCatalog.Load(projectRoot, worldId).DialogueCatalog;

    internal static RuntimeQuestDialogueCatalog FromValidatedRecords(
        IReadOnlyDictionary<string, RuntimeClassicNarrativeScript> scripts,
        IReadOnlyDictionary<string, JsonElement> triggers)
    {
        var triggerMap = triggers.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        var byNpc = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in triggerMap.Values)
        {
            var relationships = record.TryGetProperty("relationships", out var rel) ? rel : default;
            var npc = relationships.ValueKind == JsonValueKind.Object ? ReadText(relationships, "npc") : string.Empty;
            if (string.IsNullOrWhiteSpace(npc))
                throw new InvalidOperationException($"Validated quest trigger '{ReadText(record, "id")}' has no NPC relationship.");
            foreach (var scriptId in ReadStringArray(record, "entry_script_ids"))
                if (!scripts.ContainsKey(scriptId))
                    throw new InvalidOperationException($"Validated quest trigger '{ReadText(record, "id")}' references missing typed script '{scriptId}'.");
            if (!byNpc.TryGetValue(npc, out var list)) byNpc[npc] = list = [];
            list.Add(record.Clone());
        }

        return new RuntimeQuestDialogueCatalog(
            scripts,
            triggerMap,
            byNpc.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<JsonElement>)pair.Value.OrderBy(x => ReadText(x, "id"), StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase));
    }

    public RuntimeQuestDialogueDispatchResult Dispatch(string npcId, RuntimeQuestDialogueContext context, string? questId = null)
    {
        var triggers = _triggersByNpc.TryGetValue(npcId, out var rows) ? rows : Array.Empty<JsonElement>();
        if (!string.IsNullOrWhiteSpace(questId))
            triggers = triggers.Where(trigger => trigger.TryGetProperty("relationships", out var rel) &&
                rel.ValueKind == JsonValueKind.Object && ReadText(rel, "quest").Equals(questId, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (triggers.Count == 0)
            return new(false, null, null, null, [], [], "no_quest_entry_trigger");

        var failures = new List<string>();
        foreach (var trigger in triggers)
        foreach (var scriptId in ReadStringArray(trigger, "entry_script_ids"))
        {
            var result = ExecuteScript(_scripts[scriptId], context);
            if (result.Matched) return result with { TriggerId = ReadText(trigger, "id"), ScriptId = scriptId };
            if (!string.IsNullOrWhiteSpace(result.FailureCode)) failures.Add($"{scriptId}:{result.FailureCode}");
        }

        return new(false, null, null, null, [], [], failures.Count == 0
            ? "no_dialogue_clause_matched"
            : "no_dialogue_clause_matched:" + string.Join(",", failures.Take(3)));
    }

    private static RuntimeQuestDialogueDispatchResult ExecuteScript(RuntimeClassicNarrativeScript script, RuntimeQuestDialogueContext context)
    {
        var failures = new List<string>();
        foreach (var clause in script.Clauses.OrderBy(x => x.SourceLine))
        {
            var conditionFailure = EvaluateConditions(clause, context);
            if (conditionFailure is not null) { failures.Add(conditionFailure); continue; }

            var before = context.Capture();
            var lines = new List<string>();
            var events = new List<RuntimeQuestDialogueEvent>();
            try
            {
                foreach (var operation in clause.ExecutionSteps)
                    ExecuteOperation(operation, script, context, lines, events);
                return new(true, null, script.Id, clause.ClauseId, lines, events, null);
            }
            catch (Exception ex)
            {
                context.Restore(before);
                return new(false, null, script.Id, clause.ClauseId, [], [], "unsafe_execution:" + ex.Message);
            }
        }
        return new(false, null, script.Id, null, [], [], failures.Count == 0
            ? "no_compiled_clauses"
            : "conditions_failed:" + string.Join(",", failures.Take(3)));
    }

    private static string? EvaluateConditions(RuntimeClassicNarrativeClause clause, RuntimeQuestDialogueContext context)
    {
        foreach (var condition in clause.Conditions)
        {
            if (ConditionPasses(condition, context)) continue;
            var message = FailureMessage(condition);
            return string.IsNullOrWhiteSpace(message) ? condition.Opcode : $"{condition.Opcode}:{message}";
        }
        return null;
    }

    private static bool ConditionPasses(RuntimeClassicNarrativeOperation op, RuntimeQuestDialogueContext context) => op.Opcode switch
    {
        "checkability" => context.CheckAbility(Arg(op, 0), Arg(op, 1)),
        "testability" => context.TestAbility(Arg(op, 0), Arg(op, 1)),
        "failability" => context.FailAbility(Arg(op, 0)),
        "checkitem" => context.ItemCount(Arg(op, 0)) > 0,
        "failitem" => context.ItemCount(Arg(op, 0)) == 0,
        "minlevel" => context.Level >= Arg(op, 0),
        "maxlevel" => context.Level <= Arg(op, 0),
        "class" => context.ClassLegacyId == Arg(op, 0),
        "goodaligned" => context.Alignment >= Arg(op, 0),
        "evilaligned" => context.Alignment <= Arg(op, 0),
        "price" => context.Gold >= Arg(op, 0),
        "check" => context.Check(TextArg(op, 0)),
        _ => throw new InvalidOperationException($"unsupported condition opcode {op.Opcode}")
    };

    private static void ExecuteOperation(RuntimeClassicNarrativeOperation op, RuntimeClassicNarrativeScript script, RuntimeQuestDialogueContext context, List<string> lines, List<RuntimeQuestDialogueEvent> events)
    {
        switch (op.Opcode)
        {
            case "giveability": context.GiveAbility(Arg(op,0), Arg(op,1)); events.Add(Event("quest_flag_set", $"ability.{Arg(op,0)}", Arg(op,1), op)); break;
            case "addability": context.AddAbility(Arg(op,0), Arg(op,1)); events.Add(Event("quest_flag_added", $"ability.{Arg(op,0)}", Arg(op,1), op)); break;
            case "giveitem": context.GiveItem(Arg(op,0)); events.Add(Event("item_given", $"item.{Arg(op,0)}", 1, op)); break;
            case "takeitem": context.TakeItem(Arg(op,0)); events.Add(Event("item_taken", $"item.{Arg(op,0)}", 1, op)); break;
            case "addexp": context.Experience += Arg(op,0); events.Add(Event("experience_added", null, Arg(op,0), op)); break;
            case "addevil": context.Alignment -= Arg(op,0); events.Add(Event("alignment_evil_added", null, Arg(op,0), op)); break;
            case "message": lines.Add($"message.{Arg(op,0)}"); events.Add(Event("message", $"message.{Arg(op,0)}", Arg(op,0), op)); break;
            case "text": lines.Add($"textblock.{Arg(op,0)}"); events.Add(Event("text_branch", $"textblock.{Arg(op,0)}", Arg(op,0), op)); break;
            case "cast": context.ValidateWorldIntent("cast", $"spell.{Arg(op,0)}"); events.Add(Event("world_intent.cast_spell", $"spell.{Arg(op,0)}", Arg(op,0), op)); break;
            case "summon": context.ValidateWorldIntent("summon", $"npc.{Arg(op,0)}"); events.Add(Event("world_intent.summon_npc", $"npc.{Arg(op,0)}", Arg(op,0), op)); break;
            case "teleport": var roomId=$"room.map{Arg(op,1)}.{Arg(op,0)}"; context.ValidateWorldIntent("teleport",roomId); events.Add(Event("world_intent.teleport_character",roomId,Arg(op,0),op)); break;
            case "remoteaction": context.ValidateWorldIntent("remoteaction", string.Join(".", op.Arguments)); events.Add(Event("world_intent.remote_action", null, 0, op)); break;
            case "random": ExecuteRandomTable(op, script, context, lines, events); break;
            default: throw new InvalidOperationException($"unsupported execution opcode {op.Opcode}");
        }
    }

    private static void ExecuteRandomTable(RuntimeClassicNarrativeOperation op, RuntimeClassicNarrativeScript script, RuntimeQuestDialogueContext context, List<string> lines, List<RuntimeQuestDialogueEvent> events)
    {
        var textblockId = Arg(op, 0).ToString(CultureInfo.InvariantCulture);
        if (!script.RandomTables.TryGetValue(textblockId, out var table))
            throw new InvalidOperationException($"random table textblock.{textblockId} is not projected");
        var roll = context.RollRandomTextblock(textblockId);
        var selected = table.Clauses.FirstOrDefault(x => roll <= x.Threshold)
            ?? throw new InvalidOperationException($"random table textblock.{textblockId} does not cover roll {roll}");
        events.Add(new("random_textblock_selected", $"textblock.{textblockId}", roll, new Dictionary<string,string>
        {
            ["opcode"] = op.Opcode, ["raw"] = op.Raw, ["roll"] = roll.ToString(CultureInfo.InvariantCulture),
            ["threshold"] = selected.Threshold.ToString(CultureInfo.InvariantCulture), ["selection_model"] = table.SelectionModel
        }));
        foreach (var child in selected.Operations) ExecuteOperation(child, script, context, lines, events);
    }

    private static RuntimeQuestDialogueEvent Event(string type, string? subject, long magnitude, RuntimeClassicNarrativeOperation op) =>
        new(type, subject, magnitude, new Dictionary<string,string> { ["opcode"] = op.Opcode, ["raw"] = op.Raw, ["status"] = op.Status });

    private static int Arg(RuntimeClassicNarrativeOperation op, int index)
    {
        if (op.NumericArguments.Count > index) return checked((int)op.NumericArguments[index]);
        if (op.Arguments.Count > index && int.TryParse(op.Arguments[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return value;
        throw new InvalidOperationException($"missing numeric argument {index} for {op.Opcode}");
    }
    private static string TextArg(RuntimeClassicNarrativeOperation op, int index) => op.Arguments.Count > index ? op.Arguments[index] : string.Empty;
    private static string FailureMessage(RuntimeClassicNarrativeOperation op)
    {
        var required = op.Opcode is "checkability" or "testability" ? 2 : 1;
        return op.NumericArguments.Count > required ? $"message.{op.NumericArguments[required]}" : string.Empty;
    }
    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() : [];
    private static string ReadText(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : string.Empty;
}

public sealed class RuntimeQuestDialogueContext
{
    private readonly RuntimeItemCatalog _items;
    private readonly RuntimeItemEngine _itemEngine;
    private readonly RuntimeItemState _initialItemState;
    private readonly Dictionary<string, string> _conditions;
    private readonly Dictionary<int, int> _abilities;
    private readonly RuntimeQuestWorldIntentValidator? _worldIntents;

    public RuntimeQuestDialogueContext(RuntimeCharacterRecord character, RuntimeItemCatalog items, RuntimeQuestWorldIntentValidator? worldIntents = null)
    {
        _items = items;
        _worldIntents = worldIntents;
        Character = character;
        _conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        _abilities = _conditions
            .Where(pair => pair.Key.StartsWith("quest.ability.", StringComparison.OrdinalIgnoreCase) && int.TryParse(pair.Key["quest.ability.".Length..], out _) && int.TryParse(pair.Value, out _))
            .ToDictionary(pair => int.Parse(pair.Key["quest.ability.".Length..], CultureInfo.InvariantCulture), pair => int.Parse(pair.Value, CultureInfo.InvariantCulture));
        _initialItemState = character.ItemState ?? new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
        _itemEngine = new RuntimeItemEngine(items, _initialItemState.Items, _initialItemState.Corpses);
        Gold = _initialItemState.Gold;
        Experience = character.Experience;
        Alignment = ReadConditionInt(_conditions, "alignment", 0);
    }

    public RuntimeCharacterRecord Character { get; private set; }
    public int Level => Character.Level;
    public int ClassLegacyId => ParseLegacyNumber(Character.ClassId);
    public long Experience { get; set; }
    public long Gold { get; private set; }
    public int Alignment { get; set; }

    public int AbilityValue(int abilityId) => _abilities.TryGetValue(abilityId, out var value) ? value : 0;
    public int ItemCount(int itemId) => _itemEngine.Inventory(Character.CharacterId).Count(item => item.CanonicalItemId.Equals($"item.{itemId}", StringComparison.OrdinalIgnoreCase));
    public bool Check(string name) => _conditions.TryGetValue("check." + name, out var value) && bool.TryParse(value, out var parsed) && parsed;
    public int RollRandomTextblock(string textblockId)
    {
        if (_conditions.TryGetValue("runtime.random.roll." + textblockId, out var configured) &&
            int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var roll) &&
            roll >= 1 && roll <= 100)
        {
            return roll;
        }

        return Random.Shared.Next(1, 101);
    }
    public void ValidateWorldIntent(string intent, string target)
    {
        if (_worldIntents is null) return;
        _worldIntents.Validate(intent, target);
    }
    public bool CheckAbility(int abilityId, int expected) => RuntimeCharacterAbilityValues.Check(_abilities, abilityId, expected);
    public bool TestAbility(int abilityId, int minimum) => RuntimeCharacterAbilityValues.Test(_abilities, abilityId, minimum);
    public bool FailAbility(int abilityId) => RuntimeCharacterAbilityValues.Fail(_abilities, abilityId);
    public void GiveAbility(int abilityId, int value) => RuntimeCharacterAbilityValues.Give(_abilities, abilityId, value);
    public int AddAbility(int abilityId, int delta) => RuntimeCharacterAbilityValues.Add(_abilities, abilityId, delta);
    public void GiveItem(int itemId) => _itemEngine.Create($"item.{itemId}", RuntimeItemLocations.Character, Character.CharacterId, ownerId: Character.CharacterId);
    public void TakeItem(int itemId)
    {
        var item = _itemEngine.Inventory(Character.CharacterId).FirstOrDefault(x => x.CanonicalItemId.Equals($"item.{itemId}", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"takeitem missing item.{itemId}");
        _itemEngine.Remove(item.InstanceId);
    }

    public RuntimeQuestDialogueContextSnapshot Capture() =>
        new(new Dictionary<int, int>(_abilities), new Dictionary<string, string>(_conditions), _itemEngine.Snapshot(Gold, _initialItemState.ActiveItemState), Experience, Alignment);

    public void Restore(RuntimeQuestDialogueContextSnapshot snapshot)
    {
        _abilities.Clear();
        foreach (var pair in snapshot.Abilities) _abilities[pair.Key] = pair.Value;
        _conditions.Clear();
        foreach (var pair in snapshot.Conditions) _conditions[pair.Key] = pair.Value;
        _itemEngine.RestoreSnapshot(snapshot.ItemState);
        Experience = snapshot.Experience;
        Alignment = snapshot.Alignment;
    }

    public RuntimeCharacterRecord ToCharacterRecord()
    {
        RuntimeCharacterAbilityValues.Persist(_abilities, _conditions);
        _conditions["alignment"] = Alignment.ToString(CultureInfo.InvariantCulture);
        var itemState = _itemEngine.Snapshot(Gold, _initialItemState.ActiveItemState);
        return Character with { Experience = Experience, Conditions = _conditions, ItemState = itemState, UpdatedAtUtc = DateTimeOffset.UtcNow };
    }

    private static int ReadConditionInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static int ParseLegacyNumber(string id)
    {
        var dot = id.LastIndexOf('.');
        return int.TryParse(dot >= 0 ? id[(dot + 1)..] : id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}

public sealed record RuntimeQuestDialogueContextSnapshot(
    IReadOnlyDictionary<int, int> Abilities,
    IReadOnlyDictionary<string, string> Conditions,
    RuntimeItemState ItemState,
    long Experience,
    int Alignment);

public sealed class RuntimeQuestWorldIntentValidator
{
    private readonly HashSet<string> _spells;
    private readonly HashSet<string> _npcs;
    private readonly HashSet<string> _rooms;

    private RuntimeQuestWorldIntentValidator(HashSet<string> spells, HashSet<string> npcs, HashSet<string> rooms)
    {
        _spells = spells;
        _npcs = npcs;
        _rooms = rooms;
    }

    public static RuntimeQuestWorldIntentValidator Load(string projectRoot, string worldId)
    {
        var root = Path.Combine(projectRoot, "Data", worldId);
        return new(
            ReadIds(Path.Combine(root, "spells.json")),
            ReadIds(Path.Combine(root, "npcs.json"), "id", "npc_id"),
            ReadIds(Path.Combine(root, "rooms.json")));
    }

    public void Validate(string intent, string target)
    {
        switch (intent)
        {
            case "cast":
                if (!_spells.Contains(target)) throw new InvalidOperationException($"cast target {target} is not generated");
                break;
            case "summon":
                if (!_npcs.Contains(target)) throw new InvalidOperationException($"summon target {target} is not generated");
                break;
            case "teleport":
                if (!_rooms.Contains(target)) throw new InvalidOperationException($"teleport target {target} is not generated");
                break;
            case "remoteaction":
                if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("remoteaction arguments are missing");
                break;
            default:
                throw new InvalidOperationException($"unsupported world intent {intent}");
        }
    }

    private static HashSet<string> ReadIds(string path, params string[] names)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return ids;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return ids;
        var keys = names.Length == 0 ? new[] { "id" } : names;
        foreach (var record in records.EnumerateArray())
        {
            foreach (var key in keys)
            {
                var value = RuntimeQuestDialogueCatalogReadText(record, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    ids.Add(value);
                    break;
                }
            }
        }
        return ids;
    }

    private static string RuntimeQuestDialogueCatalogReadText(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : string.Empty;
}
