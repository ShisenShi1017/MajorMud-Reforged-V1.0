using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeEquipmentRuntimeCommit(
    RuntimeCharacterRecord Character,
    RuntimeItemState ItemState,
    RuntimeEquipmentModifierTotals Modifiers,
    RuntimeInventoryWeightTotals Weight,
    IReadOnlyList<string> EquippedInstanceIds);

public static class RuntimeEquipmentRuntimeAuthority
{
    public const string ContractMarker = "bundle_c.equipment_runtime.contract";
    public const string ContractVersion = "0004";
    public const string WeightMarker = "inventory.total_weight";

    public static RuntimeEquipmentRuntimeCommit Commit(
        RuntimeCharacterRecord character,
        RuntimeItemEngine engine,
        long gold,
        IReadOnlyDictionary<string, string> activeItemState,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(engine);

        var snapshot = engine.Snapshot(gold, activeItemState);
        var equipped = snapshot.Items
            .Where(item => string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase) &&
                           item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrWhiteSpace(item.EquippedSlot))
            .OrderBy(item => item.EquippedSlot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var duplicateSlot = equipped.GroupBy(item => item.EquippedSlot!, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicateSlot is not null) throw new InvalidDataException($"Equipment slot '{duplicateSlot.Key}' contains multiple items.");

        var equipmentMap = equipped.ToDictionary(item => item.EquippedSlot!, item => (string?)item.InstanceId, StringComparer.OrdinalIgnoreCase);
        var inventory = snapshot.Items
            .Where(item => string.Equals(item.OwnerId, character.CharacterId, StringComparison.OrdinalIgnoreCase) &&
                           item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.InstanceId)
            .ToArray();
        var modifiers = engine.EquipmentModifierTotals(character.CharacterId);
        var weight = engine.InventoryWeight(character.CharacterId);

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [WeightMarker] = weight.TotalWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var projected = RuntimeCharacterCombatStatisticProjector.Recalculate(
            character with
            {
                Inventory = inventory,
                Equipment = equipmentMap,
                ItemState = snapshot,
                Conditions = conditions,
                UpdatedAtUtc = nowUtc
            },
            modifiers,
            projectedAtUtc: nowUtc);

        var semantic = projected.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [ContractMarker] = string.Join("|", ContractVersion, nowUtc.ToUniversalTime().ToString("O"), string.Join(",", equipped.Select(item => item.InstanceId)))
        };
        projected = projected with { SemanticState = semantic with { RuntimeMarkers = markers } };
        RuntimeCharacterRecordValidator.Validate(projected);
        return new RuntimeEquipmentRuntimeCommit(projected, snapshot, modifiers, weight, equipped.Select(item => item.InstanceId).ToArray());
    }
}

public sealed record RuntimeBundleCEquipmentRuntimeAuditReport(
    string Status,
    int ItemCount,
    int EquipmentCount,
    int AbilityModifierItemCount,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Errors)
{
    public bool Passed => Status == "PASS";
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCEquipmentRuntimeAudit
{
    public static RuntimeBundleCEquipmentRuntimeAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var errors = new List<string>();
        void Require(bool condition, string check)
        {
            if (condition) checks.Add(check); else errors.Add(check);
        }

        try
        {
            var catalog = RuntimeItemCatalog.Load(projectRoot, worldId);
            Require(catalog.EquipmentDefinitions.All(definition => catalog.IsLegalSlot(definition.Slot)), "all-equipment-slots-legal");
            Require(catalog.Items.Any(item => item.EquipmentAbilityModifiers.Count > 0), "decoded-equipment-ability-modifiers-loaded");

            var source = File.ReadAllText(Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeLiveGameplayPart2.cs"));
            Require(source.Contains("RuntimeEquipmentRuntimeAuthority.Commit", StringComparison.Ordinal), "live-equip-remove-use-authoritative-commit");
            Require(!source.Contains("character with { ItemState = snapshot", StringComparison.Ordinal), "duplicate-itemstate-only-equipment-save-removed");

            var runtimeSource = File.ReadAllText(Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeBundleCEquipmentRuntime.cs"));
            Require(runtimeSource.Contains("RuntimeCharacterCombatStatisticProjector.Recalculate", StringComparison.Ordinal), "stat-ability-resistance-projection-consumer");
            Require(runtimeSource.Contains("InventoryWeight", StringComparison.Ordinal), "weight-recalculation-consumer");
            Require(runtimeSource.Contains("Equipment = equipmentMap", StringComparison.Ordinal), "equipment-map-persistence-consumer");
            Require(runtimeSource.Contains("RuntimeCharacterRecordValidator.Validate", StringComparison.Ordinal), "runtime-persistence-validation");

            var equipmentRuntime = File.ReadAllText(Path.Combine(projectRoot, "Apps", "Source", "Reforged.Server", "RuntimeItemsEquipment.cs"));
            Require(equipmentRuntime.Contains("EnforceHandRules", StringComparison.Ordinal), "hand-conflict-runtime");
            Require(equipmentRuntime.Contains("Equipment slot is occupied", StringComparison.Ordinal), "slot-occupancy-runtime");
            Require(equipmentRuntime.Contains("no_remove", StringComparison.Ordinal), "unequip-restriction-runtime");

            return new(errors.Count == 0 ? "PASS" : "FAIL", catalog.Items.Count, catalog.EquipmentDefinitions.Count,
                catalog.Items.Count(item => item.EquipmentAbilityModifiers.Count > 0), checks, errors);
        }
        catch (Exception ex)
        {
            errors.Add(ex.ToString());
            return new("FAIL", 0, 0, 0, checks, errors);
        }
    }
}
