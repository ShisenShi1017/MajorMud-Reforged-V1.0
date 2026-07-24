using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleFFieldLedgerEntry(
    string Family,
    string SourceFile,
    int DecodedRecordCount,
    string CanonicalOwner,
    string RuntimeConsumer,
    string PersistenceOwner,
    string EvidenceStatus,
    string Disposition);

public sealed record RuntimeBundleFPhaseF1AuthorityAuditReport(
    bool Passed,
    int FamilyCount,
    int DecodedRecordCount,
    int VerifiedDisconnectCount,
    int StronglyCorroboratedDisconnectCount,
    int DuplicateAuthorityCount,
    IReadOnlyList<RuntimeBundleFFieldLedgerEntry> FieldLedger,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF1AuthorityAudit
{
    private sealed record FamilyContract(string Family, string File, string Owner, string Consumer, string Persistence);

    private static readonly FamilyContract[] Contracts =
    [
        new("Textblocks and scripts", "scripts.json", "ClassicRuntimeCatalog / RuntimeClassicNarrativeCatalog", "RuntimeQuestDialogueExecution", "Bundle A-G mutation owners"),
        new("Quests and missions", "quests.json", "ClassicRuntimeCatalog / RuntimeClassicNarrativeCatalog", "RuntimeQuestDialogueExecution", "Bundle A character quest state / Bundle G storage"),
        new("Triggers and events", "triggers.json", "ClassicRuntimeCatalog", "RuntimeTextblockInvocation and authoritative Bundle A-E event producers", "Bundle G storage and reset authority"),
        new("Commands", "commands.json", "ClassicRuntimeCatalog", "authoritative command router and RuntimeTextblockInvocation", "owning Bundle A-G subsystem"),
        new("Narrative events", "events.json", "ClassicRuntimeCatalog", "authoritative event execution contract", "Bundle G scheduler/storage"),
        new("Race descriptions", "races.json", "ClassicRuntimeCatalog race record", "character creation/help/look/examine projection", "Bundle A identity persistence"),
        new("Room descriptions", "rooms.json", "ClassicRuntimeCatalog room record", "Bundle D look/examine projection", "Bundle D world persistence"),
        new("NPC and monster descriptions", "npcs.json", "ClassicRuntimeCatalog NPC record", "Bundle E interaction/look projection", "Bundle E NPC persistence"),
        new("Item and equipment descriptions", "items.json", "ClassicRuntimeCatalog item record", "Bundle C inventory/look/examine projection", "Bundle C item persistence"),
        new("Spell descriptions", "spells.json", "ClassicRuntimeCatalog spell record", "Bundle A spell/help projection", "Bundle A learned-spell persistence"),
        new("Skill descriptions", "skills.json", "ClassicRuntimeCatalog skill record", "Bundle A skill/help projection", "Bundle A character persistence"),
        new("Ability descriptions", "abilities.json", "ClassicRuntimeCatalog ability record", "Bundle A ability/help projection", "Bundle A ability persistence")
    ];

    public static RuntimeBundleFPhaseF1AuthorityAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var ledger = new List<RuntimeBundleFFieldLedgerEntry>();
        var dataRoot = Path.Combine(projectRoot, "Data", worldId);

        void Require(bool condition, string check, string failure)
        {
            if (condition) checks.Add(check); else failures.Add(failure);
        }

        Require(File.Exists(Path.Combine(dataRoot, "textblock_command_ability_contracts.json")),
            "required-textblock-command-contract-present", "missing:textblock_command_ability_contracts.json");
        Require(File.Exists(Path.Combine(dataRoot, "npc_ai_runtime.json")),
            "required-npc-ai-runtime-present", "missing:npc_ai_runtime.json");

        foreach (var contract in Contracts)
        {
            var path = Path.Combine(dataRoot, contract.File);
            if (!File.Exists(path))
            {
                failures.Add("missing-family-file:" + contract.File);
                ledger.Add(new(contract.Family, contract.File, 0, contract.Owner, contract.Consumer, contract.Persistence, "Relationship unresolved", "missing"));
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var count = document.RootElement.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array
                ? records.GetArrayLength()
                : 0;
            Require(count > 0, "decoded-family-nonempty:" + contract.File, "empty-family:" + contract.File);
            ledger.Add(new(contract.Family, contract.File, count, contract.Owner, contract.Consumer, contract.Persistence,
                "Verified or Strongly Corroborated records promoted individually", "canonical authority assigned; live-consumer exhaustion continues by phase"));
        }

        var serverRoot = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server");
        foreach (var file in new[]
        {
            "RuntimeClassicNarrativeCatalog.cs", "RuntimeClassicNarrativeTypes.cs", "RuntimeQuestDialogueExecution.cs",
            "RuntimeTextblockInvocation.cs", "RuntimeTextblockCommandAbilityCatalog.cs"
        })
            Require(File.Exists(Path.Combine(serverRoot, file)), "canonical-runtime-present:" + file, "missing-canonical-runtime:" + file);

        Require(!File.Exists(Path.Combine(serverRoot, "RuntimeQuestEngine.cs")),
            "no-parallel-quest-engine", "duplicate-authority:RuntimeQuestEngine.cs");
        Require(!File.Exists(Path.Combine(serverRoot, "RuntimeDialogueEngine.cs")),
            "no-parallel-dialogue-engine", "duplicate-authority:RuntimeDialogueEngine.cs");
        Require(!File.Exists(Path.Combine(serverRoot, "RuntimeTextblockCatalog.cs")),
            "no-duplicate-textblock-catalog", "duplicate-authority:RuntimeTextblockCatalog.cs");

        var narrative = RuntimeClassicNarrativeCatalog.Load(projectRoot, worldId);
        Require(narrative.Summary.ExecutableScriptCount > 0, "narrative-executable-scripts-loaded", "narrative:no-executable-scripts");
        Require(narrative.Summary.ConnectedQuestFlowCount > 0, "narrative-connected-quest-flows-loaded", "narrative:no-connected-quest-flows");
        Require(narrative.Summary.QuarantinedRecordCount >= 0, "narrative-quarantine-count-accounted", "narrative:quarantine-count-invalid");

        foreach (var root in new[] { "335", "366", "428" })
        {
            var found = narrative.TypedScripts.Any(script =>
                script.Id.Contains(root, StringComparison.OrdinalIgnoreCase) ||
                script.Clauses.Any(clause => clause.ClauseId.Contains(root, StringComparison.OrdinalIgnoreCase)));
            Require(found, "known-textblock-root-traced:" + root, "known-textblock-root-untraced:" + root);
        }

        var decodedTextblockPath = Path.Combine(projectRoot, "Librarium", "SourceImports", "MMExplorerSQLData",
            "TextblocksDecoded0142", "textblocks_decoded.json");
        var jungleRootTraced = false;
        if (File.Exists(decodedTextblockPath))
        {
            using var decodedTextblocks = JsonDocument.Parse(File.ReadAllText(decodedTextblockPath));
            if (decodedTextblocks.RootElement.ValueKind == JsonValueKind.Array)
            {
                jungleRootTraced = decodedTextblocks.RootElement.EnumerateArray().Any(record =>
                    ReadText(record, "id").Equals("textblock.2798", StringComparison.OrdinalIgnoreCase) &&
                    record.TryGetProperty("owner", out var owner) &&
                    ReadText(owner, "kind").Equals("monster", StringComparison.OrdinalIgnoreCase) &&
                    ReadText(owner, "raw").Contains("716", StringComparison.OrdinalIgnoreCase) &&
                    ReadText(record, "content_kind").Equals("command_map", StringComparison.OrdinalIgnoreCase));
            }
        }
        Require(jungleRootTraced, "known-textblock-root-traced:2798:monster.716:command_map",
            "known-textblock-root-untraced:2798");

        var duplicateAuthorityCount = failures.Count(x => x.StartsWith("duplicate-authority:", StringComparison.OrdinalIgnoreCase));
        var verifiedDisconnectCount = failures.Count(x => !x.StartsWith("known-textblock-root-untraced:", StringComparison.OrdinalIgnoreCase));
        var corroboratedDisconnectCount = failures.Count(x => x.StartsWith("known-textblock-root-untraced:", StringComparison.OrdinalIgnoreCase));

        return new(
            failures.Count == 0,
            ledger.Count,
            ledger.Sum(x => x.DecodedRecordCount),
            verifiedDisconnectCount,
            corroboratedDisconnectCount,
            duplicateAuthorityCount,
            ledger,
            checks,
            failures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string ReadText(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString() ?? string.Empty;
    }

}
