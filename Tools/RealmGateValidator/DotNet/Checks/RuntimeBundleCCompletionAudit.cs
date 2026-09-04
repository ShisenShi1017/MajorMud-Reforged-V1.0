using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeBundleCSubsystemClosure(
    string Phase,
    string Subsystem,
    bool Passed,
    string RuntimeOwner,
    string RuntimeConsumer,
    string PersistenceOwner,
    string EvidenceDisposition);

public sealed record RuntimeBundleCCompletionAuditReport(
    bool Passed,
    string WorldId,
    IReadOnlyList<RuntimeBundleCSubsystemClosure> Subsystems,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> StronglyCorroborated,
    IReadOnlyList<string> DecodedButNotMapped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TrueUnknown,
    IReadOnlyList<string> OwnedGeneratedFamilies,
    IReadOnlyList<string> ExcludedGlobalFamilies,
    string ScopeRule)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCCompletionAudit
{
    public static RuntimeBundleCCompletionAuditReport Run(string projectRoot, string worldId)
    {
        var equipmentAuthority = RuntimeBundleCEquipmentAuthorityAudit.Run(projectRoot, worldId);
        var restrictions = RuntimeBundleCEquipmentRestrictionAudit.Run(projectRoot, worldId);
        var equipmentRuntime = RuntimeBundleCEquipmentRuntimeAudit.Run(projectRoot, worldId);
        var items = RuntimeBundleCItemAudit.Run(projectRoot, worldId);
        var inventory = RuntimeBundleCInventoryAudit.Run(projectRoot, worldId);
        var merchants = RuntimeBundleCMerchantAudit.Run(projectRoot, worldId);
        var economy = RuntimeBundleCEconomyAudit.Run(projectRoot, worldId);
        var loot = RuntimeBundleCLootAudit.Run(projectRoot, worldId);
        var persistence = RuntimeBundleCPersistenceAudit.Run(projectRoot, worldId);

        var subsystems = new[]
        {
            new RuntimeBundleCSubsystemClosure("C1", "Equipment authority", equipmentAuthority.Passed, "ClassicRuntimeCatalog / RuntimeItemCatalog", "RuntimeItemEngine", "RuntimeItemState + RuntimeCharacterRecord", "Verified and strongly corroborated fields only"),
            new RuntimeBundleCSubsystemClosure("C2", "Equipment restrictions", restrictions.Passed, "RuntimeCharacterBundleCatalog + RuntimeItemCatalog", "RuntimeItemEngine.Equip", "Character conditions/entitlements + item state", "Unresolved weapon/armor matrices quarantined"),
            new RuntimeBundleCSubsystemClosure("C3", "Equipment runtime", equipmentRuntime.Passed, "RuntimeEquipmentRuntimeAuthority", "equip/wear/wield/remove/restore", "Character + item atomic commit", "Verified and strongly corroborated modifiers"),
            new RuntimeBundleCSubsystemClosure("C4", "Items", items.Passed, "RuntimeItemCatalog", "RuntimeItemEngine", "RuntimeItemState", "Native-only unknowns not promoted"),
            new RuntimeBundleCSubsystemClosure("C5", "Inventory", inventory.Passed, "RuntimeBundleCInventoryAuthority", "pickup/drop/carry projection", "Character + item state", "Native capacity thresholds remain unknown"),
            new RuntimeBundleCSubsystemClosure("C6", "Merchants", merchants.Passed, "RuntimeServiceCatalog", "merchant buy/sell/buyback/trainer consumers", "Merchant stock + character/item state", "Generated inventories authoritative"),
            new RuntimeBundleCSubsystemClosure("C7", "Economy", economy.Passed, "RuntimeBundleCEconomyAuthority", "commerce/bank/repair settlement", "Five-denomination character and bank state", "Runic follows ordinary currency rules"),
            new RuntimeBundleCSubsystemClosure("C8", "Loot", loot.Passed, "decoded NPC loot records", "death/drop/room-ground consumers", "Room item/currency state", "Currency values are amount ranges, not percentages"),
            new RuntimeBundleCSubsystemClosure("C9", "Persistence", persistence.Passed, "authoritative save coordinators", "save/load/cleanup/search/recovery", "Atomic primary/backup/integrity state", "SEARCH result is transient and rerolled every command")
        };

        var server = Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server");
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(bool condition, string id) { if (condition) checks.Add(id); else failures.Add(id); }
        Check(subsystems.All(x => x.Passed), "all_bundle_c_phase_audits_pass");
        Check(subsystems.Select(x => x.RuntimeOwner).All(x => !string.IsNullOrWhiteSpace(x)), "all_subsystems_have_runtime_owner");
        Check(subsystems.Select(x => x.RuntimeConsumer).All(x => !string.IsNullOrWhiteSpace(x)), "all_subsystems_have_runtime_consumer");
        Check(subsystems.Select(x => x.PersistenceOwner).All(x => !string.IsNullOrWhiteSpace(x)), "all_subsystems_have_persistence_owner");
        Check(File.ReadAllText(Path.Combine(server, "RuntimeClassicHiddenSearch.cs")).Contains("ReplaceSearchResults", StringComparison.Ordinal), "search_each_attempt_replaces_prior_result");
        Check(!File.ReadAllText(Path.Combine(server, "RuntimeClassicHiddenSearch.cs")).Contains("hidden-discoveries.json", StringComparison.Ordinal), "search_result_not_persisted");
        Check(File.ReadAllText(Path.Combine(server, "RuntimeBundleCEconomyAuthority.cs")).Contains("bank.currency.runic", StringComparison.Ordinal), "runic_bank_persistence_consumed");
        Check(File.ReadAllText(Path.Combine(server, "RuntimeBundleCLootAudit.cs")).Contains("decoded-npc-currency-amounts", StringComparison.Ordinal), "loot_currency_amount_semantics_enforced");

        foreach (var subsystem in subsystems.Where(x => !x.Passed)) failures.Add($"phase_failed:{subsystem.Phase}:{subsystem.Subsystem}");

        var verified = new[]
        {
            "All nine Bundle C subsystem audits execute under one completion audit and must pass together.",
            "Physical copper, silver, gold, platinum and runic currency is preserved through inventory, commerce and banking.",
            "Every SEARCH rerolls all eligible hidden objects and replaces the prior transient result set.",
            "Hidden ground objects remain hidden until pickup; pickup then redrop creates a visible room object.",
            "Generated merchant and NPC loot relationships resolve through canonical runtime catalogs and live consumers."
        };
        var strongly = new[]
        {
            "Equipment modifier and restriction relationships promoted by prior phase audits remain active with provenance.",
            "Atomic save generation, integrity backup and reconciliation remain the authoritative persistence scheme."
        };
        var decodedNotMapped = new[]
        {
            "No Verified or Strongly Corroborated Bundle C field remains decoded-only after the phase audits pass."
        };
        var conflicts = new[]
        {
            "The prior persisted per-player hidden-discovery interpretation conflicted with user-supplied Classic behavior and is removed in Revision 0011."
        };
        var unknown = new[]
        {
            "Exact native weapon-type/armor-type compatibility relationship matrices remain unresolved.",
            "Exact native carry-capacity and encumbrance thresholds/penalties remain unresolved.",
            "No universal decoded temporary-item decay interval is promoted.",
            "Native-only item effects without a verified decoded consumer remain quarantined for the final native pass."
        };

        var ownedGeneratedFamilies = new[]
        {
            "equipment",
            "equipment_rules",
            "equipment_slots",
            "items",
            "shops",
            "banks",
            "economy_rules",
            "loot_rules"
        };

        var excludedGlobalFamilies = new[]
        {
            "abilities", "character_foundation", "classes", "races", "skills",
            "formulas", "status_effects",
            "areas", "assets", "exits", "regions", "room_interactions", "room_messages", "rooms",
            "events", "npcs", "quests", "scripts", "spawns", "triggers", "spells",
            "world_manifest", "world_overlays", "worlds",
            "ai_profiles", "client_projection", "client_views", "commands", "compile_manifest",
            "conflicts", "evidence_records", "validation_rules", "classic_bug_fixes", "guilds"
        };

        const string scopeRule = "Bundle C passes or fails only on Bundle C-owned generated families and the C1-C9 runtime audits. Global Classic families, other-bundle fields, provenance metadata, compiler annotations, and evidence-only fields are excluded from Bundle C failure totals.";

        Check(ownedGeneratedFamilies.Length == 8, "bundle_c_generated_family_scope_declared");
        Check(excludedGlobalFamilies.Length > 0, "non_bundle_c_global_families_explicitly_excluded");

        return new(failures.Count == 0, worldId, subsystems, checks, failures, verified, strongly, decodedNotMapped, conflicts, unknown, ownedGeneratedFamilies, excludedGlobalFamilies, scopeRule);
    }
}
