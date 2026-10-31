using System.Text.Json;

namespace RealmGate.Server;

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

    internal const string ReadyWeaponKey = "classic.readiness.weapon_instance_id";
    internal const string ReadyLightKey = "classic.readiness.light_instance_id";

    // Capture legacy readiness BEFORE RuntimeItemEngine migrates old WIELD
    // and LIGHT equipment records into carried items.
    internal static IReadOnlyDictionary<string, string> SeedLegacyReadinessState(
        IEnumerable<RuntimeItemInstance> items,
        string ownerId,
        IReadOnlyDictionary<string, string> prior)
    {
        var next = new Dictionary<string, string>(prior, StringComparer.OrdinalIgnoreCase);
        var owned = items.Where(item => string.Equals(item.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase)
            && IsOwnedReadinessCandidate(item, ownerId)).ToArray();
        foreach (var (key, slot) in new[] { (ReadyWeaponKey, "WIELD"), (ReadyLightKey, "LIGHT") })
        {
            // Do not let an obsolete saved pointer hide the actual legacy
            // occupant before the item engine moves it into carried inventory.
            // A valid explicit selection still takes priority over legacy state.
            if (next.TryGetValue(key, out var selected)
                && !string.IsNullOrWhiteSpace(selected)
                && owned.Any(item => item.InstanceId.Equals(selected, StringComparison.OrdinalIgnoreCase)
                    && (item.EquippedSlot is null
                        || item.EquippedSlot.Equals(slot, StringComparison.OrdinalIgnoreCase))))
                continue;
            next.Remove(key);
            var oldItems = owned.Where(item => string.Equals(item.EquippedSlot, slot, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (oldItems.Length > 1)
                throw new InvalidDataException($"Ambiguous legacy {slot} readiness for {ownerId}.");
            if (oldItems.Length == 1) next[key] = oldItems[0].InstanceId;
        }
        return next;
    }

    // Preserve hand exclusions using original weapon-type evidence even when
    // transitional WIELD equipment relationships are absent.
    internal static bool IsTwoHandedClassicWeapon(
        RuntimeItemDefinition item, RuntimeEquipmentDefinition? relation) =>
        item.ItemType.Equals("weapon", StringComparison.OrdinalIgnoreCase)
        && (relation?.TwoHanded == true || item.WeaponType is 1 or 3);

    internal static bool IsOwnedReadinessCandidate(RuntimeItemInstance item, string ownerId) =>
        string.Equals(item.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase)
        && (item.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)
            && item.EquippedSlot is null
            || item.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)
            && item.EquippedSlot is not null);

    internal static IReadOnlyDictionary<string, string> ReconcileReadinessState(
        IEnumerable<RuntimeItemInstance> items,
        string ownerId,
        IReadOnlyDictionary<string, string> prior)
    {
        var next = new Dictionary<string, string>(prior, StringComparer.OrdinalIgnoreCase);
        var owned = items.Where(item => IsOwnedReadinessCandidate(item, ownerId)).ToArray();
        foreach (var (key, legacySlot) in new[]
                 {
                     (ReadyWeaponKey, "WIELD"),
                     (ReadyLightKey, "LIGHT")
                 })
        {
            if (next.TryGetValue(key, out var instanceId)
                && owned.Any(item => item.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase)
                    && (item.EquippedSlot is null
                        || item.EquippedSlot.Equals(legacySlot, StringComparison.OrdinalIgnoreCase))))
                continue;

            next.Remove(key);
            var legacy = owned.Where(item =>
                item.EquippedSlot is not null
                && item.EquippedSlot.Equals(legacySlot, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (legacy.Length > 1)
                throw new InvalidDataException($"Duplicate legacy {legacySlot} readiness cannot be persisted.");
            if (legacy.Length == 1) next[key] = legacy[0].InstanceId;
        }
        return next;
    }

    private static string? ResolveReadyItem(
        RuntimeItemState? state, string ownerId, string key, string legacySlot,
        params string[] acceptedLegacySlots)
    {
        if (state is null) return null;
        var owned = state.Items.Where(item => IsOwnedReadinessCandidate(item, ownerId)).ToArray();
        if (state.ActiveItemState.TryGetValue(key, out var id) && !string.IsNullOrWhiteSpace(id))
        {
            var selected = owned.FirstOrDefault(item =>
                item.InstanceId.Equals(id, StringComparison.OrdinalIgnoreCase)
                && (item.EquippedSlot is null
                    || item.EquippedSlot.Equals(legacySlot, StringComparison.OrdinalIgnoreCase)));
            if (selected is not null) return selected.InstanceId;
        }

        return owned.FirstOrDefault(item => item.EquippedSlot is not null
            && (item.EquippedSlot.Equals(legacySlot, StringComparison.OrdinalIgnoreCase)
                || acceptedLegacySlots.Any(slot =>
                    item.EquippedSlot.Equals(slot, StringComparison.OrdinalIgnoreCase))))?.InstanceId;
    }

    internal static string? ResolveReadiedWeaponInstanceId(RuntimeItemState? state, string ownerId) =>
        ResolveReadyItem(state, ownerId, ReadyWeaponKey, "WIELD", "MAIN_HAND");

    internal static string? ResolveReadiedLightInstanceId(RuntimeItemState? state, string ownerId) =>
        ResolveReadyItem(state, ownerId, ReadyLightKey, "LIGHT");

    public static RuntimeEquipmentRuntimeCommit Commit(
        RuntimeCharacterRecord character,
        RuntimeItemEngine engine,
        long gold,
        IReadOnlyDictionary<string, string> activeItemState,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(engine);

        // Persist readiness independently from the transitional synthetic
        // equipment positions. The old EquippedSlot representation is retained
        // only until combat, inventory and corpse recovery consumers migrate.
        // Rebuild these keys from real item instance IDs on each transaction.
        var snapshot = engine.Snapshot(gold, ReconcileReadinessState(
            engine.Items, character.CharacterId, activeItemState));
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
        var modifiers = engine.EquipmentModifierTotals(character.CharacterId, snapshot.ActiveItemState);
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

            var source = File.ReadAllText(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeLiveGameplayPart2.cs"));
            Require(source.Contains("RuntimeEquipmentRuntimeAuthority.Commit", StringComparison.Ordinal), "live-equip-remove-use-authoritative-commit");
            Require(!source.Contains("character with { ItemState = snapshot", StringComparison.Ordinal), "duplicate-itemstate-only-equipment-save-removed");

            var runtimeSource = File.ReadAllText(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeBundleCEquipmentRuntime.cs"));
            Require(runtimeSource.Contains("RuntimeCharacterCombatStatisticProjector.Recalculate", StringComparison.Ordinal), "stat-ability-resistance-projection-consumer");
            Require(runtimeSource.Contains("InventoryWeight", StringComparison.Ordinal), "weight-recalculation-consumer");
            Require(runtimeSource.Contains("Equipment = equipmentMap", StringComparison.Ordinal), "equipment-map-persistence-consumer");
            Require(runtimeSource.Contains("RuntimeCharacterRecordValidator.Validate", StringComparison.Ordinal), "runtime-persistence-validation");

            var equipmentRuntime = File.ReadAllText(Path.Combine(projectRoot, "Apps", "Source", "RealmGate.Server", "RuntimeItemsEquipment.cs"));
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
