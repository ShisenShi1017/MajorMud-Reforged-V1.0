using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicQuestScriptTriggerClosureSmokeResult(
    bool Passed,
    int SourceQuestRecordCount,
    int SourcePendingQuestCount,
    int DecodedQuestFlowCount,
    int SourceStageScriptCount,
    int SourceQuestTriggerCount,
    int DecodedQuestOperationCount,
    int RemoteActionInvocationCount,
    int RemoteActionSignatureCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// CodeUpdate0365 closure gate for the Classic Quests / Scripts / Triggers owner.
/// The gate deliberately keeps the 2,100 analysis-only quest candidates in Librarium and
/// requires the generated Classic projection to contain only the 83 decoded executable
/// quest flows. It also composes the existing Bundle F execution audits and the complete
/// 139-invocation / 85-signature native remoteaction audit instead of inventing a second
/// narrative or remote-action engine.
/// </summary>
public static class RuntimeClassicQuestScriptTriggerClosureSmokeReport
{
    public static RuntimeClassicQuestScriptTriggerClosureSmokeResult Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string name, bool value)
        {
            if (value) checks.Add(name);
            else failures.Add(name);
        }

        var sourceQuestCount = 0;
        var pendingQuestCount = 0;
        var decodedQuestFlowCount = 0;
        var stageScriptCount = 0;
        var questTriggerCount = 0;
        var questOperationCount = 0;
        var remoteInvocationCount = 0;
        var remoteSignatureCount = 0;

        try
        {
            var questSource = FindLatest(projectRoot, "Librarium", "Objects", "quests", "quest_records_*.json");
            var sourceQuests = ReadRecords(questSource);
            sourceQuestCount = sourceQuests.Count;
            pendingQuestCount = sourceQuests.Count(record =>
                ReadText(ReadObject(record, "runtime_mapping"), "status").Equals("pending", StringComparison.OrdinalIgnoreCase));
            var sourceQuestFlowIds = sourceQuests
                .Where(record => ReadText(record, "classification").Equals("npc_textblock_quest_flow", StringComparison.OrdinalIgnoreCase))
                .Select(record => ReadText(record, "id"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var conditionCandidateCount = sourceQuests.Count(record =>
                ReadText(record, "classification").Equals("condition_candidate", StringComparison.OrdinalIgnoreCase));
            var keyGateCandidateCount = sourceQuests.Count(record =>
                ReadText(record, "classification").Equals("key_item_or_gate_candidate", StringComparison.OrdinalIgnoreCase));

            Check("source-quest-records-2183", sourceQuestCount == 2183);
            Check("source-pending-analysis-candidates-2100", pendingQuestCount == 2100);
            Check("source-condition-candidates-2000", conditionCandidateCount == 2000);
            Check("source-key-gate-candidates-100", keyGateCandidateCount == 100);
            Check("source-decoded-quest-flow-identities-83", sourceQuestFlowIds.Count == 83);
            Check("pending-plus-executable-partitions-source", pendingQuestCount + sourceQuestFlowIds.Count == sourceQuestCount);

            var decodedDirectory = FindLatestDirectoryWithFile(projectRoot,
                Path.Combine("Librarium", "SourceImports", "MMExplorerSQLData"), "TextblocksDecoded*", "quest_flows_decoded.json");
            var decodedQuestPath = Path.Combine(decodedDirectory, "quest_flows_decoded.json");
            var decodedQuestFlows = ReadRootArray(decodedQuestPath);
            decodedQuestFlowCount = decodedQuestFlows.Count;
            var decodedQuestIds = decodedQuestFlows
                .Select(record => ReadText(record, "id"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var decodedStageCount = 0;
            var allDecodedOperations = true;
            foreach (var flow in decodedQuestFlows)
            {
                foreach (var stage in ReadArray(flow, "stages"))
                {
                    decodedStageCount++;
                    foreach (var operation in ReadArray(stage, "operations"))
                    {
                        questOperationCount++;
                        allDecodedOperations &= ReadText(operation, "status").Equals("decoded", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            Check("decoded-quest-flow-count-83", decodedQuestFlowCount == 83);
            Check("decoded-quest-flow-ids-match-librarium", decodedQuestIds.SetEquals(sourceQuestFlowIds));
            Check("decoded-quest-stage-count-182", decodedStageCount == 182);
            Check("decoded-quest-operation-count-2616", questOperationCount == 2616);
            Check("all-quest-stage-operations-decoded", allDecodedOperations);

            var scriptSource = Path.Combine(projectRoot, "Librarium", "Objects", "scripts", "mmexplorer_textblock_stage_records.json");
            var sourceScripts = ReadRecords(scriptSource)
                .Where(record => ReadText(record, "script_type").Equals("mmexplorer_textblock_stage", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            stageScriptCount = sourceScripts.Length;
            var sourceScriptIds = sourceScripts
                .Select(record => ReadText(record, "id"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Check("source-stage-script-count-182", stageScriptCount == 182);
            Check("source-stage-script-ids-unique", sourceScriptIds.Count == stageScriptCount);
            Check("source-stage-script-operation-count-2616", sourceScripts.Sum(record => ReadArray(record, "operations").Count) == 2616);

            var triggerSource = Path.Combine(projectRoot, "Librarium", "Objects", "triggers", "mmexplorer_room_interaction_records.json");
            var sourceTriggers = ReadRecords(triggerSource)
                .Where(record => ReadText(record, "trigger_type").Equals("npc_quest_entry", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            questTriggerCount = sourceTriggers.Length;
            var linkedScriptIds = sourceTriggers
                .SelectMany(record => ReadStringArray(record, "entry_script_ids"))
                .ToArray();
            var triggerQuestIds = sourceTriggers
                .Select(record => ReadText(ReadObject(record, "relationships"), "quest"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Check("source-npc-quest-entry-trigger-count-83", questTriggerCount == 83);
            Check("source-trigger-linked-stage-count-182", linkedScriptIds.Length == 182);
            Check("source-trigger-linked-stage-set-exact", sourceScriptIds.SetEquals(linkedScriptIds));
            Check("source-trigger-quest-set-exact", triggerQuestIds.SetEquals(decodedQuestIds));

            var generated = RuntimeClassicNarrativeCatalog.Load(projectRoot, "Classic");
            Check("generated-quest-flows-83", generated.Summary.QuestFlowCount == 83);
            Check("generated-stage-scripts-182", generated.Summary.ExecutableScriptCount == 182);
            Check("generated-quest-entry-triggers-83", generated.Summary.QuestEntryTriggerCount == 83);
            Check("generated-connected-quest-flows-83", generated.Summary.ConnectedQuestFlowCount == 83);
            Check("generated-quest-operations-2616", generated.Summary.OperationCount == 2616);
            Check("generated-pending-quest-candidates-zero", generated.Summary.EvidenceOnlyQuestCount == 0);
            Check("generated-dialogue-linked-stage-scripts-182", generated.DialogueCatalog.LinkedStageScriptCount == 182);

            var interaction = RuntimeInteractionNarrativeSmokeReport.Run(projectRoot, "Classic");
            Check("interaction-narrative-smoke", interaction.Passed);
            Check("interaction-executable-quests-83", interaction.QuestCount == 83 && interaction.ExecutableQuestCount == 83);

            var remote = RuntimeNativeRemoteActionAudit.Run(projectRoot);
            remoteInvocationCount = remote.InvocationCount;
            remoteSignatureCount = remote.DistinctSignatureCount;
            Check("remoteaction-audit-pass", remote.Passed);
            Check("remoteaction-invocations-139", remote.InvocationCount == 139 && remote.StructurallyValidCount == 139);
            Check("remoteaction-signatures-85", remote.DistinctSignatureCount == 85);
            Check("remoteaction-executable-signatures-85", remote.ExecutableMutationCount == 85);
            Check("remoteaction-evidence-gated-zero", remote.EvidenceGatedCount == 0);

            // Compose the existing exhaustive Bundle F audits. These are owner-local tests;
            // this closure gate does not duplicate their semantics.
            Check("bundle-f1-authority", RuntimeBundleFPhaseF1AuthorityAudit.Run(projectRoot, "Classic").Passed);
            Check("bundle-f2-textblock-graph", RuntimeBundleFPhaseF2TextblockAudit.Run(projectRoot).Passed);
            Check("bundle-f3-opcode-dispatch", RuntimeBundleFPhaseF3OpcodeDispatchAudit.Run(projectRoot).Passed);
            Check("bundle-f4-semantic-projection", RuntimeBundleFPhaseF4SemanticAudit.Run(projectRoot).Passed);
            Check("bundle-f4-live-bindings", RuntimeBundleFPhaseF4LiveBindingAudit.Run().Passed);
            Check("bundle-f4-execution", RuntimeBundleFPhaseF4ExecutionAudit.Run(projectRoot).Passed);
            Check("bundle-f5-quest-roots", RuntimeBundleFPhaseF5QuestRootAudit.Run(projectRoot).Passed);
            Check("bundle-f5-eligibility", RuntimeBundleFPhaseF5EligibilityAudit.Run(projectRoot).Passed);
            Check("bundle-f5-condition-transaction", RuntimeBundleFPhaseF5ConditionTransactionAudit.Run().Passed);
            Check("bundle-f6-alignment", RuntimeBundleFPhaseF6AlignmentAudit.Run(projectRoot).Passed);
            Check("bundle-f7-gnome-merchant", RuntimeBundleFPhaseF7GnomeMerchantAudit.Run(projectRoot).Passed);
            Check("bundle-f7-scrolls", RuntimeBundleFPhaseF7ScrollAudit.Run(projectRoot).Passed);
            Check("bundle-f8-witchunter", RuntimeBundleFPhaseF8WitchunterAudit.Run(projectRoot).Passed);
            Check("bundle-f9-world-reward", RuntimeBundleFPhaseF9WorldRewardAudit.Run(projectRoot).Passed);
            Check("bundle-f10-npc-dialogue", RuntimeBundleFPhaseF10NpcDialogueAudit.Run(projectRoot).Passed);
            Check("bundle-f10-npc-dialogue-execution", RuntimeBundleFPhaseF10NpcDialogueExecutionAudit.Run(projectRoot).Passed);
            Check("bundle-f10-complete", RuntimeBundleFPhaseF10CompleteAudit.Run(projectRoot).Passed);
            Check("bundle-f11-command-runtime", RuntimeBundleFPhaseF11CommandAudit.Run(projectRoot).Passed);
            Check("bundle-f12-random-tables", RuntimeBundleFPhaseF12Audit.Run(projectRoot).Passed);
            Check("bundle-f13-event-missions", RuntimeBundleFPhaseF13Audit.Run(projectRoot).Passed);
            Check("bundle-f14-reward-execution", RuntimeBundleFPhaseF14Audit.Run(projectRoot).Passed);
            Check("bundle-f15-narrative-persistence", RuntimeBundleFPhaseF15Audit.Run(projectRoot).Passed);
        }
        catch (Exception ex)
        {
            failures.Add($"closure-exception:{ex.GetType().Name}:{ex.Message}");
        }

        return new(
            failures.Count == 0,
            sourceQuestCount,
            pendingQuestCount,
            decodedQuestFlowCount,
            stageScriptCount,
            questTriggerCount,
            questOperationCount,
            remoteInvocationCount,
            remoteSignatureCount,
            checks,
            failures);
    }

    private static string FindLatest(string root, params string[] parts)
    {
        var pattern = parts[^1];
        var directory = Path.Combine(new[] { root }.Concat(parts.Take(parts.Length - 1)).ToArray());
        var match = Directory.GetFiles(directory, pattern).OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        return match ?? throw new FileNotFoundException($"No source matched {pattern} in {directory}");
    }

    private static string FindLatestDirectoryWithFile(string root, string relativeRoot, string directoryPattern, string requiredFile)
    {
        var baseDirectory = Path.Combine(root, relativeRoot);
        var match = Directory.GetDirectories(baseDirectory, directoryPattern)
            .Where(directory => File.Exists(Path.Combine(directory, requiredFile)))
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return match ?? throw new DirectoryNotFoundException($"No {directoryPattern} directory with {requiredFile} was found under {baseDirectory}");
    }

    private static IReadOnlyList<JsonElement> ReadRecords(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Expected records array in {path}");
        return records.EnumerateArray().Select(record => record.Clone()).ToArray();
    }

    private static IReadOnlyList<JsonElement> ReadRootArray(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Expected root array in {path}");
        return document.RootElement.EnumerateArray().Select(record => record.Clone()).ToArray();
    }

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.Clone()).ToArray()
            : Array.Empty<JsonElement>();

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name) =>
        ReadArray(element, name).Select(item => item.ToString()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

    private static JsonElement ReadObject(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string ReadText(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : string.Empty;
}
