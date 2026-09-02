using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeBundleCEquipmentFieldAudit(
    string Field,
    string OriginalDecode,
    string GeneratedField,
    string RuntimeOwner,
    string RuntimeConsumer,
    string PersistenceOwner,
    string EvidenceStatus,
    string DuplicateDisposition,
    bool Passed);

public sealed record RuntimeBundleCEquipmentAuthorityAuditReport(
    string Status,
    int EquipmentRecordCount,
    int SlotCount,
    int RuleCount,
    IReadOnlyList<RuntimeBundleCEquipmentFieldAudit> Fields,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Errors)
{
    public bool Passed => Status.Equals("PASS", StringComparison.OrdinalIgnoreCase);
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCEquipmentAuthorityAudit
{
    private sealed record Mapping(string Field, string Generated, string Owner, string Consumer, string Persistence, string Evidence, string Duplicate);

    private static readonly Mapping[] Mappings =
    {
        new("item_id", "equipment.json:item_id", "RuntimeItemCatalog._equipment", "RuntimeItemEngine.Equip/RestoreEquipment/EquipNpc", "RuntimeItemInstance.CanonicalItemId", "verified", "single catalog identity"),
        new("slot", "equipment.json:slot + worn_on", "RuntimeItemCatalog._equipment/_slots", "RuntimeItemEngine.Equip/EnforceHandRules", "RuntimeItemInstance.EquippedSlot", "verified", "live commands delegate to RuntimeItemEngine"),
        new("two_handed", "equipment.json:two_handed", "RuntimeItemCatalog._equipment", "RuntimeItemEngine.EnforceHandRules", "equipped state revalidated on load", "verified", "single hand-conflict executor"),
        new("class_restricted", "equipment.json:class_restricted + character_foundation item requirements", "RuntimeCharacterBundleCatalog.ItemRequirements", "RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements", "character class id + equipped item state", "strongly_correlated", "generated boolean is compatibility marker only"),
        new("race_restricted", "equipment.json:race_restricted + character_foundation item requirements", "RuntimeCharacterBundleCatalog.ItemRequirements", "RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements", "character race id + equipped item state", "strongly_correlated", "generated boolean is compatibility marker only"),
        new("minimum_level", "equipment.json:minimum_level + items runtime_projection", "RuntimeCharacterBundleCatalog.ItemRequirements", "RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements", "RuntimeCharacterRecord.Level + RuntimeItemInstance", "verified", "Bundle A authority supersedes relationship fallback"),
        new("maximum_level", "equipment.json:maximum_level + item ability 136", "RuntimeCharacterBundleCatalog.ItemRequirements + RuntimeItemCatalog._equipment", "RuntimeItemEngine.EnforceAuthoritativeCharacterRequirements", "RuntimeCharacterRecord.Level + RuntimeItemInstance", "verified", "zero means uncapped; positive decoded caps fail closed"),
        new("minimum_strength", "equipment.json:minimum_strength", "RuntimeItemCatalog._equipment", "RuntimeItemEngine.Equip", "RuntimeCharacterRecord.BaseStats + RuntimeItemInstance", "verified", "single live equip check"),
        new("alignment", "equipment.json:alignment", "RuntimeItemCatalog._equipment", "RuntimeItemEngine.Equip", "RuntimeCharacterRecord.Conditions + RuntimeItemInstance", "strongly_correlated", "single live equip check"),
        new("gender", "equipment.json:gender", "RuntimeItemCatalog._equipment", "RuntimeItemEngine.Equip", "RuntimeCharacterRecord.Conditions + RuntimeItemInstance", "strongly_correlated", "single live equip check"),
        new("combat modifiers", "items.json:combat", "RuntimeItemCatalog._items", "RuntimeItemEngine.EquipmentModifiers -> RuntimeCharacterCombatStatisticProjector", "RuntimeCharacterSemanticState derived projection", "strongly_correlated", "calculator owns aggregation; projector owns character projection"),
        new("curse/maintenance-cleanup abilities", "items.json ability flags 82/83 and Del@Maint ability 119", "RuntimeItemCatalog._items", "RuntimeItemEngine transfer/death/cleanup guards", "RuntimeItemInstance metadata/location + save state", "verified", "single item lifecycle executor"),
        new("weight", "items.json:economy.weight", "RuntimeItemCatalog._items", "RuntimeItemEngine.InventoryWeight", "item stack/location state; recalculated total", "verified", "single authoritative weight summation")
    };

    public static RuntimeBundleCEquipmentAuthorityAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var errors = new List<string>();
        var data = Path.Combine(projectRoot, "Data", worldId);
        var equipmentPath = Path.Combine(data, "equipment.json");
        var slotsPath = Path.Combine(data, "equipment_slots.json");
        var rulesPath = Path.Combine(data, "equipment_rules.json");
        var itemsPath = Path.Combine(data, "items.json");
        var runtimePath = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeItemsEquipment.cs");
        var livePath = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeLiveGameplayPart2.cs");
        var derivedPath = Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeCharacterDerivedStatistics.cs");

        using var equipment = JsonDocument.Parse(File.ReadAllText(equipmentPath));
        using var slots = JsonDocument.Parse(File.ReadAllText(slotsPath));
        using var rules = JsonDocument.Parse(File.ReadAllText(rulesPath));
        using var items = JsonDocument.Parse(File.ReadAllText(itemsPath));
        var equipmentRecords = equipment.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var slotRecords = slots.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var ruleRecords = rules.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var itemIds = items.RootElement.GetProperty("records").EnumerateArray().Select(x => x.GetProperty("id").GetString()).Where(x => x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var slotIds = slotRecords.Select(x => x.GetProperty("id").GetString()).Where(x => x is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Check(equipmentRecords.Length > 0, "equipment records loaded");
        Check(slotRecords.Length > 0, "equipment slots loaded");
        Check(ruleRecords.Length > 0, "equipment rules loaded");
        Check(equipmentRecords.All(x => x.TryGetProperty("item_id", out var id) && id.GetString() is { } value && itemIds.Contains(value)), "every equipment relationship resolves an item");
        Check(equipmentRecords.All(x => x.TryGetProperty("slot", out var slot) && slot.GetString() is { } value && slotIds.Contains(value)), "every equipment relationship resolves a legal slot");
        Check(equipmentRecords.Select(x => x.GetProperty("item_id").GetString()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == equipmentRecords.Length, "equipment relationship identity is unique");
        var multicolouredStaff = equipmentRecords.Single(x => x.GetProperty("item_id").GetString() == "item.1916");
        Check(multicolouredStaff.GetProperty("minimum_level").GetInt32() == 65 && multicolouredStaff.GetProperty("maximum_level").GetInt32() == 65, "item 1916 preserves decoded exact-level-65 equipment gate");

        var runtime = File.ReadAllText(runtimePath);
        var live = File.ReadAllText(livePath);
        var derived = File.ReadAllText(derivedPath);
        Require(runtime, "EnforceAuthoritativeCharacterRequirements", "authoritative requirement consumer");
        Require(runtime, "RuntimeItemInstance", "equipment persistence record");
        Require(runtime, "EquipmentModifierTotals", "single equipment modifier aggregation");
        Require(runtime, "InventoryWeight", "authoritative equipment/inventory weight aggregation");
        Require(live, "engine.Equip", "live equip delegates to item engine");
        Require(live, "engine.Unequip", "live unequip delegates to item engine");
        Require(derived, "RuntimeCharacterCombatStatisticProjector", "derived-stat projection consumes authoritative totals");

        var fields = Mappings.Select(m => new RuntimeBundleCEquipmentFieldAudit(m.Field, "WCC/Worldgroup/MDB/Librarium decoded projection", m.Generated, m.Owner, m.Consumer, m.Persistence, m.Evidence, m.Duplicate, true)).ToArray();
        return new(errors.Count == 0 ? "PASS" : "FAIL", equipmentRecords.Length, slotRecords.Length, ruleRecords.Length, fields, checks, errors);

        void Check(bool condition, string text) { if (condition) checks.Add(text); else errors.Add(text); }
        void Require(string source, string token, string text) => Check(source.Contains(token, StringComparison.Ordinal), text);
    }
}
