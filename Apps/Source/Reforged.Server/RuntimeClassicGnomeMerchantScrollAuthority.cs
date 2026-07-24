using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicGnomeMerchantItemRequirement(int ItemId, int Quantity, string Name);
public sealed record RuntimeClassicGnomeMerchantOffering(
    string Keyword,
    string SpellKeyword,
    int DialogueTextblockId,
    int? ActionTextblockId,
    int ClassLegacyId,
    IReadOnlyList<RuntimeClassicGnomeMerchantItemRequirement> RequiredItems,
    int RewardItemId,
    string RewardName,
    string Evidence);
public sealed record RuntimeClassicGnomeMerchantTurnInDecision(bool Passed, string? Failure, RuntimeClassicTextblockExecutionPlan? Plan);

/// <summary>
/// Canonical, data-driven Bundle F authority for the complete verified/strongly-corroborated
/// Gnome Merchant class-scroll turn-in family. Bundle F selects and composes the plan; Bundle C
/// remains the sole inventory-removal, capacity, grant, and scroll-use-level owner.
/// </summary>
public sealed class RuntimeClassicGnomeMerchantScrollAuthority
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicGnomeMerchantOffering> _offerings;
    public IReadOnlyCollection<RuntimeClassicGnomeMerchantOffering> Offerings => _offerings.Values.ToArray();
    public int SuccessTextblockId { get; }
    public int FailureTextblockId { get; }

    private RuntimeClassicGnomeMerchantScrollAuthority(
        IReadOnlyDictionary<string, RuntimeClassicGnomeMerchantOffering> offerings,
        int successTextblockId,
        int failureTextblockId)
    {
        _offerings = offerings;
        SuccessTextblockId = successTextblockId;
        FailureTextblockId = failureTextblockId;
    }

    public static RuntimeClassicGnomeMerchantScrollAuthority Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "Data", "Classic", "gnome_merchant_scroll_offerings.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Authoritative Gnome Merchant offering catalog is required.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.GetProperty("npc_legacy_id").GetInt32() != 716 || root.GetProperty("root_textblock_id").GetInt32() != 2798)
            throw new InvalidDataException("Gnome Merchant offering identity mismatch.");
        if (root.GetProperty("receipt_level_gate").ValueKind != JsonValueKind.Null)
            throw new InvalidDataException("Scroll receipt must not have an invented level gate.");
        var rows = new List<RuntimeClassicGnomeMerchantOffering>();
        foreach (var row in root.GetProperty("offerings").EnumerateArray())
        {
            var items = row.GetProperty("required_items").EnumerateArray().Select(x =>
                new RuntimeClassicGnomeMerchantItemRequirement(x.GetProperty("item_id").GetInt32(), x.GetProperty("quantity").GetInt32(), x.GetProperty("name").GetString() ?? string.Empty)).ToArray();
            rows.Add(new(
                row.GetProperty("keyword").GetString() ?? string.Empty,
                row.GetProperty("spell_keyword").GetString() ?? string.Empty,
                row.GetProperty("dialogue_textblock_id").GetInt32(),
                row.GetProperty("action_textblock_id").ValueKind == JsonValueKind.Null ? null : row.GetProperty("action_textblock_id").GetInt32(),
                row.GetProperty("class_legacy_id").GetInt32(), items,
                row.GetProperty("reward_item_id").GetInt32(), row.GetProperty("reward_name").GetString() ?? string.Empty,
                row.GetProperty("evidence").GetString() ?? string.Empty));
        }
        if (rows.Count != 6 || rows.Any(x => string.IsNullOrWhiteSpace(x.Keyword) || x.RequiredItems.Count == 0 || x.RequiredItems.Any(i => i.Quantity <= 0)))
            throw new InvalidDataException("The complete six-offering Gnome Merchant catalog is required.");
        if (rows.Select(x => x.Keyword).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Count || rows.Select(x => x.RewardItemId).Distinct().Count() != rows.Count)
            throw new InvalidDataException("Duplicate Gnome Merchant keyword or reward.");
        return new(rows.ToDictionary(x => x.Keyword, StringComparer.OrdinalIgnoreCase), root.GetProperty("success_textblock_id").GetInt32(), root.GetProperty("failure_textblock_id").GetInt32());
    }

    public bool TryGetOffering(string input, out RuntimeClassicGnomeMerchantOffering offering) =>
        _offerings.TryGetValue((input ?? string.Empty).Trim(), out offering!);

    public RuntimeClassicGnomeMerchantTurnInDecision CreateTurnInPlan(
        string input,
        int classLegacyId,
        IReadOnlyDictionary<int, int> inventoryCounts)
    {
        if (!TryGetOffering(input, out var offering)) return new(false, "Unknown Gnome Merchant scroll request.", null);
        if (classLegacyId != offering.ClassLegacyId) return new(false, $"Class {offering.ClassLegacyId} is required.", null);
        foreach (var item in offering.RequiredItems)
            if (!inventoryCounts.TryGetValue(item.ItemId, out var count) || count < item.Quantity)
                return new(false, $"Missing {item.Quantity} x item {item.ItemId}.", null);

        var operations = new List<RuntimeClassicTextblockOperation>();
        var sequence = 1;
        operations.Add(Op(sequence++, "class", offering.ClassLegacyId));
        foreach (var item in offering.RequiredItems)
            for (var n = 0; n < item.Quantity; n++) operations.Add(Op(sequence++, "takeitem", item.ItemId, FailureTextblockId));
        operations.Add(Op(sequence++, "giveitem", offering.RewardItemId));
        operations.Add(Op(sequence, "text", SuccessTextblockId));
        return new(true, null, new RuntimeClassicTextblockExecutionPlan(2798, offering.ActionTextblockId ?? offering.DialogueTextblockId, offering.DialogueTextblockId, offering.Keyword, operations, new[] { SuccessTextblockId }, null));
    }

    private static RuntimeClassicTextblockOperation Op(int sequence, string opcode, params int[] args) =>
        new(1, sequence, string.Join(' ', new[] { opcode }.Concat(args.Select(x => x.ToString()))), opcode,
            opcode == "class" ? "condition" : opcode == "text" ? "branch" : "effect", "verified",
            args.Select(x => x.ToString()).ToArray(), args,
            opcode == "text" ? args[0] : null,
            opcode is "takeitem" or "giveitem" ? args[0] : null, null, null, null, null, null);
}

public sealed record RuntimeBundleFPhaseF7ScrollAuditReport(bool Passed, int OfferingCount, int RequirementUnitCount, int DisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF7ScrollAudit
{
    public static RuntimeBundleFPhaseF7ScrollAuditReport Run(string projectRoot)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) { if (ok) checks.Add(name); else failures.Add(name); }
        var authority = RuntimeClassicGnomeMerchantScrollAuthority.Load(projectRoot);
        Check(authority.Offerings.Count == 6, "all-six-decoded-scroll-offerings-projected");
        Check(authority.Offerings.Count(x => x.ClassLegacyId == 12) == 3, "mage-branches-remain-distinct");
        Check(authority.Offerings.Count(x => x.ClassLegacyId == 13) == 2, "druid-branches-remain-distinct");
        Check(authority.Offerings.Count(x => x.ClassLegacyId == 5) == 1, "priest-branch-remains-distinct");
        Check(authority.Offerings.Any(x => x.Keyword == "magma scroll" && x.Evidence == "strongly_corroborated"), "magma-branch-promoted-with-explicit-evidence-classification");
        foreach (var offering in authority.Offerings)
        {
            var inventory = offering.RequiredItems.ToDictionary(x => x.ItemId, x => x.Quantity);
            var decision = authority.CreateTurnInPlan(offering.Keyword, offering.ClassLegacyId, inventory);
            Check(decision.Passed && decision.Plan is not null, $"{offering.Keyword}-eligible-plan-created");
            Check(decision.Plan is not null && decision.Plan.Operations.Count(x => x.Opcode == "takeitem") == offering.RequiredItems.Sum(x => x.Quantity), $"{offering.Keyword}-exact-item-quantities-preserved");
            Check(decision.Plan is not null && decision.Plan.Operations.Count(x => x.Opcode == "giveitem" && x.ItemId == offering.RewardItemId) == 1, $"{offering.Keyword}-single-scroll-reward");
            Check(!authority.CreateTurnInPlan(offering.Keyword, offering.ClassLegacyId + 100, inventory).Passed, $"{offering.Keyword}-wrong-class-rejected");
            var missing = inventory.ToDictionary(x => x.Key, x => x.Value); missing[offering.RequiredItems[0].ItemId]--;
            Check(!authority.CreateTurnInPlan(offering.Keyword, offering.ClassLegacyId, missing).Passed, $"{offering.Keyword}-incomplete-turnin-rejected-before-mutation");
        }
        var dispatcher = new RuntimeClassicNarrativeOperationDispatcher();
        Check(authority.Offerings.All(o => RuntimeClassicQuestConditionAndTransactionAuthority.Project(authority.CreateTurnInPlan(o.Keyword, o.ClassLegacyId, o.RequiredItems.ToDictionary(x => x.ItemId, x => x.Quantity)).Plan!, dispatcher).RequiresAtomicTransaction), "every-turnin-routes-through-atomic-owner-plan");
        return new(failures.Count == 0, authority.Offerings.Count, authority.Offerings.Sum(x => x.RequiredItems.Sum(i => i.Quantity)), failures.Count, checks, failures);
    }
}
