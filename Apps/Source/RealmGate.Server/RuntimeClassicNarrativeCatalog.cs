using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicNarrativeSummary(
    int ContractCount,
    int ExecutableScriptCount,
    int QuestFlowCount,
    int EvidenceOnlyQuestCount,
    int QuestEntryTriggerCount,
    int OperationCount,
    int ConnectedQuestFlowCount,
    int QuarantinedRecordCount);

public sealed class RuntimeClassicNarrativeCatalog
{
    private readonly IReadOnlyDictionary<string, JsonElement> _scripts;
    private readonly IReadOnlyDictionary<string, JsonElement> _questFlows;
    private readonly IReadOnlyDictionary<string, JsonElement> _questTriggers;
    private readonly IReadOnlyDictionary<string, RuntimeClassicNarrativeScript> _typedScripts;
    private readonly IReadOnlySet<string> _allowedOpcodes;

    private RuntimeClassicNarrativeCatalog(
        IReadOnlyDictionary<string, JsonElement> scripts,
        IReadOnlyDictionary<string, JsonElement> questFlows,
        IReadOnlyDictionary<string, JsonElement> questTriggers,
        IReadOnlyDictionary<string, RuntimeClassicNarrativeScript> typedScripts,
        IReadOnlySet<string> allowedOpcodes,
        RuntimeClassicNarrativeSummary summary)
    {
        _scripts = scripts;
        _questFlows = questFlows;
        _questTriggers = questTriggers;
        _typedScripts = typedScripts;
        _allowedOpcodes = allowedOpcodes;
        DialogueCatalog = RuntimeQuestDialogueCatalog.FromValidatedRecords(typedScripts, questTriggers);
        Summary = summary;
    }

    public RuntimeClassicNarrativeSummary Summary { get; }
    public RuntimeQuestDialogueCatalog DialogueCatalog { get; }
    public IReadOnlyCollection<string> ScriptIds => _scripts.Keys.ToArray();
    public IReadOnlyCollection<string> QuestFlowIds => _questFlows.Keys.ToArray();
    public IReadOnlyCollection<RuntimeClassicNarrativeScript> TypedScripts => _typedScripts.Values.ToArray();
    public bool TryGetTypedScript(string id, out RuntimeClassicNarrativeScript script) => _typedScripts.TryGetValue(id, out script!);

    public static RuntimeClassicNarrativeCatalog Load(string projectRoot, string worldId)
    {
        var dataRoot = Path.Combine(projectRoot, "Data", worldId);
        var contracts = ReadRecords(Path.Combine(dataRoot, "textblock_command_ability_contracts.json"));
        var scriptsAll = ReadRecords(Path.Combine(dataRoot, "scripts.json"));
        var questsAll = ReadRecords(Path.Combine(dataRoot, "quests.json"));
        var triggersAll = ReadRecords(Path.Combine(dataRoot, "triggers.json"));

        var opcodeContract = contracts.SingleOrDefault(x => ReadText(x, "id") == "contract.textblock.opcode_execution");
        if (opcodeContract.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Textblock opcode execution contract is missing.");

        var allowedOpcodes = ReadArray(opcodeContract, "opcodes")
            .Select(x => ReadText(x, "opcode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowedOpcodes.Count == 0)
            throw new InvalidOperationException("Textblock opcode execution contract contains no opcodes.");

        var scripts = scriptsAll
            .Where(x => ReadText(x, "script_type") == "mmexplorer_textblock_stage")
            .ToDictionary(x => ReadRequiredId(x, "script"), x => x.Clone(), StringComparer.OrdinalIgnoreCase);
        var questFlows = questsAll
            .Where(x => ReadText(x, "classification") == "npc_textblock_quest_flow")
            .ToDictionary(x => ReadRequiredId(x, "quest flow"), x => x.Clone(), StringComparer.OrdinalIgnoreCase);
        var questTriggers = triggersAll
            .Where(x => ReadText(x, "trigger_type") == "npc_quest_entry")
            .ToDictionary(x => ReadRequiredId(x, "quest entry trigger"), x => x.Clone(), StringComparer.OrdinalIgnoreCase);

        var typedScripts = scripts.ToDictionary(
            pair => pair.Key,
            pair => RuntimeClassicNarrativeTypedProjection.ParseScript(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        var operationCount = 0;
        foreach (var script in scripts.Values)
        {
            var relationships = RequireObject(script, "relationships", ReadText(script, "id"));
            var questId = ReadText(relationships, "quest");
            var npcId = ReadText(relationships, "npc");
            if (!questFlows.ContainsKey(questId))
                throw new InvalidOperationException($"Script '{ReadText(script, "id")}' references missing quest flow '{questId}'.");
            if (string.IsNullOrWhiteSpace(npcId))
                throw new InvalidOperationException($"Script '{ReadText(script, "id")}' has no NPC relationship.");

            foreach (var op in ReadArray(script, "operations"))
            {
                operationCount++;
                var opcode = ReadText(op, "opcode");
                if (!allowedOpcodes.Contains(opcode))
                    throw new InvalidOperationException($"Script '{ReadText(script, "id")}' uses opcode '{opcode}' outside the authoritative contract.");
                if (!ReadText(op, "status").Equals("decoded", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Script '{ReadText(script, "id")}' contains a non-decoded operation.");
            }
        }

        var connectedFlows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var trigger in questTriggers.Values)
        {
            var rel = RequireObject(trigger, "relationships", ReadText(trigger, "id"));
            var questId = ReadText(rel, "quest");
            var npcId = ReadText(rel, "npc");
            if (!questFlows.ContainsKey(questId))
                throw new InvalidOperationException($"Quest trigger '{ReadText(trigger, "id")}' references missing quest flow '{questId}'.");

            var scriptIds = ReadStringArray(trigger, "entry_script_ids");
            if (scriptIds.Count == 0)
                throw new InvalidOperationException($"Quest trigger '{ReadText(trigger, "id")}' has no entry scripts.");
            foreach (var scriptId in scriptIds)
            {
                if (!scripts.TryGetValue(scriptId, out var script))
                    throw new InvalidOperationException($"Quest trigger '{ReadText(trigger, "id")}' references missing script '{scriptId}'.");
                var scriptRel = RequireObject(script, "relationships", scriptId);
                if (!ReadText(scriptRel, "quest").Equals(questId, StringComparison.OrdinalIgnoreCase) ||
                    !ReadText(scriptRel, "npc").Equals(npcId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Quest trigger '{ReadText(trigger, "id")}' and script '{scriptId}' disagree on quest/NPC ownership.");
            }
            connectedFlows.Add(questId);
        }

        var missingTriggerFlows = questFlows.Keys.Where(id => !connectedFlows.Contains(id)).ToArray();
        if (missingTriggerFlows.Length > 0)
            throw new InvalidOperationException($"Quest flows missing live NPC entry triggers: {string.Join(", ", missingTriggerFlows.Take(8))}");

        var evidenceOnlyQuestCount = questsAll.Count - questFlows.Count;
        var quarantined = evidenceOnlyQuestCount + scriptsAll.Count - scripts.Count;
        var summary = new RuntimeClassicNarrativeSummary(
            contracts.Count,
            scripts.Count,
            questFlows.Count,
            evidenceOnlyQuestCount,
            questTriggers.Count,
            operationCount,
            connectedFlows.Count,
            quarantined);

        return new RuntimeClassicNarrativeCatalog(scripts, questFlows, questTriggers, typedScripts, allowedOpcodes, summary);
    }

    public bool ContainsScript(string id) => _scripts.ContainsKey(id);
    public bool ContainsQuestFlow(string id) => _questFlows.ContainsKey(id);
    public bool IsAllowedOpcode(string opcode) => _allowedOpcodes.Contains(opcode);

    private static List<JsonElement> ReadRecords(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Generated narrative data file was not found.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("record_parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            var output = new List<JsonElement>();
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, relative.Replace('/', Path.DirectorySeparatorChar));
                output.AddRange(ReadRecords(partPath));
            }

            return output;
        }

        if (!root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Generated narrative data has no records array: {path}");
        return records.EnumerateArray().Select(x => x.Clone()).ToList();
    }

    private static IEnumerable<JsonElement> ReadArray(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => x.Clone())
            : Array.Empty<JsonElement>();

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name) =>
        ReadArray(element, name).Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

    private static string ReadText(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString() : string.Empty;

    private static string ReadRequiredId(JsonElement element, string kind)
    {
        var id = ReadText(element, "id");
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException($"Generated {kind} record has no id.");
        return id;
    }

    private static JsonElement RequireObject(JsonElement element, string name, string owner)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Generated record '{owner}' has no {name} object.");
        return value;
    }
}
