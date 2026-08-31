using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicAlignmentQuestPath(
    string Alignment,
    string NpcName,
    string NpcId,
    int RootTextblockId,
    IReadOnlyList<int> RequiredLevels,
    string CharacterOwner,
    string InventoryOwner,
    string NarrativeOwner,
    string PersistenceOwner);

/// <summary>
/// Bundle F alignment narrative authority. It selects the decoded Good/Neutral/Evil root,
/// applies the sequential stage preflight, and delegates the selected decoded operation plan
/// to the existing atomic narrative coordinator. It does not own character, inventory, or storage state.
/// </summary>
public sealed class RuntimeClassicAlignmentQuestNarrativeAuthority
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicAlignmentQuestPath> _paths;
    private readonly RuntimeClassicQuestNarrativeAuthority _roots;
    private readonly RuntimeClassicQuestEligibilityAuthority _eligibility;

    private RuntimeClassicAlignmentQuestNarrativeAuthority(
        IReadOnlyDictionary<string, RuntimeClassicAlignmentQuestPath> paths,
        RuntimeClassicQuestNarrativeAuthority roots)
    {
        _paths = paths;
        _roots = roots;
        _eligibility = new RuntimeClassicQuestEligibilityAuthority(roots);
    }

    public static RuntimeClassicAlignmentQuestNarrativeAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "alignment_quest_narrative_paths.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Authoritative Classic alignment narrative paths are required.", path);

        var roots = RuntimeClassicQuestNarrativeAuthority.Load(projectRoot);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var records = new Dictionary<string, RuntimeClassicAlignmentQuestPath>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var record = new RuntimeClassicAlignmentQuestPath(
                row.GetProperty("alignment").GetString() ?? string.Empty,
                row.GetProperty("npc_name").GetString() ?? string.Empty,
                row.GetProperty("npc_id").GetString() ?? string.Empty,
                row.GetProperty("root_textblock_id").GetInt32(),
                row.GetProperty("required_levels").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
                row.GetProperty("character_owner").GetString() ?? string.Empty,
                row.GetProperty("inventory_owner").GetString() ?? string.Empty,
                row.GetProperty("narrative_owner").GetString() ?? string.Empty,
                row.GetProperty("persistence_owner").GetString() ?? string.Empty);

            if (string.IsNullOrWhiteSpace(record.Alignment) || !records.TryAdd(record.Alignment, record))
                throw new InvalidDataException("Duplicate or invalid alignment quest path.");
            var root = roots.Require(record.RootTextblockId);
            if (!record.RequiredLevels.SequenceEqual(root.StageLevels))
                throw new InvalidDataException($"Alignment path '{record.Alignment}' disagrees with decoded root stage levels.");
        }
        return new(records, roots);
    }

    public RuntimeClassicAlignmentQuestPath Require(string alignment) =>
        _paths.TryGetValue(alignment, out var value)
            ? value
            : throw new KeyNotFoundException($"Alignment quest path '{alignment}' is not authoritative.");

    public RuntimeClassicQuestEligibilityDecision Evaluate(string alignment, RuntimeClassicQuestEligibilityState state) =>
        _eligibility.Evaluate(Require(alignment).RootTextblockId, state);

    public RuntimeClassicNarrativeExecutionResult Execute(
        RuntimeClassicTextblockGraphAuthority graph,
        RuntimeClassicNarrativeExecutionCoordinator coordinator,
        string alignment,
        string playerInput,
        RuntimeClassicQuestEligibilityState state,
        RuntimeClassicNarrativeExecutionContext context)
    {
        var path = Require(alignment);
        if (context.RootTextblockId != path.RootTextblockId)
            throw new InvalidOperationException("Alignment execution context does not match the authoritative decoded root.");
        return _eligibility.Execute(graph, coordinator, path.RootTextblockId, playerInput, state, context);
    }
}

public sealed record RuntimeBundleFPhaseF6AlignmentAuditReport(
    bool Passed,
    int PathCount,
    int GateCount,
    int DisconnectCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF6AlignmentAudit
{
    public static RuntimeBundleFPhaseF6AlignmentAuditReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool value, string name) { if (value) checks.Add(name); else failures.Add(name); }

        var authority = RuntimeClassicAlignmentQuestNarrativeAuthority.Load(projectRoot);
        var expected = new[]
        {
            (Alignment: "Good", Npc: "Chancellor Annotated", NpcId: "npc.244", Root: 335),
            (Alignment: "Neutral", Npc: "Grey Lord", NpcId: "npc.251", Root: 366),
            (Alignment: "Evil", Npc: "Balthazar", NpcId: "npc.263", Root: 428)
        };
        var emptyAbilities = (IReadOnlySet<int>)new HashSet<int>();
        var emptyInventory = (IReadOnlyDictionary<int, int>)new Dictionary<int, int>();
        RuntimeClassicQuestEligibilityState State(int level, int stage, string alignment) =>
            new(level, stage, "audit", "audit", alignment, emptyAbilities, emptyInventory);

        foreach (var item in expected)
        {
            var path = authority.Require(item.Alignment);
            Check(path.NpcName.Equals(item.Npc, StringComparison.OrdinalIgnoreCase), $"{item.Alignment}-authoritative-npc-name");
            Check(path.RootTextblockId == item.Root, $"{item.Alignment}-authoritative-root");
            Check(path.RequiredLevels.SequenceEqual(new[] { 10, 20, 30, 40, 50 }), $"{item.Alignment}-sequential-level-gates");
            Check(path.CharacterOwner == "BundleACharacter", $"{item.Alignment}-bundle-a-character-owner");
            Check(path.InventoryOwner == "BundleCItemsEconomy", $"{item.Alignment}-bundle-c-inventory-owner");
            Check(path.NarrativeOwner == "BundleF", $"{item.Alignment}-bundle-f-narrative-owner");
            Check(path.PersistenceOwner == "BundleGStorage", $"{item.Alignment}-bundle-g-persistence-contract");
            for (var stage = 0; stage < 5; stage++)
            {
                var required = path.RequiredLevels[stage];
                Check(!authority.Evaluate(item.Alignment, State(required - 1, stage, item.Alignment)).Eligible,
                    $"{item.Alignment}-stage-{stage + 1}-rejects-below-{required}");
                Check(authority.Evaluate(item.Alignment, State(required, stage, item.Alignment)).Eligible,
                    $"{item.Alignment}-stage-{stage + 1}-accepts-{required}");
            }
            Check(authority.Evaluate(item.Alignment, State(99, 5, item.Alignment)).AlreadyCompleted,
                $"{item.Alignment}-duplicate-completion-rejected");
        }

        return new(failures.Count == 0, expected.Length, expected.Length * 5, failures.Count, checks, failures);
    }
}
