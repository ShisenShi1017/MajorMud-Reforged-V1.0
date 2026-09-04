using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicQuestNarrativeRoot(
    string QuestFamily,
    int RootTextblockId,
    string NpcName,
    string ActivationKind,
    IReadOnlyList<int> StageLevels,
    string RestrictionsOwner,
    string InventoryOwner,
    string NarrativeOwner,
    string PersistenceOwner,
    IReadOnlyList<string> Commands,
    string Evidence);

/// <summary>Canonical quest activation-root authority. It resolves decoded NPC command roots but delegates character, inventory, world, NPC, and persistence mutations to their established Bundle owners.</summary>
public sealed class RuntimeClassicQuestNarrativeAuthority
{
    private readonly IReadOnlyDictionary<int, RuntimeClassicQuestNarrativeRoot> _roots;
    private RuntimeClassicQuestNarrativeAuthority(IReadOnlyDictionary<int, RuntimeClassicQuestNarrativeRoot> roots) => _roots = roots;
    public int Count => _roots.Count;

    public static RuntimeClassicQuestNarrativeAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "quest_narrative_roots.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative Classic quest narrative roots are required.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var roots = new Dictionary<int, RuntimeClassicQuestNarrativeRoot>();
        foreach (var row in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var root = new RuntimeClassicQuestNarrativeRoot(
                row.GetProperty("quest_family").GetString() ?? string.Empty,
                row.GetProperty("root_textblock_id").GetInt32(),
                row.GetProperty("npc_name").GetString() ?? string.Empty,
                row.GetProperty("activation_kind").GetString() ?? string.Empty,
                row.GetProperty("stage_levels").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
                row.GetProperty("restrictions_owner").GetString() ?? string.Empty,
                row.GetProperty("inventory_owner").GetString() ?? string.Empty,
                row.GetProperty("narrative_owner").GetString() ?? string.Empty,
                row.GetProperty("persistence_owner").GetString() ?? string.Empty,
                row.GetProperty("commands").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray(),
                row.GetProperty("evidence").GetString() ?? string.Empty);
            if (root.RootTextblockId <= 0 || string.IsNullOrWhiteSpace(root.QuestFamily) || !roots.TryAdd(root.RootTextblockId, root))
                throw new InvalidDataException($"Duplicate or invalid quest narrative root {root.RootTextblockId}.");
        }
        return new(roots);
    }

    public RuntimeClassicQuestNarrativeRoot Require(int rootTextblockId) => _roots.TryGetValue(rootTextblockId, out var root) ? root : throw new KeyNotFoundException($"Quest narrative root {rootTextblockId} is not authoritative.");

    public RuntimeClassicTextblockExecutionPlan CreatePlan(RuntimeClassicTextblockGraphAuthority graph, int rootTextblockId, string playerInput)
    {
        var root = Require(rootTextblockId);
        if (!root.ActivationKind.Equals("npc_command_map", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Unsupported quest activation kind '{root.ActivationKind}'.");
        if (!graph.TryCreateExecutionPlan(rootTextblockId, playerInput, out var plan)) throw new InvalidOperationException($"No decoded quest command branch matched '{playerInput}' at root {rootTextblockId}.");
        return plan;
    }
}

public sealed record RuntimeBundleFPhaseF5QuestRootAuditReport(bool Passed, int RootCount, int CommandCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{ public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }); }

public static class RuntimeBundleFPhaseF5QuestRootAudit
{
    public static RuntimeBundleFPhaseF5QuestRootAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var authority = RuntimeClassicQuestNarrativeAuthority.Load(projectRoot);
        var graph = RuntimeClassicTextblockGraphAuthority.Load(projectRoot);
        var expected = new[] { 335, 366, 428, 2798 };
        foreach (var id in expected)
        {
            var root = authority.Require(id); var decoded = graph.Require(id);
            Check(decoded.ContentKind.Equals("command_map", StringComparison.OrdinalIgnoreCase), $"root-{id}-decoded-command-map");
            Check(root.Commands.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).SequenceEqual(decoded.Commands.Select(x => x.NormalizedKeyword).OrderBy(x => x, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase), $"root-{id}-command-set-exact");
            Check(root.NarrativeOwner == "BundleF", $"root-{id}-bundle-f-narrative-owner");
            Check(root.RestrictionsOwner == "BundleACharacter", $"root-{id}-bundle-a-restriction-owner");
            Check(root.InventoryOwner == "BundleCItemsEconomy", $"root-{id}-bundle-c-inventory-owner");
            Check(root.PersistenceOwner == "BundleGStorage", $"root-{id}-bundle-g-persistence-contract");
            Check(graph.MissingTextblockReferences(id).Count == 0, $"root-{id}-zero-reachable-textblock-disconnects");
        }
        Check(authority.Require(335).StageLevels.SequenceEqual(new[] { 10, 20, 30, 40, 50 }), "good-alignment-sequential-level-gates");
        Check(authority.Require(366).StageLevels.SequenceEqual(new[] { 10, 20, 30, 40, 50 }), "neutral-alignment-sequential-level-gates");
        Check(authority.Require(428).StageLevels.SequenceEqual(new[] { 10, 20, 30, 40, 50 }), "evil-alignment-sequential-level-gates");
        Check(authority.CreatePlan(graph, 335, "Commander Markus").MatchedTextblockId == 352, "good-command-routes-decoded-branch");
        Check(authority.CreatePlan(graph, 366, "CODE WORD").MatchedTextblockId == 368, "neutral-command-normalization-routes-decoded-branch");
        Check(authority.CreatePlan(graph, 428, "work").MatchedTextblockId == 430, "evil-work-routes-decoded-branch");
        Check(authority.CreatePlan(graph, 2798, "magma scroll").MatchedTextblockId == 2803, "jungle-gnome-scroll-routes-decoded-branch");
        return new(failures.Count == 0, authority.Count, expected.Sum(id => authority.Require(id).Commands.Count), failures.Count, checks, failures);
    }
}
