using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeItemSmokeResult(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeItemSmokeReport
{
    public static RuntimeItemSmokeResult Run(string projectRoot, string worldId)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        void Check(string name, Action action)
        {
            try { action(); checks.Add(name); }
            catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
        }

        var catalog = RuntimeItemCatalog.Load(projectRoot, worldId);
        var engine = new RuntimeItemEngine(catalog);
        var owner = "smoke:owner";
        var other = "smoke:other";
        var room = "smoke:room";
        const string merchantId = "smoke:merchant";
        var gettable = catalog.Items.First(x => x.Gettable && !x.Container && !x.RetainedOnDeath);
        var containerDef = catalog.Items.FirstOrDefault(x => x.Container && x.Gettable);
        var equipDef = catalog.EquipmentDefinitions.First(x => x.Slot != "NONE" && !x.ClassRestricted && !x.RaceRestricted && x.MinimumLevel == 0 && x.MinimumStrength == 0 && string.IsNullOrWhiteSpace(x.Alignment) && string.IsNullOrWhiteSpace(x.Gender) && !catalog.Require(x.ItemId).RetainedOnDeath);
        var character = SmokeCharacter(owner, worldId);

        Check("classic-readiness-persists-independent-instance-ids", () =>
        {
            var weapon = new RuntimeItemInstance("ready-weapon", "item.1", owner,
                RuntimeItemLocations.Equipment, owner, "WIELD", 0, 1, null);
            var light = new RuntimeItemInstance("ready-light", "item.2", owner,
                RuntimeItemLocations.Equipment, owner, "LIGHT", 50, 1, null);
            var prior = new Dictionary<string, string>
            {
                ["bank.test"] = "preserved",
                [RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey] = "stale"
            };
            var active = RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(
                new[] { weapon, light }, owner, prior);
            if (active.GetValueOrDefault(RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey) != weapon.InstanceId
                || active.GetValueOrDefault(RuntimeEquipmentRuntimeAuthority.ReadyLightKey) != light.InstanceId
                || active.GetValueOrDefault("bank.test") != "preserved")
                throw new InvalidDataException("Ready instance IDs were not persisted independently.");

            var carried = RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(
                new[] { weapon with { LocationKind = RuntimeItemLocations.Character, EquippedSlot = null } },
                owner, active);
            if (carried.GetValueOrDefault(RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey) != weapon.InstanceId
                || carried.ContainsKey(RuntimeEquipmentRuntimeAuthority.ReadyLightKey)
                || carried.GetValueOrDefault("bank.test") != "preserved")
                throw new InvalidDataException("Carried readied weapon was lost or stale light was retained.");

            var removed = RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(
                Array.Empty<RuntimeItemInstance>(), owner, carried);
            if (removed.ContainsKey(RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey)
                || removed.GetValueOrDefault("bank.test") != "preserved")
                throw new InvalidDataException("Missing weapon failed to clear readiness or erased bank data.");
        });

        Check("classic-carried-readiness-state-survives-and-clears", () =>
        {
            var weaponDef = catalog.Items.First(x =>
                x.ItemType.Equals("weapon", StringComparison.OrdinalIgnoreCase) && x.Gettable);
            var weapon = new RuntimeItemEngine(catalog).Create(
                weaponDef.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
            var saved = new RuntimeItemState(new[] { weapon }, 0,
                new Dictionary<string, string>
                {
                    [RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey] = weapon.InstanceId
                });
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(saved, owner)
                != weapon.InstanceId)
                throw new InvalidDataException("Carried readied weapon was not resolved.");

            var modifiers = new RuntimeItemEngine(catalog, new[] { weapon })
                .EquipmentModifierTotals(owner, saved.ActiveItemState);
            if (!modifiers.SourceInstanceIds.Contains(weapon.InstanceId))
                throw new InvalidDataException("Carried readied weapon was omitted from equipment modifiers.");

            var dropped = RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(
                new[] { weapon with { LocationKind = RuntimeItemLocations.Room,
                    LocationId = room, OwnerId = null } }, owner, saved.ActiveItemState);
            if (dropped.ContainsKey(RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey))
                throw new InvalidDataException("Dropped ready weapon retained its active pointer.");
        });

        Check("classic-two-handed-weapon-type-without-synthetic-slot", () =>
        {
            var nativeTwoHanded = new RuntimeItemDefinition("smoke:weapon", "Test weapon",
                "weapon", true, false, false, false, 0) { WeaponType = 1 };
            if (!RuntimeEquipmentRuntimeAuthority.IsTwoHandedClassicWeapon(nativeTwoHanded, null)
                || !RuntimeEquipmentRuntimeAuthority.IsTwoHandedClassicWeapon(
                    nativeTwoHanded with { WeaponType = 3 }, null)
                || RuntimeEquipmentRuntimeAuthority.IsTwoHandedClassicWeapon(
                    nativeTwoHanded with { WeaponType = 2 }, null)
                || RuntimeEquipmentRuntimeAuthority.IsTwoHandedClassicWeapon(
                    nativeTwoHanded with { ItemType = "armor" }, null))
                throw new InvalidDataException("Native two-handed weapon rules must work without WIELD relationships.");
        });

        Check("classic-weapon-readiness-reads-saved-instance-id", () =>
        {
            var one = new RuntimeItemInstance("readied-first", "item.1", owner,
                RuntimeItemLocations.Equipment, owner, "WIELD", 0, 1, null);
            var two = new RuntimeItemInstance("readied-second", "item.2", owner,
                RuntimeItemLocations.Character, owner, null, 0, 1, null);
            var state = new RuntimeItemState(new[] { one, two }, 0,
                new Dictionary<string, string>
                {
                    [RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey] = one.InstanceId
                });
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(state, owner)
                != one.InstanceId)
                throw new InvalidDataException("Saved weapon instance was not selected.");
            var stale = state with
            {
                ActiveItemState = new Dictionary<string, string>
                {
                    [RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey] = "missing-instance"
                }
            };
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(stale, owner)
                != one.InstanceId)
                throw new InvalidDataException("Legacy wield fallback was not retained.");
            var removed = state with { Items = new[] { two } };
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(removed, owner) is not null)
                throw new InvalidDataException("Orphaned weapon readiness was accepted.");

            var offhand = one with { EquippedSlot = "OFF_HAND" };
            var noMainHand = state with { Items = new[] { offhand, two } };
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(noMainHand, owner) is not null)
                throw new InvalidDataException("Off-hand item was mistaken for primary weapon.");

            var legacyMain = state with
            {
                Items = new[] { one with { EquippedSlot = "MAIN_HAND" }, two },
                ActiveItemState = new Dictionary<string, string>()
            };
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(legacyMain, owner)
                != one.InstanceId)
                throw new InvalidDataException("Legacy MAIN_HAND combat fixture lost its weapon.");
        });

        Check("classic-light-readiness-reads-saved-instance-id", () =>
        {
            var light = new RuntimeItemInstance("lit-lantern", "item.2", owner,
                RuntimeItemLocations.Equipment, owner, "LIGHT", 50, 1, null);
            var unlit = new RuntimeItemInstance("spare-lantern", "item.2", owner,
                RuntimeItemLocations.Character, owner, null, 50, 1, null);
            var active = new RuntimeItemState(new[] { light, unlit }, 0,
                new Dictionary<string, string>
                {
                    [RuntimeEquipmentRuntimeAuthority.ReadyLightKey] = light.InstanceId
                });
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedLightInstanceId(active, owner)
                != light.InstanceId)
                throw new InvalidDataException("Saved lantern instance was not resolved.");
            var stale = active with
            {
                ActiveItemState = new Dictionary<string, string>
                {
                    [RuntimeEquipmentRuntimeAuthority.ReadyLightKey] = "nonexistent"
                }
            };
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedLightInstanceId(stale, owner)
                != light.InstanceId)
                throw new InvalidDataException("Older saved lantern fallback did not work.");
            var removed = active with { Items = new[] { unlit } };
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedLightInstanceId(removed, owner) is not null)
                throw new InvalidDataException("Stale light pointer resolved after light removal.");
            var cleanup = RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(
                removed.Items, owner, active.ActiveItemState);
            if (cleanup.ContainsKey(RuntimeEquipmentRuntimeAuthority.ReadyLightKey))
                throw new InvalidDataException("A removed lantern retained its active-state pointer.");
        });

        Check("classic-light-command-uses-item-runtime", () =>
        {
            if (RuntimeCommandRuntimeRouteCatalog.Require("light").Kind != RuntimeCommandRouteKind.ItemRuntime)
                throw new InvalidDataException("LIGHT command must route through the item runtime.");
        });

        Check("classic-legacy-ready-state-seeding", () =>
        {
            var legacyWeapon = new RuntimeItemInstance("legacy-ready-weapon", "item.1", owner,
                RuntimeItemLocations.Equipment, owner, "WIELD", 0, 1, null);
            var legacyLight = new RuntimeItemInstance("legacy-ready-light", "item.2", owner,
                RuntimeItemLocations.Equipment, owner, "LIGHT", 50, 1, null);
            var seeded = RuntimeEquipmentRuntimeAuthority.SeedLegacyReadinessState(
                new[] { legacyWeapon, legacyLight }, owner,
                new Dictionary<string, string> { ["bank"] = "keep" });
            if (seeded.GetValueOrDefault(RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey)
                    != legacyWeapon.InstanceId ||
                seeded.GetValueOrDefault(RuntimeEquipmentRuntimeAuthority.ReadyLightKey)
                    != legacyLight.InstanceId || seeded.GetValueOrDefault("bank") != "keep")
                throw new InvalidDataException("Legacy weapon/light readiness was not captured before migration.");

            var carried = new[]
            {
                legacyWeapon with { LocationKind = RuntimeItemLocations.Character, EquippedSlot = null },
                legacyLight with { LocationKind = RuntimeItemLocations.Character, EquippedSlot = null }
            };
            var preserved = RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(carried, owner, seeded);
            if (RuntimeEquipmentRuntimeAuthority.ResolveReadiedWeaponInstanceId(
                    new RuntimeItemState(carried, 0, preserved), owner) != legacyWeapon.InstanceId ||
                RuntimeEquipmentRuntimeAuthority.ResolveReadiedLightInstanceId(
                    new RuntimeItemState(carried, 0, preserved), owner) != legacyLight.InstanceId)
                throw new InvalidDataException("Readiness pointers were lost after synthetic equipment removal.");
        });

        Check("classic-death-retained-readiness-after-legacy-slot-migration", () =>
        {
            var retainedWeapon = new RuntimeItemInstance("legacy.retained.weapon", "item.1",
                owner, RuntimeItemLocations.Equipment, owner, "WIELD", 0, 1, null);
            var retainedLight = new RuntimeItemInstance("legacy.retained.light", "item.2",
                owner, RuntimeItemLocations.Equipment, owner, "LIGHT", 35, 1, null);
            var preMigration = new[] { retainedWeapon, retainedLight };
            var seeded = RuntimeEquipmentRuntimeAuthority.SeedLegacyReadinessState(
                preMigration, owner, new Dictionary<string, string>());
            // Simulate a death transition where a retained item is moved into
            // inventory by slot migration, while a dropped light goes to corpse.
            var afterDeath = new[]
            {
                retainedWeapon with { LocationKind = RuntimeItemLocations.Character, EquippedSlot = null },
                retainedLight with { LocationKind = RuntimeItemLocations.Corpse,
                    LocationId = "corpse.test", OwnerId = null, EquippedSlot = null }
            };
            var after = RuntimeEquipmentRuntimeAuthority.ReconcileReadinessState(
                afterDeath, owner, seeded);
            if (after.GetValueOrDefault(RuntimeEquipmentRuntimeAuthority.ReadyWeaponKey)
                    != retainedWeapon.InstanceId
                || after.ContainsKey(RuntimeEquipmentRuntimeAuthority.ReadyLightKey))
                throw new InvalidDataException("Death readiness lost a retained migrated weapon or retained a corpse light.");
        });

        Check("classic-synthetic-relationship-removal-save-migration", () =>
        {
            // This branch runs after the compiler no longer emits synthetic
            // WIELD/LIGHT relationships; it is dormant in old generated data.
            foreach (var (itemType, oldSlot) in new[]
                     {
                         ("weapon", "WIELD"), ("light_source", "LIGHT")
                     })
            {
                var definition = catalog.Items.FirstOrDefault(item =>
                    item.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase)
                    && catalog.Equipment(item.Id) is null);
                if (definition is null) continue;
                var legacy = new RuntimeItemInstance("legacy." + oldSlot, definition.Id, owner,
                    RuntimeItemLocations.Equipment, owner, oldSlot, 50, 1, null);
                var migrated = new RuntimeItemEngine(catalog, new[] { legacy }).Items.Single();
                if (migrated.InstanceId != legacy.InstanceId ||
                    migrated.LocationKind != RuntimeItemLocations.Character ||
                    migrated.EquippedSlot is not null || migrated.Charges != legacy.Charges ||
                    migrated.Metadata?.GetValueOrDefault("classic_legacy_equipped_slot") != oldSlot)
                    throw new InvalidDataException("Native readiness migration did not preserve original item state.");
            }
        });

        Check("classic-saved-equipment-native-slot-migration", () =>
        {
            var definition = catalog.EquipmentDefinitions.First(x =>
                x.Slot is not "NONE" and not "UNKNOWN");
            var oldSlot = definition.Slot.Equals("BACK", StringComparison.OrdinalIgnoreCase)
                ? "FEET" : "BACK";
            var source = new RuntimeItemEngine(catalog);
            var oldInstance = source.Create(definition.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner);
            var fixedInstance = source.Create(definition.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner);
            var savedOld = oldInstance with
            {
                LocationKind = RuntimeItemLocations.Equipment,
                EquippedSlot = oldSlot
            };
            var savedCorrect = fixedInstance with
            {
                LocationKind = RuntimeItemLocations.Equipment,
                EquippedSlot = definition.Slot
            };
            var single = RuntimeItemEngine.ReconcileSavedEquipmentSlots(new[] { savedOld }, catalog).Single();
            if (single.InstanceId != savedOld.InstanceId || single.EquippedSlot != definition.Slot ||
                single.Metadata?.GetValueOrDefault("classic_legacy_equipped_slot") != oldSlot)
                throw new InvalidDataException("Legacy equipment position was not migrated and recorded.");

            // Live command initialization is the primary save-load path. It must
            // reconcile the very same source state, not just RestoreSnapshot.
            var liveLoaded = new RuntimeItemEngine(catalog, new[] { savedOld });
            var liveItem = liveLoaded.RequireInstance(savedOld.InstanceId);
            if (liveItem.EquippedSlot != definition.Slot ||
                liveItem.Metadata?.GetValueOrDefault("classic_legacy_equipped_slot") != oldSlot)
                throw new InvalidDataException("Live engine constructor did not reconcile the old equipment slot.");

            var snapshotLoaded = new RuntimeItemEngine(catalog);
            snapshotLoaded.RestoreSnapshot(new RuntimeItemState(
                new[] { savedOld }, 0, new Dictionary<string, string>()));
            if (snapshotLoaded.RequireInstance(savedOld.InstanceId).EquippedSlot != definition.Slot)
                throw new InvalidDataException("RestoreSnapshot did not reconcile the old equipment slot.");

            var conflict = RuntimeItemEngine.ReconcileSavedEquipmentSlots(
                new[] { savedOld, savedCorrect }, catalog);
            if (conflict.Count != 2 || conflict.Select(x => x.InstanceId).Distinct().Count() != 2 ||
                conflict[1].EquippedSlot != definition.Slot ||
                conflict[0].EquippedSlot is not null ||
                conflict[0].LocationKind != RuntimeItemLocations.Character ||
                conflict[0].Metadata?.GetValueOrDefault("classic_pending_equipped_slot") != definition.Slot)
                throw new InvalidDataException("Legacy equipment conflict lost an item or displaced the native occupant.");

            var liveConflict = new RuntimeItemEngine(catalog, new[] { savedOld, savedCorrect });
            if (liveConflict.RequireInstance(savedOld.InstanceId).LocationKind != RuntimeItemLocations.Character ||
                liveConflict.RequireInstance(savedCorrect.InstanceId).EquippedSlot != definition.Slot ||
                liveConflict.Items.Count != 2)
                throw new InvalidDataException("Live migration lost an equipment item or overwrote native occupancy.");
        });

        Check("decoded-curse-remove-and-death-retention", () =>
        {
            var curseEngine = new RuntimeItemEngine(catalog);
            var ordinaryCurse = curseEngine.Create("item.1521", RuntimeItemLocations.Character, owner, ownerId: owner);
            var deathRetainedCurse = curseEngine.Create("item.657", RuntimeItemLocations.Character, owner, ownerId: owner);
            curseEngine.Equip(ordinaryCurse.InstanceId, owner, character);
            curseEngine.Equip(deathRetainedCurse.InstanceId, owner, character);
            try
            {
                curseEngine.Unequip(ordinaryCurse.InstanceId, owner);
                throw new InvalidOperationException("Decoded Curse ability 82 allowed ordinary unequip.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("cannot be removed", StringComparison.OrdinalIgnoreCase)) { }

            var removed = curseEngine.RemoveCurse(owner);
            if (removed.Count != 2 || removed.Any(item => item.EquippedSlot is not null))
                throw new InvalidOperationException("Decoded Rcrs did not remove every equipped cursed item.");

            curseEngine.Equip(ordinaryCurse.InstanceId, owner, character);
            curseEngine.Equip(deathRetainedCurse.InstanceId, owner, character);
            var corpse = curseEngine.CreateCorpse(owner, room, 0);
            var retained = curseEngine.Find(deathRetainedCurse.InstanceId);
            var dropped = curseEngine.Find(ordinaryCurse.InstanceId);
            if (retained is null || !string.Equals(retained.OwnerId, owner, StringComparison.OrdinalIgnoreCase) || retained.EquippedSlot is null ||
                dropped is null || !dropped.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) ||
                !dropped.LocationId.Equals(corpse.CorpseId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Decoded CURSED ability 83 death retention is disconnected from corpse creation.");
        });

        Check("decoded-loyal-item-death-retention", () =>
        {
            if (catalog.Items.Count(item => item.EquipmentAbilityModifiers.ContainsKey(100)) != 101 ||
                catalog.Items.Count(item => item.RetainedOnDeath) != 103)
                throw new InvalidOperationException("Decoded LoyalItem/CURSED death-retention inventory is incomplete.");
            var loyalEngine = new RuntimeItemEngine(catalog);
            var loyal = loyalEngine.Create("item.1554", RuntimeItemLocations.Character, owner, ownerId: owner);
            var ordinaryDefinition = catalog.Items.First(item => item.Gettable && !item.RetainedOnDeath);
            var ordinary = loyalEngine.Create(ordinaryDefinition.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
            var corpse = loyalEngine.CreateCorpse(owner, room, 0);
            var retained = loyalEngine.Find(loyal.InstanceId);
            var dropped = loyalEngine.Find(ordinary.InstanceId);
            if (retained is null || !string.Equals(retained.OwnerId, owner, StringComparison.OrdinalIgnoreCase) ||
                !retained.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) ||
                dropped is null || !dropped.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) ||
                !dropped.LocationId.Equals(corpse.CorpseId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Decoded LoyalItem did not remain with its owner through death.");
        });

        Check("blacknight-decoded-active-item", () =>
        {
            var blacknight = catalog.Require("item.1316");
            if (blacknight.Name != "blacknight" || blacknight.MaximumUses != 5 || blacknight.DefaultCharges != 5 ||
                !blacknight.RetainAfterUses || blacknight.UsesConsumedOnUse ||
                blacknight.EquipmentAbilityModifiers.GetValueOrDefault(43) != 1220 ||
                blacknight.EquipmentAbilityModifiers.GetValueOrDefault(180) != 20 ||
                blacknight.EquipmentAbilityModifiers.GetValueOrDefault(179) != 20 ||
                blacknight.EquipmentAbilityModifiers.GetValueOrDefault(27) != 10)
                throw new InvalidDataException("Blacknight active/passive decoded item contract is incomplete.");
            var blacknightEngine = new RuntimeItemEngine(catalog);
            var instance = blacknightEngine.Create(blacknight.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
            if (instance.Charges != 5 || blacknightEngine.SpendCharges(instance.InstanceId, owner).Charges != 4 ||
                blacknightEngine.Find(instance.InstanceId) is null)
                throw new InvalidOperationException("Blacknight use charge did not decrement while retaining the cloak.");
        });

        Check("decoded-shadowform-equipment-selector", () =>
        {
            var shroud = catalog.Require("item.1579");
            if (shroud.EquipmentAbilityModifiers.GetValueOrDefault(178) != 9652)
                throw new InvalidDataException("Death shroud lost decoded Shadowform textblock selector 9652.");
            var projected = RuntimeCharacterCombatStatisticProjector.Recalculate(character,
                new RuntimeEquipmentModifierTotals(0, 0, 0, 0, new[] { "instance.death-shroud" })
                {
                    AbilityModifiers = new Dictionary<int, int> { [178] = 9652 }
                }, projectedAtUtc: DateTimeOffset.UnixEpoch);
            if (projected.SemanticState?.DerivedStats.GetValueOrDefault("shadowform_textblock") != 9652)
                throw new InvalidOperationException("Decoded Shadowform equipment selector did not reach character projection.");
            var native = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
            if (RuntimeClassicCharacterAppearance.TryResolveShadowformOverride(projected, native, out _))
                throw new InvalidOperationException("Unresolved native Shadowform textblock 9652 must fail closed.");
        });

        Check("universal-defensive-equipment-ability-projection", () =>
        {
            var modifiers = new RuntimeEquipmentModifierTotals(0, 0, 0, 0, new[] { "instance.defensive" })
            {
                AbilityModifiers = new Dictionary<int, int> { [21] = 100, [36] = 20, [51] = 0 }
            };
            var projected = RuntimeCharacterCombatStatisticProjector.Recalculate(character, modifiers, projectedAtUtc: DateTimeOffset.UnixEpoch);
            var derived = projected.SemanticState!.DerivedStats;
            if (derived.GetValueOrDefault("magic_resistance_bonus") != 20 ||
                derived.GetValueOrDefault("anti_magic_rank") != 1 ||
                derived.GetValueOrDefault("poison_immunity_rank") != 100 ||
                !RuntimeClassicStatusProjection.IsPoisonImmune(projected))
                throw new InvalidOperationException("Decoded defensive equipment abilities did not reach their shared runtime projection.");
        });

        Check("universal-resource-equipment-ability-projection-and-removal", () =>
        {
            var resourceCharacter = character with { HitPoints = 90, MaximumHitPoints = 100, Mana = 45, MaximumMana = 50 };
            var equippedModifiers = new RuntimeEquipmentModifierTotals(0, 0, 0, 0, new[] { "instance.resource" })
            {
                AbilityModifiers = new Dictionary<int, int> { [69] = 20, [88] = 25, [96] = 10, [123] = 35, [145] = 15 }
            };
            var equippedProjection = RuntimeCharacterCombatStatisticProjector.Recalculate(resourceCharacter, equippedModifiers, projectedAtUtc: DateTimeOffset.UnixEpoch);
            var derived = equippedProjection.SemanticState!.DerivedStats;
            if (equippedProjection.MaximumHitPoints != 125 || equippedProjection.HitPoints != 90 ||
                equippedProjection.MaximumMana != 70 || equippedProjection.Mana != 45 ||
                derived.GetValueOrDefault("encumbrance_modifier_percent") != 10 ||
                derived.GetValueOrDefault("hit_point_regeneration_percent") != 35 ||
                derived.GetValueOrDefault("mana_regeneration_percent") != 15)
                throw new InvalidOperationException("Decoded resource equipment abilities did not project correctly.");

            var removedProjection = RuntimeCharacterCombatStatisticProjector.Recalculate(
                equippedProjection with { HitPoints = 125, Mana = 70 },
                new RuntimeEquipmentModifierTotals(0, 0, 0, 0, Array.Empty<string>()),
                projectedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1));
            if (removedProjection.MaximumHitPoints != 100 || removedProjection.HitPoints != 100 ||
                removedProjection.MaximumMana != 50 || removedProjection.Mana != 50)
                throw new InvalidOperationException("Removing resource equipment did not restore base maxima and clamp current resources.");
        });

        Check("martial-max-damage-projection-excludes-duplicate-quest-bonus", () =>
        {
            // The combat round adds QuestMaximumDamageBonus separately. The
            // projector retains quest + item ability 4 in its derived field.
            var quest = new Dictionary<string, string>(character.Conditions,
                StringComparer.OrdinalIgnoreCase) { ["quest.ability.4"] = "3" };
            var withQuest = character with { Conditions = quest,
                SemanticState = RuntimeCharacterSemanticState.Empty };
            var itemModifiers = new RuntimeEquipmentModifierTotals(0, 0, 0, 0,
                new[] { "martial:gear" })
            {
                AbilityModifiers = new Dictionary<int, int> { [4] = 7 }
            };
            var projected = RuntimeCharacterCombatStatisticProjector.Recalculate(
                withQuest, itemModifiers, projectedAtUtc: DateTimeOffset.UnixEpoch);
            var itemOnly = RuntimeClassicCharacterFormulaCalculator.ResolveProjectedEquipmentContribution(
                projected, "maximum_damage_bonus", 3);
            if (itemOnly != 7 || projected.SemanticState!.DerivedStats["maximum_damage_bonus"] != 10)
                throw new InvalidDataException("Martial damage double-counted quest maximum-damage bonus.");
            var unequipped = RuntimeCharacterCombatStatisticProjector.Recalculate(
                projected, new RuntimeEquipmentModifierTotals(0, 0, 0, 0, Array.Empty<string>()),
                projectedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1));
            if (RuntimeClassicCharacterFormulaCalculator.ResolveProjectedEquipmentContribution(
                    unequipped, "maximum_damage_bonus", 3) != 0)
                throw new InvalidDataException("Unready martial damage retained removed equipment ability 4.");
        });

        Check("player-combat-equipment-reprojection-does-not-stack-on-switch", () =>
        {
            var baseline = character with
            {
                HitPoints = 80, MaximumHitPoints = 100,
                Mana = 35, MaximumMana = 50,
                SemanticState = RuntimeCharacterSemanticState.Empty
            };
            var weaponA = new RuntimeEquipmentModifierTotals(3, 4, 5, 6, new[] { "switch.a" })
            {
                AbilityModifiers = new Dictionary<int, int>
                {
                    [4] = 7, [34] = 9, [88] = 25
                }
            };
            var weaponB = new RuntimeEquipmentModifierTotals(11, 12, 13, 14, new[] { "switch.b" })
            {
                AbilityModifiers = new Dictionary<int, int>
                {
                    [4] = 2, [34] = 3, [88] = 10
                }
            };
            var first = RuntimeCharacterCombatStatisticProjector.Recalculate(
                baseline, weaponA, projectedAtUtc: DateTimeOffset.UnixEpoch);
            var repeated = RuntimeCharacterCombatStatisticProjector.Recalculate(
                first, weaponA, projectedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1));
            var switched = RuntimeCharacterCombatStatisticProjector.Recalculate(
                repeated, weaponB, projectedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(2));
            var firstAgain = RuntimeCharacterCombatStatisticProjector.Recalculate(
                switched, weaponA, projectedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(3));
            var unready = RuntimeCharacterCombatStatisticProjector.Recalculate(
                firstAgain, new RuntimeEquipmentModifierTotals(0, 0, 0, 0,
                    Array.Empty<string>()), projectedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(4));
            var a = first.SemanticState!.DerivedStats;
            var re = repeated.SemanticState!.DerivedStats;
            var b = switched.SemanticState!.DerivedStats;
            var again = firstAgain.SemanticState!.DerivedStats;
            var none = unready.SemanticState!.DerivedStats;
            if (first.MaximumHitPoints != 125 || repeated.MaximumHitPoints != 125 ||
                switched.MaximumHitPoints != 110 || firstAgain.MaximumHitPoints != 125 ||
                unready.MaximumHitPoints != 100 || unready.HitPoints > 100)
                throw new InvalidDataException("Switching weapon equipment accumulated maximum HP bonuses.");
            foreach (var key in new[] { "armor_class_bonus", "damage_resistance_bonus",
                "accuracy_bonus", "speed_bonus", "maximum_damage_bonus", "dodge_bonus" })
            {
                if (a.GetValueOrDefault(key) != re.GetValueOrDefault(key)
                    || a.GetValueOrDefault(key) != again.GetValueOrDefault(key))
                    throw new InvalidDataException("Repeated weapon readiness duplicated " + key);
            }
            if (b.GetValueOrDefault("accuracy_bonus") != 13 ||
                b.GetValueOrDefault("armor_class_bonus") != 11 ||
                b.GetValueOrDefault("damage_resistance_bonus") != 12 ||
                b.GetValueOrDefault("maximum_damage_bonus") !=
                    none.GetValueOrDefault("maximum_damage_bonus") + 2 ||
                none.GetValueOrDefault("armor_class_bonus") != 0 ||
                none.GetValueOrDefault("accuracy_bonus") != 0 ||
                none.GetValueOrDefault("damage_resistance_bonus") != 0)
                throw new InvalidDataException("Unready or switched weapon retained the previous combat bonuses.");

            var firstSources = RuntimeCharacterCombatStatisticProjector.Snapshot(first).EquipmentSourceInstanceIds;
            var lastSources = RuntimeCharacterCombatStatisticProjector.Snapshot(unready).EquipmentSourceInstanceIds;
            if (firstSources.Count != 1 || firstSources[0] != "switch.a"
                || lastSources.Count != 0)
                throw new InvalidDataException("Readied equipment source IDs accumulated or remained after unready.");
        });

        Check("decoded-passive-regeneration-cadence-and-equipment-percentages", () =>
        {
            if (RuntimeCharacterRegenerationAuthority.PassiveCadence != TimeSpan.FromSeconds(30))
                throw new InvalidOperationException("Decoded passive regeneration cadence is not 30 seconds.");
            var regenerationCharacter = character with
            {
                Level = 10,
                HitPoints = 10,
                MaximumHitPoints = 100,
                Mana = 5,
                MaximumMana = 100,
                BaseStats = new Dictionary<string, int> { ["hea"] = 50, ["int"] = 50, ["wil"] = 50, ["cha"] = 50 },
                SemanticState = RuntimeCharacterSemanticState.Empty with
                {
                    DerivedStats = new Dictionary<string, int>
                    {
                        ["hit_point_regeneration_percent"] = 50,
                        ["mana_regeneration_percent"] = 100
                    }
                }
            };
            var caster = new RuntimeCharacterDefinition(
                "class.regen", "Regen", "test", "class",
                new Dictionary<string, int>(), new Dictionary<string, int>(),
                0, 0, 0, Array.Empty<RuntimeAbilityGrant>(), null, new RuntimeMagicDefinition(1, 3),
                0, 0, 0, 0, 0, 0, string.Empty, string.Empty, 0, "test");
            var tick = RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(regenerationCharacter, caster, DateTimeOffset.UnixEpoch);
            if (tick.HitPointRate != 3 || tick.ManaRate != 8 || tick.HitPointsRestored != 3 || tick.ManaRestored != 8)
                throw new InvalidOperationException($"Decoded regeneration formula mismatch: HP {tick.HitPointRate}, mana {tick.ManaRate}.");
            var spellModifiedConditions = new Dictionary<string, string>(regenerationCharacter.Conditions, StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeClassicStatusProjection.Prefix + "mana_regeneration"] = "25"
            };
            var spellModified = regenerationCharacter with { Conditions = spellModifiedConditions };
            var spellModifiedTick = RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(spellModified, caster, DateTimeOffset.UnixEpoch);
            if (RuntimeCharacterRegenerationAuthority.ResolveManaRegenerationPercent(spellModified) != 125 || spellModifiedTick.ManaRate != 9)
                throw new InvalidOperationException("Decoded spell ManaRgn percentage is not combined with equipment regeneration.");
            var suppressedConditions = new Dictionary<string, string>(regenerationCharacter.Conditions, StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeClassicStatusProjection.Prefix + "mana_regeneration"] = "-300"
            };
            var suppressed = RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(
                regenerationCharacter with { Conditions = suppressedConditions }, caster, DateTimeOffset.UnixEpoch);
            if (suppressed.ManaRate != -4 || suppressed.ManaRestored != -4 || suppressed.Character.Mana != 1)
                throw new InvalidOperationException("Native combined negative MPRegen arithmetic is not preserved.");
            var rest = RuntimeCharacterRegenerationAuthority.ApplyRestTick(regenerationCharacter, caster, DateTimeOffset.UnixEpoch);
            var meditate = RuntimeCharacterRegenerationAuthority.ApplyMeditateTick(regenerationCharacter, caster, DateTimeOffset.UnixEpoch);
            if (RuntimeCharacterRegenerationAuthority.FocusActivationDelay != TimeSpan.FromSeconds(20) ||
                RuntimeCharacterRegenerationAuthority.RestCadence != TimeSpan.FromSeconds(21) ||
                RuntimeCharacterRegenerationAuthority.MeditateCadence != TimeSpan.FromSeconds(15) ||
                rest.HitPointRate != 9 || meditate.ManaRate != 4 ||
                RuntimeCommand.Parse("rest").Name != "rest" || RuntimeCommand.Parse("meditate").Name != "meditate")
                throw new InvalidOperationException("Decoded REST/MEDITATE activation, cadence, formulas, or command routes are incomplete.");

            var poisonedConditions = new Dictionary<string, string>(regenerationCharacter.Conditions, StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeClassicStatusProjection.Prefix + "poisoned"] = "1"
            };
            if (!RuntimeClassicRegenerationCommandAuthority.IsTooSick(regenerationCharacter with { Conditions = poisonedConditions }) ||
                RuntimeClassicRegenerationCommandAuthority.IsTooSick(regenerationCharacter) ||
                RuntimeClassicRegenerationCommandAuthority.MeditationWillNotHelp(regenerationCharacter) ||
                !RuntimeClassicRegenerationCommandAuthority.MeditationWillNotHelp(regenerationCharacter with { Mana = 100 }) ||
                RuntimeClassicRegenerationCommandAuthority.RestTooSickMessage != "You are too sick to rest!" ||
                RuntimeClassicRegenerationCommandAuthority.MeditateTooSickMessage != "You are too sick to meditate!" ||
                RuntimeClassicRegenerationCommandAuthority.MeditationNotHelpfulMessage != "Meditation will not help at this time." ||
                RuntimeClassicRegenerationCommandAuthority.ObserverMessage("Tester", "rest") != "Tester stops to rest." ||
                RuntimeClassicRegenerationCommandAuthority.ObserverMessage("Tester", "meditate") != "Tester kneels to meditate.")
                throw new InvalidOperationException("Native REST/MEDITATE sickness, full-mana, or observer presentation is incomplete.");

            var kai = caster with { Magic = new RuntimeMagicDefinition(5, 0) };
            var kaiPassive = RuntimeCharacterRegenerationAuthority.ApplyPassiveTick(regenerationCharacter, kai, DateTimeOffset.UnixEpoch);
            var kaiMeditate = RuntimeCharacterRegenerationAuthority.ApplyMeditateTick(regenerationCharacter, kai, DateTimeOffset.UnixEpoch);
            if (kaiPassive.ManaRate != 2 || kaiMeditate.ManaRate != 1)
                throw new InvalidOperationException("Native Kai/type-5 passive/meditation MPRegen ordering is incomplete.");
            if (RuntimeClassicNativeSchedulerClockAudit.Run().Count != 6)
                throw new InvalidOperationException("Native scheduler clock regression audit did not complete.");
        });

        Check("decoded-timed-maximum-resource-and-encumbrance-effects", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var effects = new[]
            {
                new RuntimeActiveSpellEffect("max-mana", "spell.491", owner, new RuntimeSpellTarget("character", owner), 0, 0, 2400, now, now.AddHours(1)),
                new RuntimeActiveSpellEffect("max-hp", "spell.746", owner, new RuntimeSpellTarget("character", owner), 25, 25, 10, now, now.AddMinutes(1)),
                new RuntimeActiveSpellEffect("encumbrance", "spell.1236", owner, new RuntimeSpellTarget("character", owner), 100, 100, 1, now, now.AddMinutes(1))
            };
            var projected = RuntimeClassicStatusProjection.Apply(character, effects, statusCatalog, now, "smoke.resource-status");
            if (projected.MaximumMana != character.MaximumMana + 10 ||
                projected.MaximumHitPoints != character.MaximumHitPoints + 25 ||
                RuntimeClassicStatusProjection.Numeric(projected, "encumbrance") != 100)
                throw new InvalidOperationException("Timed maximum-resource or encumbrance projection is disconnected.");
            var restored = RuntimeClassicStatusProjection.Clear(projected, now.AddMinutes(2), "smoke.resource-status-clear");
            if (restored.MaximumMana != character.MaximumMana || restored.MaximumHitPoints != character.MaximumHitPoints)
                throw new InvalidOperationException("Timed maximum-resource removal did not restore base maxima.");
        });

        Check("decoded-rolled-status-effect-persistence", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var frail = new RuntimeActiveSpellEffect(
                "frail-roll", "spell.949", "npc.808", new RuntimeSpellTarget("character", owner),
                -500, -50, 15, now, now.AddSeconds(90),
                new Dictionary<string, string> { ["effect_roll"] = "-125" });
            var projected = RuntimeClassicStatusProjection.Apply(character, new[] { frail }, statusCatalog, now, "smoke.frail-roll");
            if (RuntimeClassicStatusProjection.Numeric(projected, "damage_resistance_percent") != -125)
                throw new InvalidOperationException("Rolled AlterDRpercent value was not retained in status projection.");
        });

        Check("decoded-confusion-chance-and-native-message", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var confusion = new RuntimeActiveSpellEffect(
                "confusion-roll", "spell.63", "npc.808", new RuntimeSpellTarget("character", owner),
                25, 25, 10, now, now.AddSeconds(60),
                new Dictionary<string, string> { ["effect_roll"] = "25" });
            var projected = RuntimeClassicStatusProjection.Apply(character, new[] { confusion }, statusCatalog, now, "smoke.confusion-roll");
            if (RuntimeClassicStatusProjection.Numeric(projected, "confusion_chance") != 25 ||
                RuntimeClassicStatusProjection.Numeric(projected, "confusion_message_id") != 8489 ||
                !RuntimeClassicStatusProjection.ConfusionFumbles(projected, 25) ||
                RuntimeClassicStatusProjection.ConfusionFumbles(projected, 26) ||
                RuntimeClassicStatusProjection.IsPhysicalAttackBlocked(projected) ||
                RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot).RequireMessage(8489) != "You fumble in confusion!")
                throw new InvalidOperationException("Decoded confusion percentage, fumble boundary, or native message is disconnected.");
        });

        Check("decoded-regeneration-and-negate-ability-coverage", () =>
        {
            if (catalog.Items.Count(item => item.EquipmentAbilityModifiers.ContainsKey(123)) != 25)
                throw new InvalidOperationException("Decoded item HPRegen owner count drifted from 25.");

            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var immunity = new RuntimeActiveSpellEffect(
                "confusion-immunity", "spell.745", owner, new RuntimeSpellTarget("character", owner),
                1, 1, 20, now, now.AddMinutes(2));
            var confusion = new RuntimeActiveSpellEffect(
                "confusion", "spell.63", "npc.808", new RuntimeSpellTarget("character", owner),
                25, 25, 10, now, now.AddMinutes(1),
                new Dictionary<string, string> { ["effect_roll"] = "25" });
            var projected = RuntimeClassicStatusProjection.Apply(character, new[] { confusion, immunity }, statusCatalog, now, "smoke.negate-ability");
            if (!projected.Conditions.ContainsKey(RuntimeClassicStatusProjection.Prefix + "negate_ability.71") ||
                RuntimeClassicStatusProjection.Numeric(projected, "confusion_chance") != 0)
                throw new InvalidOperationException("Decoded NegateAbility 71 did not suppress Confusion while preserving its immunity marker.");

            using var npcDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot, "Data", worldId, "npcs.json")));
            var negatingNpcs = npcDocument.RootElement.GetProperty("records").EnumerateArray()
                .Select(RuntimeNpcDefinition.FromJson)
                .Where(npc => npc.NegatedAbilityIds.Count > 0)
                .ToArray();
            if (negatingNpcs.Length != 2 || negatingNpcs.Any(npc => !npc.NegatedAbilityIds.SequenceEqual(new[] { 71 })))
                throw new InvalidOperationException("Decoded NPC NegateAbility owners did not resolve to both Tyrannosaur Confusion immunities.");

            var spells = RuntimeSpellCatalog.Load(projectRoot, worldId);
            var confusionSpell = spells.Require("spell.63");
            var immuneNpc = new RuntimeLivingNpcState(
                "npc.negate", "spawn.negate", negatingNpcs[0].Id, negatingNpcs[0].Name, room, room,
                10, 10, true, null, "hostile", 5,
                new Dictionary<string, string> { ["negated_ability_ids"] = "71" });
            if (RuntimeLiveGameplayFacade.IsDecodedSpellTargetAdmitted(
                    confusionSpell, new RuntimeSpellTarget("npc", immuneNpc.InstanceId), immuneNpc))
                throw new InvalidOperationException("Decoded NPC NegateAbility 71 admitted a Confusion spell.");
        });

        Check("decoded-fear-forces-combat-flee", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var fear = new RuntimeActiveSpellEffect(
                "fear", "spell.60", "npc.808", new RuntimeSpellTarget("character", owner),
                35, 90, 10, now, now.AddSeconds(60),
                new Dictionary<string, string> { ["effect_roll"] = "50" });
            var projected = RuntimeClassicStatusProjection.Apply(character, new[] { fear }, statusCatalog, now, "smoke.fear");
            if (!RuntimeClassicStatusProjection.MustFleeCombat(projected) ||
                RuntimeClassicStatusProjection.IsPhysicalAttackBlocked(projected) ||
                RuntimeClassicStatusProjection.IsMovementBlocked(projected))
                throw new InvalidOperationException("Decoded Fear did not force combat flee through the existing flee path.");
        });

        Check("decoded-hold-person-immobilizes-without-paralyzing", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var hold = new RuntimeActiveSpellEffect(
                "hold", "spell.66", "npc.808", new RuntimeSpellTarget("character", owner),
                1, 1, 4, now, now.AddSeconds(24));
            var projected = RuntimeClassicStatusProjection.Apply(character, new[] { hold }, statusCatalog, now, "smoke.hold-person");
            if (!RuntimeClassicStatusProjection.Flag(projected, "immobilized") ||
                RuntimeClassicStatusProjection.Flag(projected, "paralyzed") ||
                !RuntimeClassicStatusProjection.IsMovementBlocked(projected) ||
                RuntimeClassicStatusProjection.IsPhysicalAttackBlocked(projected) ||
                RuntimeClassicStatusProjection.IsSpellCastBlocked(projected) ||
                RuntimePlayerCombatProfileResolver.IsPhysicalAttackSuppressed(projected) ||
                !RuntimePlayerCombatProfileResolver.IsAvoidanceSuppressed(projected))
                throw new InvalidOperationException("Decoded HoldPerson did not remain a movement-only immobilization status.");
        });

        Check("decoded-perception-status-feeds-hidden-search", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            RuntimeActiveSpellEffect Effect(string id, string spellId, int roll) => new(
                id, spellId, owner, new RuntimeSpellTarget("character", owner),
                roll, roll, 40, now, now.AddSeconds(240),
                new Dictionary<string, string> { ["effect_roll"] = roll.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            var baseline = RuntimeLiveGameplayFacade.ResolveEffectiveSearchPerception(character);
            var alert = RuntimeClassicStatusProjection.Apply(character, new[] { Effect("alert", "spell.91", 10) }, statusCatalog, now, "smoke.perception-alert");
            var feeblemind = RuntimeClassicStatusProjection.Apply(character, new[] { Effect("feeblemind", "spell.423", -15) }, statusCatalog, now, "smoke.perception-feeblemind");
            if (RuntimeClassicStatusProjection.Numeric(alert, "perception") != 10 ||
                RuntimeLiveGameplayFacade.ResolveEffectiveSearchPerception(alert) != checked(baseline + 10) ||
                RuntimeClassicStatusProjection.Numeric(feeblemind, "perception") != -20 ||
                RuntimeLiveGameplayFacade.ResolveEffectiveSearchPerception(feeblemind) != checked(baseline - 20))
                throw new InvalidOperationException("Decoded Percep values are disconnected from effective hidden-search Perception.");
        });

        Check("decoded-affects-animals-target-admission", () =>
        {
            var spells = RuntimeSpellCatalog.Load(projectRoot, worldId);
            var charmAnimal = spells.Require("spell.92");
            var animal = new RuntimeLivingNpcState(
                "npc.animal", "spawn.animal", "npc.786", "black cat", room, room,
                10, 10, true, null, "hostile", 5,
                new Dictionary<string, string> { ["animal"] = "1" });
            var nonAnimal = animal with
            {
                InstanceId = "npc.non-animal",
                NpcId = "npc.1",
                Name = "non-animal",
                Metadata = new Dictionary<string, string> { ["animal"] = "0" }
            };
            if (!RuntimeLiveGameplayFacade.IsDecodedSpellTargetAdmitted(charmAnimal, new RuntimeSpellTarget("npc", animal.InstanceId), animal) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellTargetAdmitted(charmAnimal, new RuntimeSpellTarget("npc", nonAnimal.InstanceId), nonAnimal) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellTargetAdmitted(charmAnimal, new RuntimeSpellTarget("character", owner), null))
                throw new InvalidOperationException("AffectsAnimals did not enforce the decoded Animal-only target boundary.");
        });

        Check("decoded-spell-alignment-admission", () =>
        {
            var spells = RuntimeSpellCatalog.Load(projectRoot, worldId);
            var goodSpell = spells.Require("spell.107");
            var evilSpell = spells.Require("spell.108");
            var notGoodSpell = spells.Require("spell.111");
            var notEvilSpell = spells.Require("spell.110");
            var neutralSpell = spells.Require("spell.1099");
            RuntimeCharacterRecord Aligned(string alignment) => character with
            {
                Conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
                {
                    ["alignment"] = alignment
                }
            };
            if (!RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(goodSpell, Aligned("Good"), out _) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(goodSpell, Aligned("Neutral"), out _) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(goodSpell, Aligned("Fiend"), out _) ||
                !RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(evilSpell, Aligned("Fiend"), out _) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(evilSpell, Aligned("Neutral"), out _) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(evilSpell, Aligned("Saint"), out _) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(notGoodSpell, Aligned("Saint"), out _) ||
                !RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(notGoodSpell, Aligned("Seedy"), out _) ||
                !RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(notGoodSpell, Aligned("Fiend"), out _) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(notEvilSpell, Aligned("Villain"), out _) ||
                !RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(notEvilSpell, Aligned("Lawful"), out _) ||
                !RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(notEvilSpell, Aligned("Neutral"), out _) ||
                !RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(neutralSpell, Aligned("Neutral"), out _) ||
                !RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(neutralSpell, Aligned("Seedy"), out _) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(neutralSpell, Aligned("Good"), out _) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellCasterAlignmentAdmitted(neutralSpell, Aligned("Fiend"), out _))
                throw new InvalidOperationException("Decoded Good/Evil spell requirements are disconnected from caster admission.");
        });

        Check("decoded-affects-living-target-admission", () =>
        {
            var spells = RuntimeSpellCatalog.Load(projectRoot, worldId);
            var affectsLiving = spells.Require("spell.22");
            using var npcDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot, "Data", worldId, "npcs.json")));
            var npcDefinitions = npcDocument.RootElement.GetProperty("records").EnumerateArray()
                .Select(RuntimeNpcDefinition.FromJson)
                .ToArray();
            var nonLivingCount = npcDefinitions.Count(npc => npc.NonLiving);
            var nonLivingNotUndeadCount = npcDefinitions.Count(npc => npc.NonLiving && !npc.Undead);
            if (nonLivingCount != 184 || nonLivingNotUndeadCount != 83)
                throw new InvalidOperationException(
                    $"Decoded NonLiving NPC classification drifted from its authoritative ability relationships: " +
                    $"nonliving={nonLivingCount}, nonliving-not-undead={nonLivingNotUndeadCount}.");
            var living = new RuntimeLivingNpcState(
                "npc.living", "spawn.living", "npc.1", "living target", room, room,
                10, 10, true, null, "hostile", 5,
                new Dictionary<string, string> { ["non_living"] = "0" });
            var nonLiving = living with
            {
                InstanceId = "npc.non-living",
                NpcId = "npc.5",
                Name = "acid slime",
                Metadata = new Dictionary<string, string> { ["non_living"] = "1" }
            };
            if (!RuntimeLiveGameplayFacade.IsDecodedSpellTargetAdmitted(affectsLiving, new RuntimeSpellTarget("character", owner), null) ||
                !RuntimeLiveGameplayFacade.IsDecodedSpellTargetAdmitted(affectsLiving, new RuntimeSpellTarget("npc", living.InstanceId), living) ||
                RuntimeLiveGameplayFacade.IsDecodedSpellTargetAdmitted(affectsLiving, new RuntimeSpellTarget("npc", nonLiving.InstanceId), nonLiving))
                throw new InvalidOperationException("AffectsLiving did not enforce the decoded NonLiving target boundary.");
        });

        Check("decoded-spell-backstab-accuracy-projection", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var camouflage = new RuntimeActiveSpellEffect(
                "camouflage", "spell.1314", owner, new RuntimeSpellTarget("character", owner),
                1, 1, 10, now, now.AddSeconds(60));
            var projected = RuntimeClassicStatusProjection.Apply(character, new[] { camouflage }, statusCatalog, now, "smoke.backstab-accuracy");
            if (RuntimeClassicStatusProjection.Numeric(projected, "backstab_accuracy") != 10)
                throw new InvalidOperationException("Decoded camouflage BSAccu +10 is disconnected from live status projection.");
        });

        Check("decoded-spell-backstab-damage-range-projection", () =>
        {
            using var itemDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot, "Data", worldId, "items.json")));
            static int[] AbilityValues(JsonElement root, int abilityId) => root.GetProperty("records").EnumerateArray()
                .Where(item => item.TryGetProperty("public_reference_0139", out var reference)
                    && reference.TryGetProperty("fields", out var fields)
                    && fields.TryGetProperty("abilities_named", out var abilities)
                    && abilities.ValueKind == JsonValueKind.Array)
                .SelectMany(item => item.GetProperty("public_reference_0139").GetProperty("fields").GetProperty("abilities_named").EnumerateArray())
                .Where(ability => ability.GetProperty("ability_id").GetInt32() == abilityId)
                .Select(ability => int.Parse(ability.GetProperty("value").GetString()!, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            var minimumValues = AbilityValues(itemDocument.RootElement, 117);
            var maximumValues = AbilityValues(itemDocument.RootElement, 118);
            if (minimumValues.Length != 47 || minimumValues.Count(value => value < 0) != 4 || minimumValues.Count(value => value > 0) != 43 ||
                maximumValues.Length != 35 || maximumValues.Count(value => value < 0) != 2 || maximumValues.Count(value => value > 0) != 33)
                throw new InvalidOperationException("Decoded item backstab damage relationship distribution drifted.");
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            RuntimeActiveSpellEffect Effect(string id, string spellId, int roll) => new(
                id, spellId, owner, new RuntimeSpellTarget("character", owner),
                roll, roll, 10, now, now.AddSeconds(60),
                new Dictionary<string, string> { ["effect_roll"] = roll.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            var mantle = RuntimeClassicStatusProjection.Apply(character, new[] { Effect("mantle", "spell.869", 25) }, statusCatalog, now, "smoke.backstab-range");
            var zeroPair = RuntimeClassicStatusProjection.Apply(character, new[] { Effect("cat", "spell.39", 0) }, statusCatalog, now, "smoke.backstab-zero-range");
            if (RuntimeClassicStatusProjection.Numeric(mantle, "backstab_minimum_damage") != 25 ||
                RuntimeClassicStatusProjection.Numeric(mantle, "backstab_maximum_damage") != 25 ||
                RuntimeClassicStatusProjection.Numeric(zeroPair, "backstab_minimum_damage") != 0 ||
                RuntimeClassicStatusProjection.Numeric(zeroPair, "backstab_maximum_damage") != 0)
                throw new InvalidOperationException("Decoded spell backstab minimum/maximum damage projection is disconnected.");
        });

        Check("decoded-mute-and-mage-bind-separation", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            RuntimeActiveSpellEffect Effect(string id, string spellId) => new(
                id, spellId, "npc.808", new RuntimeSpellTarget("character", owner),
                1, 1, 10, now, now.AddSeconds(60));
            var muted = RuntimeClassicStatusProjection.Apply(character, new[] { Effect("mute", "spell.68") }, statusCatalog, now, "smoke.mute");
            var mageBound = RuntimeClassicStatusProjection.Apply(character, new[] { Effect("bind", "spell.860") }, statusCatalog, now, "smoke.mage-bind");
            if (!RuntimeClassicStatusProjection.IsSpeechBlocked(muted) || RuntimeClassicStatusProjection.IsSpellCastBlocked(muted) ||
                RuntimeClassicStatusProjection.IsSpeechBlocked(mageBound) || RuntimeClassicStatusProjection.IsSpellCastBlocked(mageBound) ||
                !RuntimeClassicStatusProjection.Flag(mageBound, "mage_bind_evidence"))
                throw new InvalidOperationException("Decoded Mute and unresolved MageBind evidence were incorrectly merged.");
        });

        Check("decoded-blind-user-presentation-boundary", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var blind = new RuntimeActiveSpellEffect(
                "blind", "spell.77", "npc.808", new RuntimeSpellTarget("character", owner),
                1, 1, 10, now, now.AddSeconds(60));
            var projected = RuntimeClassicStatusProjection.Apply(character, new[] { blind }, statusCatalog, now, "smoke.blind");
            if (!RuntimeClassicStatusProjection.IsVisionBlocked(projected) ||
                RuntimeClassicStatusProjection.IsMovementBlocked(projected) ||
                RuntimeClassicStatusProjection.IsPhysicalAttackBlocked(projected) ||
                RuntimeClassicStatusProjection.IsSpellCastBlocked(projected))
                throw new InvalidOperationException("Decoded BlindUser was not isolated to its visual-presentation boundary.");
        });

        Check("decoded-spell-immunity-inclusive-level-boundary", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var now = DateTimeOffset.UnixEpoch;
            var globe = new RuntimeActiveSpellEffect(
                "globe", "spell.307", owner, new RuntimeSpellTarget("character", owner),
                100, 100, 500, now, now.AddSeconds(3000));
            var projected = RuntimeClassicStatusProjection.Apply(character, new[] { globe }, statusCatalog, now, "smoke.spell-immunity");
            if (RuntimeClassicStatusProjection.Numeric(projected, "spell_immunity_level") != 9999 ||
                RuntimeClassicStatusProjection.Numeric(projected, "hit_magic") != 10 ||
                !RuntimeClassicStatusProjection.IsSpellImmune(projected, 0) ||
                !RuntimeClassicStatusProjection.IsSpellImmune(projected, 9999) ||
                RuntimeClassicStatusProjection.IsSpellImmune(projected, 10000))
                throw new InvalidOperationException("Decoded SpellImmu inclusive spell-level boundary is disconnected.");
        });

        Check("decoded-ability-scoped-and-general-dispel-classification", () =>
        {
            var statusCatalog = RuntimeClassicLiveStatusCatalog.Load(projectRoot, worldId);
            var cureBlindness = statusCatalog.Definitions.Single(x => x.SpellId == "spell.87");
            var dispelMagic = statusCatalog.Definitions.Single(x => x.SpellId == "spell.65");
            var antidote = statusCatalog.Definitions.Single(x => x.SpellId == "spell.31");
            if (cureBlindness.GeneralMagicDispel || !cureBlindness.DispelledAbilityIds.SequenceEqual(new[] { 107 }) ||
                !dispelMagic.GeneralMagicDispel || dispelMagic.DispelledAbilityIds.Count != 0 ||
                antidote.GeneralMagicDispel || !antidote.DispelledAbilityIds.SequenceEqual(new[] { 19 }))
                throw new InvalidOperationException("Decoded DispellMagic ability targeting was collapsed into a broad dispel.");
        });

        Check("decoded-alignment-equipment-and-retaliation-effects", () =>
        {
            var modifiers = new RuntimeEquipmentModifierTotals(0, 0, 0, 0, new[] { "instance.alignment-defense" })
            {
                AbilityModifiers = new Dictionary<int, int> { [24] = 15, [25] = 9, [72] = 6 }
            };
            var projected = RuntimeCharacterCombatStatisticProjector.Recalculate(character, modifiers, projectedAtUtc: DateTimeOffset.UnixEpoch);
            if (RuntimeEquipmentCombatEffects.ResolveAlignmentProtection(projected, "Evil") != 15 ||
                RuntimeEquipmentCombatEffects.ResolveAlignmentProtection(projected, "Good") != 9 ||
                RuntimeEquipmentCombatEffects.ResolveAlignmentProtection(projected, "Neutral") != 0 ||
                RuntimeEquipmentCombatEffects.ResolveDamageShieldRetaliation(projected, 1) != 6 ||
                RuntimeEquipmentCombatEffects.ResolveDamageShieldRetaliation(projected, 0) != 0)
                throw new InvalidOperationException("Decoded alignment protection or hit-only damage-shield timing is disconnected.");

            var native = RuntimeClassicNativeTextblockPayloadAuthority.Load(projectRoot);
            var shockIds = catalog.Items
                .Where(item => item.EquipmentAbilityModifiers.GetValueOrDefault(72) > 0)
                .Select(item => item.EquipmentAbilityModifiers.GetValueOrDefault(137))
                .ToArray();
            if (shockIds.Length != 8 || shockIds.Any(id => id <= 0 || !native.TryResolveMessage(id, out _)))
                throw new InvalidOperationException("Decoded equipment damage-shield message selectors are incomplete.");
            var visibleAtMaintenance = catalog.Items.Where(item => item.VisibleAtCleanup).ToArray();
            if (visibleAtMaintenance.Length != 16 || visibleAtMaintenance.Any(item => item.Gettable))
                throw new InvalidOperationException("Decoded Visible@Maint fixtures are disconnected or include a gettable item.");
            if (RuntimeEquipmentCombatEffects.FormatDamageShieldMessage(
                    native.RequireMessage(3051), "a goblin", 6) != "The shield spike stabs a goblin for 6 damage!")
                throw new InvalidOperationException("Decoded damage-shield native message formatting is disconnected.");

            var goodCharacter = character with
            {
                SemanticState = RuntimeCharacterSemanticState.Empty with
                {
                    Alignment = RuntimeCharacterAlignmentResolver.CreateInitial() with { Reputation = "Saint" }
                }
            };
            var goodOnly = catalog.Items.First(x => x.EquipmentAbilityModifiers.ContainsKey(97));
            RuntimeItemEngine.EnforceAlignmentAbilityRequirements(goodOnly, goodCharacter);
            var rejected = false;
            try
            {
                RuntimeItemEngine.EnforceAlignmentAbilityRequirements(
                    goodOnly,
                    goodCharacter with { SemanticState = goodCharacter.SemanticState! with { Alignment = goodCharacter.SemanticState.Alignment! with { Reputation = "Outlaw" } } });
            }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Good-only decoded item accepted an Evil-aligned character.");

            var requiredIds = new[] { 97, 98, 110, 111, 112, 113 };
            if (requiredIds.Any(id => !catalog.Items.Any(item => item.EquipmentAbilityModifiers.ContainsKey(id))))
                throw new InvalidDataException("One or more decoded alignment restriction ability families are absent from generated items.");
        });

        Check("decoded-martial-equipment-modifiers", () =>
        {
            var modifiers = new RuntimeEquipmentModifierTotals(0, 0, 0, 0, new[] { "instance.martial" })
            {
                AbilityModifiers = new Dictionary<int, int>
                {
                    [89] = 3, [90] = 4, [91] = 5,
                    [92] = 6, [93] = 7, [94] = 8
                }
            };
            var projected = RuntimeCharacterCombatStatisticProjector.Recalculate(character, modifiers, projectedAtUtc: DateTimeOffset.UnixEpoch);
            if (RuntimeEquipmentCombatEffects.ResolveMartialAccuracy(projected, ClassicAttackType.Punch) != 3 ||
                RuntimeEquipmentCombatEffects.ResolveMartialAccuracy(projected, ClassicAttackType.Kick) != 4 ||
                RuntimeEquipmentCombatEffects.ResolveMartialAccuracy(projected, ClassicAttackType.JumpKick) != 5 ||
                RuntimeEquipmentCombatEffects.ResolveMartialDamage(projected, ClassicAttackType.Punch) != 6 ||
                RuntimeEquipmentCombatEffects.ResolveMartialDamage(projected, ClassicAttackType.Kick) != 7 ||
                RuntimeEquipmentCombatEffects.ResolveMartialDamage(projected, ClassicAttackType.JumpKick) != 8 ||
                RuntimeEquipmentCombatEffects.ResolveMartialDamage(projected, ClassicAttackType.NormalWeapon) != 0)
                throw new InvalidOperationException("Decoded martial modifiers did not remain scoped to their matching attack family.");
            if (RuntimeCommand.Parse("punch target").Name != "punch" ||
                RuntimeCommand.Parse("kick target").Name != "kick" ||
                RuntimeCommand.Parse("jumpkick target").Name != "jumpkick" ||
                RuntimeCommand.Parse("jk target").Name != "jumpkick")
                throw new InvalidOperationException("Classic martial command routes are incomplete.");
            var requiredIds = new[] { 89, 90, 91, 92, 93, 94 };
            if (requiredIds.Any(id => !catalog.Items.Any(item => item.EquipmentAbilityModifiers.ContainsKey(id))))
                throw new InvalidDataException("One or more decoded martial equipment modifier families are absent from generated items.");
        });

        RuntimeItemInstance? item = null;
        Check("create-ground", () => item = engine.Create(gettable.Id, RuntimeItemLocations.Room, room));
        Check("get", () => engine.Get(item!.InstanceId, owner));
        Check("find-lookup", () => { if (engine.Find(item!.InstanceId) is null) throw new InvalidOperationException("lookup failed"); });
        Check("drop", () => engine.Drop(item!.InstanceId, owner, room));
        Check("get-all", () => { engine.GetAll(room, owner); if (!engine.Inventory(owner).Any(x => x.InstanceId == item!.InstanceId)) throw new InvalidOperationException("get all failed"); });
        Check("give", () => engine.Give(item!.InstanceId, owner, other));
        Check("give-back", () => engine.Give(item!.InstanceId, other, owner));

        RuntimeItemInstance? equipment = null;
        Check("equip", () => { equipment = engine.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner); engine.Equip(equipment.InstanceId, owner, character); });
        Check("unequip", () => engine.Unequip(equipment!.InstanceId, owner));
        Check("wear-remove", () => { engine.Wear(equipment!.InstanceId, owner, character); engine.RemoveEquipped(equipment.InstanceId, owner); });

        Check("corpse-exclusive-loot-permission-and-expiry", () =>
        {
            var permissionEngine = new RuntimeItemEngine(catalog);
            var protectedLoot = permissionEngine.Create(gettable.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
            var protectedCorpse = permissionEngine.CreateCorpse(owner, room, 77, TimeSpan.FromMinutes(10), owner, TimeSpan.FromMinutes(2));
            var beforeExpiry = protectedCorpse.CreatedAtUtc.AddMinutes(1);
            var afterExpiry = protectedCorpse.CreatedAtUtc.AddMinutes(3);
            if (!permissionEngine.CanLootCorpse(protectedCorpse.CorpseId, owner, beforeExpiry)) throw new InvalidOperationException("corpse owner was denied during exclusive loot window");
            if (permissionEngine.CanLootCorpse(protectedCorpse.CorpseId, other, beforeExpiry)) throw new InvalidOperationException("non-owner was allowed during exclusive loot window");
            try { permissionEngine.Loot(protectedLoot.InstanceId, protectedCorpse.CorpseId, other); throw new InvalidOperationException("exclusive corpse item reservation was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("reserved", StringComparison.OrdinalIgnoreCase)) { }
            try { permissionEngine.LootGold(protectedCorpse.CorpseId, other, beforeExpiry); throw new InvalidOperationException("exclusive corpse gold reservation was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("reserved", StringComparison.OrdinalIgnoreCase)) { }
            if (!permissionEngine.CanLootCorpse(protectedCorpse.CorpseId, other, afterExpiry)) throw new InvalidOperationException("corpse reservation did not expire");
            if (permissionEngine.LootGold(protectedCorpse.CorpseId, other, afterExpiry) != 77) throw new InvalidOperationException("authorized corpse gold loot failed after expiry");
        });

        if (containerDef is not null)
        {
            RuntimeItemInstance? container = null;
            Check("container-put-get", () => { container = engine.Create(containerDef.Id, RuntimeItemLocations.Character, owner, ownerId: owner, metadata: new Dictionary<string, string> { ["open"] = "true", ["capacity"] = "2" }); engine.Put(item!.InstanceId, owner, container.InstanceId); engine.GetFromContainer(item.InstanceId, owner, container.InstanceId); });
            Check("container-open-close", () => { engine.Close(container!.InstanceId, owner); engine.Open(container.InstanceId, owner); });
            Check("classic-cleanup-container-subtree-delete", () =>
            {
                var cleanupEngine = new RuntimeItemEngine(catalog);
                var root = cleanupEngine.Create(containerDef.Id, RuntimeItemLocations.Room, room, metadata: new Dictionary<string, string> { ["open"] = "true" });
                var child = cleanupEngine.Create(gettable.Id, RuntimeItemLocations.Container, root.InstanceId);
                cleanupEngine.RemoveForClassicCleanup(root.InstanceId);
                if (cleanupEngine.Find(root.InstanceId) is not null || cleanupEngine.Find(child.InstanceId) is not null)
                    throw new InvalidOperationException("Classic maintenance deletion orphaned container contents.");
                if (!cleanupEngine.Validate().Passed)
                    throw new InvalidOperationException("Classic maintenance subtree deletion left invalid item state.");
            });
        }

        RuntimeCorpseState? corpse = null;
        Check("corpse-create-transfer", () => { corpse = engine.CreateCorpse(owner, room, 123); if (!engine.Items.Any(x => x.LocationKind == RuntimeItemLocations.Corpse && x.LocationId == corpse.CorpseId)) throw new InvalidOperationException("corpse transfer failed"); });
        Check("corpse-loot", () =>
        {
            var corpseState = corpse ?? throw new InvalidOperationException("corpse was not created");
            var corpseItem = engine.Items.First(x => x.LocationKind == RuntimeItemLocations.Corpse && x.LocationId == corpseState.CorpseId);
            engine.Loot(corpseItem.InstanceId, corpseState.CorpseId, owner);
            if (engine.LootGold(corpseState.CorpseId) != 123) throw new InvalidOperationException("gold transfer failed");
        });

        Check("stack-split-merge-partial-transfer", () =>
        {
            var stackable = catalog.Items.FirstOrDefault(x => x.Stackable);
            if (stackable is null) return;
            var stack = engine.Create(stackable.Id, RuntimeItemLocations.Character, owner, 5, owner, new Dictionary<string, string> { ["maximum_stack"] = "10" });
            var split = engine.SplitStack(stack.InstanceId, 2);
            if (engine.RequireInstance(stack.InstanceId).StackQuantity != 3 || split.StackQuantity != 2) throw new InvalidOperationException("split quantities were not preserved");
            var moved = engine.TransferQuantity(stack.InstanceId, 1, RuntimeItemLocations.Character, other, other);
            if (moved.StackQuantity != 1 || !string.Equals(moved.OwnerId, other, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("partial transfer failed");
            var merged = engine.MergeStacks(stack.InstanceId, split.InstanceId);
            if (merged.StackQuantity != 4 || engine.Find(split.InstanceId) is not null) throw new InvalidOperationException("merge identity failed");
        });

        Check("charges-consumption-and-recharge-rollback", () =>
        {
            var chargeItem = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 2, 1, null, new Dictionary<string, string> { ["maximum_charges"] = "3" }));
            engine.SpendCharges(chargeItem.InstanceId, owner);
            if (engine.RequireInstance(chargeItem.InstanceId).Charges != 1) throw new InvalidOperationException("charge spending failed");
            engine.Recharge(chargeItem.InstanceId, owner, 2);
            if (engine.RequireInstance(chargeItem.InstanceId).Charges != 3) throw new InvalidOperationException("recharge failed");
            try { engine.Recharge(chargeItem.InstanceId, owner, 1); throw new InvalidOperationException("charge overflow accepted"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("maximum", StringComparison.OrdinalIgnoreCase)) { }
            if (engine.RequireInstance(chargeItem.InstanceId).Charges != 3) throw new InvalidOperationException("recharge rollback failed");

            var consumable = catalog.Items.FirstOrDefault(x => x.Consumable);
            if (consumable is not null)
            {
                var quantity = consumable.Stackable ? 2 : 1;
                var consumedItem = engine.Create(consumable.Id, RuntimeItemLocations.Character, owner, quantity, owner);
                var result = engine.ConsumeQuantity(consumedItem.InstanceId, owner);
                if (quantity == 1 && (!result.Destroyed || engine.Find(consumedItem.InstanceId) is not null)) throw new InvalidOperationException("consumable destruction failed");
                if (quantity == 2 && (result.Destroyed || engine.RequireInstance(consumedItem.InstanceId).StackQuantity != 1)) throw new InvalidOperationException("consumable decrement failed");
            }
        });
        Check("decoded-cleanup-recharge-ability-121-cap-and-cadence", () =>
        {
            var rechargeDefinitions = catalog.Items.Where(definition => definition.RechargeAtCleanup > 0).ToArray();
            if (rechargeDefinitions.Length != 78
                || rechargeDefinitions.Count(definition => definition.MaximumUses > 0) != 77
                || rechargeDefinitions.Single(definition => definition.MaximumUses <= 0).Id != "item.1851")
                throw new InvalidOperationException("Decoded Recharge ability 121 ownership or fail-closed capacity boundary drifted.");
            var boneNecklace = catalog.Require("item.1187");
            if (boneNecklace.MaximumUses != 10 || boneNecklace.RechargeAtCleanup != 5)
                throw new InvalidOperationException("Bone necklace recharge/capacity evidence drifted.");
            var rechargeEngine = new RuntimeItemEngine(catalog);
            var charged = rechargeEngine.Create(boneNecklace.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
            rechargeEngine.SpendCharges(charged.InstanceId, owner, 7);
            var firstCleanup = DateTimeOffset.UnixEpoch;
            if (rechargeEngine.RechargeForClassicCleanup(charged.InstanceId, firstCleanup).Charges != 8
                || rechargeEngine.RechargeForClassicCleanup(charged.InstanceId, firstCleanup).Charges != 8
                || rechargeEngine.RechargeForClassicCleanup(charged.InstanceId, firstCleanup.AddDays(1)).Charges != 10)
                throw new InvalidOperationException("Classic cleanup recharge did not add the decoded amount and cap at maximum uses.");
        });

        Check("durability-damage-break-and-repair", () =>
        {
            var durable = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, 3, new Dictionary<string, string> { ["maximum_durability"] = "3" }));
            var damaged = engine.DamageDurability(durable.InstanceId, owner, 2);
            if (damaged.CurrentDurability != 1 || damaged.Broken) throw new InvalidOperationException("durability damage failed");
            var broken = engine.DamageDurability(durable.InstanceId, owner, 5);
            if (!broken.Broken || broken.CurrentDurability != 0) throw new InvalidOperationException("break transition failed");
            if (engine.RequireInstance(durable.InstanceId).Metadata?.GetValueOrDefault("broken") != "true") throw new InvalidOperationException("broken state was not persisted");
            var repaired = engine.RepairDurability(durable.InstanceId, owner, 2);
            if (repaired.CurrentDurability != 2 || repaired.Broken) throw new InvalidOperationException("repair failed");
            if (engine.RequireInstance(durable.InstanceId).Metadata?.ContainsKey("broken") == true) throw new InvalidOperationException("repair did not clear broken state");
            engine.RepairDurability(durable.InstanceId, owner, 99);
            if (engine.RequireInstance(durable.InstanceId).Durability != 3) throw new InvalidOperationException("repair maximum was not enforced");
        });

        Check("equipment-and-transfer-restriction-integrity", () =>
        {
            var restrictedDrop = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null, new Dictionary<string, string> { ["no_drop"] = "true" }));
            try { engine.Drop(restrictedDrop.InstanceId, owner, room); throw new InvalidOperationException("no-drop restriction was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("restriction", StringComparison.OrdinalIgnoreCase)) { }
            if (!string.Equals(engine.RequireInstance(restrictedDrop.InstanceId).OwnerId, owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("restricted transfer rollback failed");

            var brokenEquipment = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), equipDef.ItemId, owner, RuntimeItemLocations.Character, owner, null, 0, 1, 0, new Dictionary<string, string> { ["maximum_durability"] = "1", ["broken"] = "true" }));
            try { engine.Equip(brokenEquipment.InstanceId, owner, character); throw new InvalidOperationException("broken item was equipped"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Broken", StringComparison.OrdinalIgnoreCase)) { }

            var lockedEquipment = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), equipDef.ItemId, other, RuntimeItemLocations.Character, other, null, 0, 1, null, new Dictionary<string, string> { ["no_remove"] = "true" }));
            engine.Equip(lockedEquipment.InstanceId, other, SmokeCharacter(other, worldId));
            try { engine.Unequip(lockedEquipment.InstanceId, other); throw new InvalidOperationException("no-remove restriction was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("cannot be removed", StringComparison.OrdinalIgnoreCase)) { }
            try { engine.Give(lockedEquipment.InstanceId, other, owner); throw new InvalidOperationException("equipped item was transferred"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("carried and unequipped", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("binding-quest-trade-and-destruction-integrity", () =>
        {
            var bound = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null, new Dictionary<string, string> { ["bound_owner_id"] = owner }));
            try { engine.Give(bound.InstanceId, owner, other); throw new InvalidOperationException("bound ownership restriction was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Bound item", StringComparison.OrdinalIgnoreCase)) { }
            if (!string.Equals(engine.RequireInstance(bound.InstanceId).OwnerId, owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("bound transfer rollback failed");

            var noTrade = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null, new Dictionary<string, string> { ["no_trade"] = "true" }));
            try { engine.MoveToTrade(noTrade.InstanceId, owner, "smoke:trade"); throw new InvalidOperationException("no-trade restriction was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("restriction", StringComparison.OrdinalIgnoreCase)) { }

            var protectedItem = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null, new Dictionary<string, string> { ["quest_protected"] = "true", ["quest_owner_id"] = owner }));
            try { engine.Destroy(protectedItem.InstanceId, owner); throw new InvalidOperationException("quest destruction protection was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Quest-protected", StringComparison.OrdinalIgnoreCase)) { }
            var removed = engine.AdministrativeRemove(protectedItem.InstanceId);
            if (!removed.AdministrativeOverride || engine.Find(protectedItem.InstanceId) is not null) throw new InvalidOperationException("administrative removal failed");

            var destroyable = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null));
            var destroyed = engine.Destroy(destroyable.InstanceId, owner);
            if (destroyed.AdministrativeOverride || engine.Find(destroyable.InstanceId) is not null) throw new InvalidOperationException("ordinary destruction failed");
        });

        Check("bank-and-merchant-transfer-integrity", () =>
        {
            const string bankId = "smoke:bank";
            var bankable = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null));
            engine.Deposit(bankable.InstanceId, owner, bankId);
            if (!engine.Banked(owner, bankId).Any(x => x.InstanceId == bankable.InstanceId)) throw new InvalidOperationException("bank deposit failed");
            engine.Withdraw(bankable.InstanceId, owner, bankId);
            if (!engine.Inventory(owner).Any(x => x.InstanceId == bankable.InstanceId)) throw new InvalidOperationException("bank withdrawal failed");

            var noBank = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null, new Dictionary<string, string> { ["no_bank"] = "true" }));
            try { engine.Deposit(noBank.InstanceId, owner, bankId); throw new InvalidOperationException("no-bank restriction was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("restriction", StringComparison.OrdinalIgnoreCase)) { }

            var sellable = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null));
            engine.SellToMerchant(sellable.InstanceId, owner, merchantId);
            if (!engine.MerchantStock(merchantId).Any(x => x.InstanceId == sellable.InstanceId)) throw new InvalidOperationException("merchant stock transfer failed");
            engine.BuyFromMerchant(sellable.InstanceId, merchantId, other);
            if (!engine.Inventory(other).Any(x => x.InstanceId == sellable.InstanceId)) throw new InvalidOperationException("merchant purchase transfer failed");

            var noSell = engine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null, new Dictionary<string, string> { ["no_sell"] = "true" }));
            try { engine.SellToMerchant(noSell.InstanceId, owner, merchantId); throw new InvalidOperationException("no-sell restriction was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("restriction", StringComparison.OrdinalIgnoreCase)) { }
            if (!string.Equals(engine.RequireInstance(noSell.InstanceId).OwnerId, owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("merchant rejection rollback failed");

            var stackable = catalog.Items.FirstOrDefault(x => x.Stackable);
            if (stackable is not null)
            {
                var bankStack = engine.Create(stackable.Id, RuntimeItemLocations.Character, owner, 5, owner, new Dictionary<string, string> { ["maximum_stack"] = "10" });
                var bankPart = engine.Deposit(bankStack.InstanceId, owner, bankId, 2);
                if (bankPart.StackQuantity != 2 || engine.RequireInstance(bankStack.InstanceId).StackQuantity != 3) throw new InvalidOperationException("partial bank deposit failed");
                var withdrawnPart = engine.Withdraw(bankPart.InstanceId, owner, bankId, 1);
                if (withdrawnPart.StackQuantity != 1 || engine.RequireInstance(bankPart.InstanceId).StackQuantity != 1) throw new InvalidOperationException("partial bank withdrawal failed");

                var merchantStack = engine.Create(stackable.Id, RuntimeItemLocations.Character, owner, 4, owner, new Dictionary<string, string> { ["maximum_stack"] = "10" });
                var soldPart = engine.SellToMerchant(merchantStack.InstanceId, owner, merchantId, 2);
                if (soldPart.StackQuantity != 2 || engine.RequireInstance(merchantStack.InstanceId).StackQuantity != 2) throw new InvalidOperationException("partial merchant sale failed");
                var boughtPart = engine.BuyFromMerchant(soldPart.InstanceId, merchantId, other, 1);
                if (boughtPart.StackQuantity != 1 || engine.RequireInstance(soldPart.InstanceId).StackQuantity != 1) throw new InvalidOperationException("partial merchant purchase failed");

                var mergeMetadata = new Dictionary<string, string> { ["maximum_stack"] = "5" };
                var existingBankStack = engine.Create(stackable.Id, RuntimeItemLocations.Bank, bankId, 3, owner, mergeMetadata);
                var mergeSource = engine.Create(stackable.Id, RuntimeItemLocations.Character, owner, 4, owner, mergeMetadata);
                var overflow = engine.Deposit(mergeSource.InstanceId, owner, bankId, 4);
                if (engine.RequireInstance(existingBankStack.InstanceId).StackQuantity != 5) throw new InvalidOperationException("destination stack merge failed");
                if (overflow.InstanceId != mergeSource.InstanceId || overflow.StackQuantity != 2) throw new InvalidOperationException("destination stack overflow identity failed");

                var absorbingTarget = engine.Create(stackable.Id, RuntimeItemLocations.Character, other, 4, other, mergeMetadata);
                var merchantMergeSource = engine.Create(stackable.Id, RuntimeItemLocations.Merchant, merchantId, 1, merchantId, mergeMetadata);
                var absorbed = engine.BuyFromMerchant(merchantMergeSource.InstanceId, merchantId, other, 1);
                if (absorbed.InstanceId != absorbingTarget.InstanceId || absorbed.StackQuantity != 5 || engine.Find(merchantMergeSource.InstanceId) is not null) throw new InvalidOperationException("full destination stack absorption failed");
            }
        });

        Check("owner-quantity-limit-and-release", () =>
        {
            var limitEngine = new RuntimeItemEngine(catalog);
            var limitedMetadata = new Dictionary<string, string> { ["maximum_owned_quantity"] = "1" };
            var limited = limitEngine.Create(gettable.Id, RuntimeItemLocations.Character, owner, 1, owner, limitedMetadata);
            var roomCopy = limitEngine.Create(gettable.Id, RuntimeItemLocations.Room, "smoke:limited-room", 1, null, limitedMetadata);
            try { limitEngine.Get(roomCopy.InstanceId, owner); throw new InvalidOperationException("owner quantity limit was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("quantity limit", StringComparison.OrdinalIgnoreCase)) { }
            if (!limitEngine.RequireInstance(roomCopy.InstanceId).LocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("limited transfer rollback failed");
            limitEngine.Drop(limited.InstanceId, owner, "smoke:limited-room");
            var acquired = limitEngine.Get(roomCopy.InstanceId, owner);
            if (!string.Equals(acquired.OwnerId, owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("released owner quantity reservation was not reusable");
        });

        Check("corpse-resurrection-item-restoration", () =>
        {
            var resurrection = new RuntimeItemEngine(catalog);
            var carried = resurrection.Create(gettable.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
            var corpse = resurrection.CreateCorpse(owner, room, 0);
            var restored = resurrection.RestoreCorpseItems(corpse.CorpseId, owner);
            if (restored.RestoredInstanceCount != 1 || restored.RestoredGold != 0 || !restored.RestoredRootInstanceIds.Contains(carried.InstanceId)) throw new InvalidOperationException("resurrection restoration result was incomplete");
            var recovered = resurrection.RequireInstance(carried.InstanceId);
            if (!recovered.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase) || !string.Equals(recovered.OwnerId, owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("resurrection did not restore item ownership");
            if (!restored.CorpseFinalized || !resurrection.Corpses.Single(x => x.CorpseId == corpse.CorpseId).CleanedUp) throw new InvalidOperationException("empty resurrection corpse was not finalized");
        });

        Check("corpse-resurrection-explicit-equipment-restoration", () =>
        {
            var carriedOnly = new RuntimeItemEngine(catalog);
            var carriedOnlyEquipment = carriedOnly.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner);
            carriedOnly.Equip(carriedOnlyEquipment.InstanceId, owner, character);
            var carriedOnlyCorpse = carriedOnly.CreateCorpse(owner, room, 0);
            var corpseEquipment = carriedOnly.RequireInstance(carriedOnlyEquipment.InstanceId);
            if (corpseEquipment.Metadata?.TryGetValue("death_original_equipped_slot", out var recordedSlot) != true || !string.Equals(recordedSlot, equipDef.Slot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("death equipment slot was not recorded");
            carriedOnly.RestoreCorpseItems(carriedOnlyCorpse.CorpseId, owner);
            var safelyRestored = carriedOnly.RequireInstance(carriedOnlyEquipment.InstanceId);
            if (safelyRestored.EquippedSlot is not null || !safelyRestored.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("default resurrection restoration did not remain carried and unequipped");
            if (safelyRestored.Metadata?.ContainsKey("death_original_equipped_slot") == true) throw new InvalidOperationException("default restoration retained stale death equipment metadata");

            var equippedRestore = new RuntimeItemEngine(catalog);
            var equipped = equippedRestore.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner);
            equippedRestore.Equip(equipped.InstanceId, owner, character);
            var equippedCorpse = equippedRestore.CreateCorpse(owner, room, 0);
            var result = equippedRestore.RestoreCorpseForResurrection(equippedCorpse.CorpseId, owner, false, true, character);
            var restoredEquipment = equippedRestore.RequireInstance(equipped.InstanceId);
            if (!string.Equals(restoredEquipment.EquippedSlot, equipDef.Slot, StringComparison.OrdinalIgnoreCase) || !restoredEquipment.LocationKind.Equals(RuntimeItemLocations.Equipment, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("explicit resurrection equipment restoration failed");
            if (!result.CorpseFinalized) throw new InvalidOperationException("equipment restoration did not finalize empty corpse");

            var denied = new RuntimeItemEngine(catalog);
            var deniedEquipment = denied.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner);
            denied.Equip(deniedEquipment.InstanceId, owner, character);
            var deniedCorpse = denied.CreateCorpse(owner, room, 0);
            try { denied.RestoreCorpseForResurrection(deniedCorpse.CorpseId, owner, false, true, null); throw new InvalidOperationException("equipment restoration without a character record was accepted"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("character record", StringComparison.OrdinalIgnoreCase)) { }
            var rolledBack = denied.RequireInstance(deniedEquipment.InstanceId);
            if (!rolledBack.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) || !string.Equals(rolledBack.LocationId, deniedCorpse.CorpseId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("failed equipment restoration did not roll back");
        });

        Check("corpse-resurrection-equipment-conflict-displacement", () =>
        {
            var conflictEngine = new RuntimeItemEngine(catalog);
            var original = conflictEngine.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner);
            conflictEngine.Equip(original.InstanceId, owner, character);
            var corpse = conflictEngine.CreateCorpse(owner, room, 0);
            var replacement = conflictEngine.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner);
            conflictEngine.Equip(replacement.InstanceId, owner, character);
            try
            {
                conflictEngine.RestoreCorpseForResurrection(corpse.CorpseId, owner, false, true, character);
                throw new InvalidOperationException("default resurrection restoration displaced occupied equipment");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("occupied", StringComparison.OrdinalIgnoreCase)) { }
            if (!conflictEngine.RequireInstance(original.InstanceId).LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("failed default conflict restoration did not roll back corpse item");
            if (!string.Equals(conflictEngine.RequireInstance(replacement.InstanceId).EquippedSlot, equipDef.Slot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("failed default conflict restoration mutated replacement equipment");

            var displaced = conflictEngine.RestoreCorpseForResurrection(corpse.CorpseId, owner, false, true, character, true);
            var restored = conflictEngine.RequireInstance(original.InstanceId);
            var unequippedReplacement = conflictEngine.RequireInstance(replacement.InstanceId);
            if (!string.Equals(restored.EquippedSlot, equipDef.Slot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("authorized conflict displacement did not restore original equipment");
            if (unequippedReplacement.EquippedSlot is not null || !unequippedReplacement.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("authorized conflict displacement did not carry displaced equipment");
            if (displaced.DisplacedEquipmentInstanceIds is null || !displaced.DisplacedEquipmentInstanceIds.Contains(replacement.InstanceId, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("displaced equipment identity was not reported");

            var blocked = new RuntimeItemEngine(catalog);
            var blockedOriginal = blocked.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner);
            blocked.Equip(blockedOriginal.InstanceId, owner, character);
            var blockedCorpse = blocked.CreateCorpse(owner, room, 0);
            var blockedReplacement = blocked.Create(equipDef.ItemId, RuntimeItemLocations.Character, owner, ownerId: owner, metadata: new Dictionary<string, string> { ["no_remove"] = "true" });
            blocked.Equip(blockedReplacement.InstanceId, owner, character);
            try
            {
                blocked.RestoreCorpseForResurrection(blockedCorpse.CorpseId, owner, false, true, character, true);
                throw new InvalidOperationException("no-remove equipment was displaced during resurrection");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("cannot be removed", StringComparison.OrdinalIgnoreCase)) { }
            if (!blocked.RequireInstance(blockedOriginal.InstanceId).LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("blocked displacement did not roll back corpse item");
            if (!string.Equals(blocked.RequireInstance(blockedReplacement.InstanceId).EquippedSlot, equipDef.Slot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("blocked displacement mutated protected equipment");
        });

        Check("corpse-resurrection-explicit-gold-restoration", () =>
        {
            var resurrection = new RuntimeItemEngine(catalog);
            resurrection.Create(gettable.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
            var corpse = resurrection.CreateCorpse(owner, room, 125);
            var itemOnly = resurrection.RestoreCorpseItems(corpse.CorpseId, owner);
            if (itemOnly.RestoredGold != 0 || itemOnly.CorpseFinalized) throw new InvalidOperationException("item-only restoration consumed or finalized corpse gold");
            if (resurrection.Corpses.Single(x => x.CorpseId == corpse.CorpseId).Gold != 125) throw new InvalidOperationException("item-only restoration mutated corpse gold");
            var withGold = resurrection.RestoreCorpseForResurrection(corpse.CorpseId, owner, true);
            if (withGold.RestoredGold != 125 || !withGold.CorpseFinalized) throw new InvalidOperationException("explicit resurrection gold restoration failed");
            if (!resurrection.Corpses.Single(x => x.CorpseId == corpse.CorpseId).CleanedUp) throw new InvalidOperationException("gold-empty resurrection corpse was not finalized");
            try
            {
                var denied = new RuntimeItemEngine(catalog);
                var deniedCorpse = denied.CreateCorpse(owner, room, 1);
                denied.RestoreCorpseForResurrection(deniedCorpse.CorpseId, other, true);
                throw new InvalidOperationException("non-owner resurrection restoration was accepted");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("corpse character", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("corpse-limited-item-reservation-and-release", () =>
        {
            var limitedMetadata = new Dictionary<string, string> { ["maximum_owned_quantity"] = "1" };
            var reservation = new RuntimeItemEngine(catalog);
            reservation.Create(gettable.Id, RuntimeItemLocations.Character, owner, 1, owner, limitedMetadata);
            var reservedCorpse = reservation.CreateCorpse(owner, room, 0, TimeSpan.Zero);
            if (!string.Equals(reservedCorpse.LimitedItemReservationOwnerId, owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("corpse reservation owner was not persisted");
            try
            {
                reservation.Create(gettable.Id, RuntimeItemLocations.Character, owner, 1, owner, limitedMetadata);
                throw new InvalidOperationException("corpse-held limited item did not reserve owner quantity");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("quantity limit", StringComparison.OrdinalIgnoreCase)) { }
            reservation.CleanupExpiredCorpses(DateTimeOffset.UtcNow.AddSeconds(1));
            var replacement = reservation.Create(gettable.Id, RuntimeItemLocations.Character, owner, 1, owner, limitedMetadata);
            if (!string.Equals(replacement.OwnerId, owner, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("corpse decay did not release limited-item reservation");
        });

        Check("corpse-death-protection-and-partial-stack-loot", () =>
        {
            var protectedEngine = new RuntimeItemEngine(catalog);
            var protectedItem = protectedEngine.Add(new RuntimeItemInstance(Guid.NewGuid().ToString("N"), gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null, new Dictionary<string, string> { ["death_protected"] = "true" }));
            var protectedCorpse = protectedEngine.CreateCorpse(owner, room, 0);
            var retained = protectedEngine.RequireInstance(protectedItem.InstanceId);
            if (!string.Equals(retained.OwnerId, owner, StringComparison.OrdinalIgnoreCase) || !retained.LocationKind.Equals(RuntimeItemLocations.Character, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("death-protected item was transferred to corpse");

            var stackable = catalog.Items.FirstOrDefault(x => x.Stackable);
            if (stackable is null) return;
            var corpseStackEngine = new RuntimeItemEngine(catalog);
            var stack = corpseStackEngine.Create(stackable.Id, RuntimeItemLocations.Character, owner, 5, owner, new Dictionary<string, string> { ["maximum_stack"] = "10" });
            var stackCorpse = corpseStackEngine.CreateCorpse(owner, room, 0);
            var looted = corpseStackEngine.Loot(stack.InstanceId, stackCorpse.CorpseId, other, 2);
            var remainder = corpseStackEngine.RequireInstance(stack.InstanceId);
            if (remainder.StackQuantity != 3 || !remainder.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("partial corpse loot did not preserve source remainder");
            if (looted.StackQuantity != 2 || !string.Equals(looted.OwnerId, other, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("partial corpse loot did not transfer requested quantity");
            if (looted.InstanceId == stack.InstanceId) throw new InvalidOperationException("partial corpse loot reused source identity");
        });

        if (containerDef is not null)
        {
            Check("corpse-container-hierarchy-preservation", () =>
            {
                var hierarchy = new RuntimeItemEngine(catalog);
                var outer = hierarchy.Create(containerDef.Id, RuntimeItemLocations.Character, owner, ownerId: owner, metadata: new Dictionary<string, string> { ["open"] = "true", ["capacity"] = "4" });
                var inner = hierarchy.Create(containerDef.Id, RuntimeItemLocations.Character, owner, ownerId: owner, metadata: new Dictionary<string, string> { ["open"] = "true", ["capacity"] = "4" });
                var leaf = hierarchy.Create(gettable.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
                hierarchy.Put(inner.InstanceId, owner, outer.InstanceId);
                hierarchy.Put(leaf.InstanceId, owner, inner.InstanceId);
                var hierarchyCorpse = hierarchy.CreateCorpse(owner, room, 0);
                var corpseOuter = hierarchy.RequireInstance(outer.InstanceId);
                var corpseInner = hierarchy.RequireInstance(inner.InstanceId);
                var corpseLeaf = hierarchy.RequireInstance(leaf.InstanceId);
                if (!corpseOuter.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) || corpseOuter.LocationId != hierarchyCorpse.CorpseId) throw new InvalidOperationException("outer container was not transferred to corpse");
                if (!corpseInner.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) || corpseInner.LocationId != outer.InstanceId) throw new InvalidOperationException("nested container hierarchy was flattened");
                if (!corpseLeaf.LocationKind.Equals(RuntimeItemLocations.Container, StringComparison.OrdinalIgnoreCase) || corpseLeaf.LocationId != inner.InstanceId) throw new InvalidOperationException("nested item hierarchy was flattened");
                if (corpseInner.OwnerId is not null || corpseLeaf.OwnerId is not null) throw new InvalidOperationException("corpse-contained ownership was not cleared");
                hierarchy.Loot(outer.InstanceId, hierarchyCorpse.CorpseId, other);
                if (!string.Equals(hierarchy.RequireInstance(inner.InstanceId).OwnerId, other, StringComparison.OrdinalIgnoreCase) || !string.Equals(hierarchy.RequireInstance(leaf.InstanceId).OwnerId, other, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("loot did not restore descendant ownership");
            });

            Check("corpse-decay-recursive-container-cleanup", () =>
            {
                var decay = new RuntimeItemEngine(catalog);
                var outer = decay.Create(containerDef.Id, RuntimeItemLocations.Character, owner, ownerId: owner, metadata: new Dictionary<string, string> { ["open"] = "true", ["capacity"] = "4" });
                var inner = decay.Create(containerDef.Id, RuntimeItemLocations.Character, owner, ownerId: owner, metadata: new Dictionary<string, string> { ["open"] = "true", ["capacity"] = "4" });
                var leaf = decay.Create(gettable.Id, RuntimeItemLocations.Character, owner, ownerId: owner);
                decay.Put(inner.InstanceId, owner, outer.InstanceId);
                decay.Put(leaf.InstanceId, owner, inner.InstanceId);
                decay.CreateCorpse(owner, room, 0, TimeSpan.Zero);
                if (decay.CleanupExpiredCorpses(DateTimeOffset.UtcNow.AddSeconds(1)) != 1) throw new InvalidOperationException("corpse cleanup did not execute");
                if (decay.Find(outer.InstanceId) is not null || decay.Find(inner.InstanceId) is not null || decay.Find(leaf.InstanceId) is not null) throw new InvalidOperationException("corpse decay orphaned nested contents");
            });
        }

        Check("merchant-restock-target-and-identity", () =>
        {
            var restock = new RuntimeItemEngine(catalog);
            var first = restock.RestockMerchantStock(gettable.Id, merchantId, "smoke.basic", 2);
            if (first.ExistingQuantity != 0 || first.CreatedQuantity != 2 || first.FinalQuantity != 2) throw new InvalidOperationException("merchant restock did not reach projected target");
            var beforeIds = restock.MerchantStock(merchantId).Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var second = restock.RestockMerchantStock(gettable.Id, merchantId, "smoke.basic", 2);
            var afterIds = restock.MerchantStock(merchantId).Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (second.CreatedQuantity != 0 || second.FinalQuantity != 2) throw new InvalidOperationException("idempotent merchant restock created duplicate stock");
            if (!beforeIds.SequenceEqual(afterIds, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("idempotent merchant restock changed stock identities");
            try
            {
                restock.RestockMerchantStock(gettable.Id, merchantId, "smoke.basic", 1);
                throw new InvalidOperationException("merchant restock silently removed stock above target");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not remove", StringComparison.OrdinalIgnoreCase)) { }
            if (restock.MerchantStock(merchantId).Sum(x => x.StackQuantity) != 2) throw new InvalidOperationException("failed restock target rollback changed merchant stock");
        });

        Check("room-item-reset-target-and-identity", () =>
        {
            var reset = new RuntimeItemEngine(catalog);
            var first = reset.ResetRoomItem(gettable.Id, room, "smoke.room.basic", 2);
            if (first.ExistingQuantity != 0 || first.CreatedQuantity != 2 || first.FinalQuantity != 2) throw new InvalidOperationException("room item reset did not reach projected target");
            var beforeIds = reset.Ground(room).Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var second = reset.ResetRoomItem(gettable.Id, room, "smoke.room.basic", 2);
            var afterIds = reset.Ground(room).Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (second.CreatedQuantity != 0 || second.FinalQuantity != 2) throw new InvalidOperationException("idempotent room item reset created duplicate instances");
            if (!beforeIds.SequenceEqual(afterIds, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("idempotent room item reset changed instance identities");
            try
            {
                reset.ResetRoomItem(gettable.Id, room, "smoke.room.basic", 1);
                throw new InvalidOperationException("room item reset silently removed instances above target");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not remove", StringComparison.OrdinalIgnoreCase)) { }
            if (reset.Ground(room).Sum(x => x.StackQuantity) != 2) throw new InvalidOperationException("failed room item reset rollback changed room inventory");
        });

        if (containerDef is not null)
        {
            Check("portable-container-restock-target-and-identity", () =>
            {
                var restock = new RuntimeItemEngine(catalog);
                var container = restock.Create(containerDef.Id, RuntimeItemLocations.Character, owner, ownerId: owner, metadata: new Dictionary<string, string> { ["open"] = "true", ["capacity"] = "4" });
                var first = restock.RestockPortableContainer(gettable.Id, container.InstanceId, owner, "smoke.container.basic", 2);
                if (first.ExistingQuantity != 0 || first.CreatedQuantity != 2 || first.FinalQuantity != 2) throw new InvalidOperationException("container restock did not reach projected target");
                var beforeIds = restock.Contents(container.InstanceId).Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                var second = restock.RestockPortableContainer(gettable.Id, container.InstanceId, owner, "smoke.container.basic", 2);
                var afterIds = restock.Contents(container.InstanceId).Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                if (second.CreatedQuantity != 0 || second.FinalQuantity != 2) throw new InvalidOperationException("idempotent container restock created duplicate contents");
                if (!beforeIds.SequenceEqual(afterIds, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("idempotent container restock changed content identities");
                try { restock.RestockPortableContainer(gettable.Id, container.InstanceId, owner, "smoke.container.basic", 1); throw new InvalidOperationException("container restock silently removed contents above target"); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("does not remove", StringComparison.OrdinalIgnoreCase)) { }
                if (restock.Contents(container.InstanceId).Sum(x => x.StackQuantity) != 2) throw new InvalidOperationException("failed container restock rollback changed contents");
            });
        }

        Check("npc-inventory-reset-target-and-identity", () =>
        {
            var reset = new RuntimeItemEngine(catalog);
            var first = reset.ResetNpcItem(gettable.Id, "smoke:npc", "smoke.npc.inventory", 2);
            if (first.ExistingQuantity != 0 || first.CreatedQuantity != 2 || first.FinalQuantity != 2) throw new InvalidOperationException("NPC inventory reset did not reach projected target");
            var beforeIds = reset.Items.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) && x.LocationId == "smoke:npc").Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var second = reset.ResetNpcItem(gettable.Id, "smoke:npc", "smoke.npc.inventory", 2);
            var afterIds = reset.Items.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) && x.LocationId == "smoke:npc").Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (second.CreatedQuantity != 0 || !beforeIds.SequenceEqual(afterIds, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("idempotent NPC inventory reset changed identities");
            try { reset.ResetNpcItem(gettable.Id, "smoke:npc", "smoke.npc.inventory", 1); throw new InvalidOperationException("NPC inventory reset silently removed excess state"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not remove", StringComparison.OrdinalIgnoreCase)) { }
            if (reset.Items.Where(x => x.LocationId == "smoke:npc").Sum(x => x.StackQuantity) != 2) throw new InvalidOperationException("failed NPC reset rollback changed inventory");
        });

        Check("npc-death-corpse-transfer-and-retention", () =>
        {
            var npc = new RuntimeItemEngine(catalog);
            var drop = npc.Create(gettable.Id, RuntimeItemLocations.Npc, "smoke:npc:death", ownerId: "smoke:npc:death");
            var retained = npc.Create(gettable.Id, RuntimeItemLocations.Npc, "smoke:npc:death", ownerId: "smoke:npc:death", metadata: new Dictionary<string, string> { ["npc_no_drop"] = "true" });
            var result = npc.CreateNpcCorpse("smoke:npc:death", "smoke:room");
            var moved = npc.Items.Single(x => x.InstanceId == drop.InstanceId);
            var kept = npc.Items.Single(x => x.InstanceId == retained.InstanceId);
            if (!moved.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) || moved.LocationId != result.CorpseId || moved.OwnerId is not null) throw new InvalidOperationException("NPC drop was not transferred to corpse ownership");
            if (!kept.LocationKind.Equals(RuntimeItemLocations.Npc, StringComparison.OrdinalIgnoreCase) || kept.OwnerId != "smoke:npc:death") throw new InvalidOperationException("NPC no-drop item was not retained");
            if (result.TransferredInstanceCount != 1 || !result.RetainedInstanceIds.Contains(retained.InstanceId, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("NPC corpse transfer result was incomplete");
        });

        Check("npc-corpse-loot-target-and-identity", () =>
        {
            var loot = new RuntimeItemEngine(catalog);
            var corpse = loot.CreateNpcCorpse("smoke:npc:loot", "smoke:room");
            var first = loot.ReconcileNpcCorpseLoot(corpse.CorpseId, gettable.Id, "smoke.loot.guaranteed", 2);
            if (first.ExistingQuantity != 0 || first.CreatedQuantity != 2 || first.FinalQuantity != 2) throw new InvalidOperationException("NPC corpse loot did not reach projected target");
            var beforeIds = loot.Items.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && x.LocationId == corpse.CorpseId).Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var second = loot.ReconcileNpcCorpseLoot(corpse.CorpseId, gettable.Id, "smoke.loot.guaranteed", 2);
            var afterIds = loot.Items.Where(x => x.LocationKind.Equals(RuntimeItemLocations.Corpse, StringComparison.OrdinalIgnoreCase) && x.LocationId == corpse.CorpseId).Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (second.CreatedQuantity != 0 || !beforeIds.SequenceEqual(afterIds, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("idempotent NPC corpse loot reconciliation changed identities");
            try { loot.ReconcileNpcCorpseLoot(corpse.CorpseId, gettable.Id, "smoke.loot.guaranteed", 1); throw new InvalidOperationException("NPC corpse loot silently removed excess state"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not remove", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("deterministic-npc-random-loot-table", () =>
        {
            var loot = new RuntimeItemEngine(catalog);
            var corpse = loot.CreateNpcCorpse("smoke:npc:random-loot", "smoke:room:random-loot");
            var entries = new[]
            {
                new RuntimeNpcLootRollEntry(gettable.Id, "common", 10000, 1, 2),
                new RuntimeNpcLootRollEntry(gettable.Id, "never", 0, 1, 1)
            };
            var first = loot.ReconcileNpcCorpseLootTable(corpse.CorpseId, "smoke.table.random", 194, entries);
            var ids = loot.Items.Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var second = loot.ReconcileNpcCorpseLootTable(corpse.CorpseId, "smoke.table.random", 194, entries);
            var idsAfter = loot.Items.Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (first.Outcomes[0].RollBasisPoints != second.Outcomes[0].RollBasisPoints || first.Outcomes[0].TargetQuantity != second.Outcomes[0].TargetQuantity) throw new InvalidOperationException("deterministic NPC loot-table evaluation changed roll outcome");
            if (!ids.SequenceEqual(idsAfter, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("repeated NPC loot-table reconciliation changed identities");
            if (!first.Outcomes[0].Awarded || first.Outcomes[1].Awarded) throw new InvalidOperationException("NPC loot chance boundaries were not enforced");
            try { loot.ReconcileNpcCorpseLootTable(corpse.CorpseId, "smoke.table.invalid", 194, new[] { new RuntimeNpcLootRollEntry(gettable.Id, "bad", 10001, 1, 1) }); throw new InvalidOperationException("invalid NPC loot chance was accepted"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("chance", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("npc-corpse-gold-deterministic-idempotent-and-conflict-safe", () =>
        {
            var gold = new RuntimeItemEngine(catalog);
            var corpse = gold.CreateNpcCorpse("smoke:npc:gold", "room:smoke");
            var first = gold.ReconcileNpcCorpseGold(corpse.CorpseId, "smoke.gold.table", 195, 10, 25);
            var second = gold.ReconcileNpcCorpseGold(corpse.CorpseId, "smoke.gold.table", 195, 10, 25);
            if (first.TargetGold < 10 || first.TargetGold > 25 || first.FinalGold != second.FinalGold) throw new InvalidOperationException("deterministic NPC corpse gold changed outcome");
            if (!first.Created || second.Created) throw new InvalidOperationException("NPC corpse gold idempotency reporting is incorrect");
            try { gold.ReconcileNpcCorpseGold(corpse.CorpseId, "smoke.gold.conflict", 196, 100, 100); throw new InvalidOperationException("conflicting NPC corpse gold was overwritten"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("conflicting", StringComparison.OrdinalIgnoreCase)) { }
            if (gold.Corpses.Single(x => x.CorpseId == corpse.CorpseId).Gold != first.FinalGold) throw new InvalidOperationException("failed NPC corpse gold reconciliation changed corpse state");
            try { gold.ReconcileNpcCorpseGold(corpse.CorpseId, "smoke.gold.invalid", 195, 5, 4); throw new InvalidOperationException("invalid NPC corpse gold range was accepted"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("range", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("quest-item-grant-consume-and-rollback", () =>
        {
            var quest = new RuntimeItemEngine(catalog);
            var grant = quest.GrantQuestItem("smoke:quest", owner, gettable.Id, "smoke.quest.reward", 2);
            if (grant.CreatedQuantity != 2 || grant.FinalQuantity != 2) throw new InvalidOperationException("quest grant did not reach target quantity");
            var beforeIds = quest.Items.Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var again = quest.GrantQuestItem("smoke:quest", owner, gettable.Id, "smoke.quest.reward", 2);
            var afterIds = quest.Items.Select(x => x.InstanceId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (again.CreatedQuantity != 0 || !beforeIds.SequenceEqual(afterIds, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("idempotent quest grant changed identities");
            try { quest.ConsumeQuestItem("smoke:quest", owner, gettable.Id, "smoke.quest.consume", 3); throw new InvalidOperationException("quest consume accepted insufficient quantity"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("exceeds", StringComparison.OrdinalIgnoreCase)) { }
            if (quest.Items.Sum(x => x.StackQuantity) != 2) throw new InvalidOperationException("failed quest consume changed inventory");
            var consumed = quest.ConsumeQuestItem("smoke:quest", owner, gettable.Id, "smoke.quest.consume", 1);
            if (consumed.ConsumedQuantity != 1 || quest.Items.Sum(x => x.StackQuantity) != 1) throw new InvalidOperationException("quest consume did not remove exact quantity");
        });

        Check("quest-item-lifecycle-cleanup-target-and-rollback", () =>
        {
            var quest = new RuntimeItemEngine(catalog);
            quest.GrantQuestItem("smoke:quest:cleanup", owner, gettable.Id, "smoke.quest.cleanup.grant", 3);
            var before = quest.Items.OrderBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
            var cleanup = quest.ReconcileQuestItemCleanup("smoke:quest:cleanup", owner, gettable.Id, "smoke.quest.cleanup.abandon", "abandon", 1);
            if (cleanup.ExistingQuantity != 3 || cleanup.RemovedQuantity != 2 || cleanup.FinalQuantity != 1) throw new InvalidOperationException("quest cleanup did not reconcile exact target quantity");
            var stable = quest.ReconcileQuestItemCleanup("smoke:quest:cleanup", owner, gettable.Id, "smoke.quest.cleanup.repeat", "reset", 1);
            if (stable.RemovedQuantity != 0 || stable.FinalQuantity != 1) throw new InvalidOperationException("idempotent quest cleanup changed target state");
            var protectedQuest = new RuntimeItemEngine(catalog);
            protectedQuest.GrantQuestItem("smoke:quest:protected", owner, gettable.Id, "smoke.quest.protected.grant", 1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["quest_cleanup_protected"] = "true" });
            var protectedId = protectedQuest.Items.Single().InstanceId;
            try { protectedQuest.ReconcileQuestItemCleanup("smoke:quest:protected", owner, gettable.Id, "smoke.quest.protected.cleanup", "failure", 0); throw new InvalidOperationException("protected quest cleanup was accepted"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("protected", StringComparison.OrdinalIgnoreCase)) { }
            if (protectedQuest.Items.Single().InstanceId != protectedId) throw new InvalidOperationException("failed quest cleanup changed protected item state");
        });

        Check("duplicate-prevention-and-rollback", () =>
        {
            var existing = engine.Items.First();
            var count = engine.Items.Count;
            try { engine.Add(existing); throw new InvalidOperationException("duplicate accepted"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)) { }
            if (engine.Items.Count != count) throw new InvalidOperationException("rollback failed");
        });


        Check("event-item-reward-cleanup-target-and-rollback", () =>
        {
            var eventEngine = new RuntimeItemEngine(catalog);
            const string eventId = "smoke:event";
            const string rewardKey = "reward:primary";
            var granted = eventEngine.GrantEventItem(eventId, owner, gettable.Id, rewardKey, 2);
            if (granted.FinalQuantity != 2 || granted.CreatedQuantity != 2) throw new InvalidOperationException("event reward did not reconcile exact target quantity");
            var repeated = eventEngine.GrantEventItem(eventId, owner, gettable.Id, rewardKey, 2);
            if (repeated.CreatedQuantity != 0 || repeated.FinalQuantity != 2) throw new InvalidOperationException("event reward was not idempotent");
            var cleanup = eventEngine.ReconcileEventItemCleanup(eventId, owner, gettable.Id, "cleanup:expiration", "expiration", 1);
            if (cleanup.RemovedQuantity != 1 || cleanup.FinalQuantity != 1) throw new InvalidOperationException("event cleanup did not reconcile exact target quantity");
            var protectedItem = eventEngine.GrantEventItem(eventId, other, gettable.Id, "reward:protected", 1, new Dictionary<string, string> { ["event_cleanup_protected"] = "true" });
            try { eventEngine.ReconcileEventItemCleanup(eventId, other, gettable.Id, "cleanup:cancel", "cancellation", 0); throw new InvalidOperationException("protected event cleanup was allowed"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("protected", StringComparison.OrdinalIgnoreCase)) { }
            if (eventEngine.Find(protectedItem.CreatedInstanceIds.Single()) is null) throw new InvalidOperationException("failed event cleanup changed protected item state");
        });

        Check("save-load-world-state", () =>
        {
            var repository = new RuntimeItemWorldRepository(Path.Combine(Path.GetTempPath(), "ReforgedItemSmoke", Guid.NewGuid().ToString("N")), worldId);
            var state = engine.Snapshot();
            repository.Save(state);
            var loaded = repository.Load();
            var restored = new RuntimeItemEngine(catalog, loaded.Items, loaded.Corpses);
            if (restored.Items.Count != engine.Items.Count) throw new InvalidOperationException("item persistence mismatch");
        });

        Check("validated-world-item-persistence-and-corruption-rejection", () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "ReforgedItemPersistenceSmoke", Guid.NewGuid().ToString("N"));
            var repository = new RuntimeItemWorldRepository(folder, worldId);
            var state = engine.Snapshot();
            repository.SaveValidated(catalog, state);
            var loaded = repository.LoadValidated(catalog);
            if (loaded.Items.Count != state.Items.Count) throw new InvalidOperationException("validated item persistence changed item count");
            var invalid = new RuntimeItemState(new[] { state.Items.First() with { StackQuantity = 0 } }, 0, new Dictionary<string, string>(), Array.Empty<RuntimeCorpseState>());
            try { repository.SaveValidated(catalog, invalid); throw new InvalidOperationException("invalid item state was persisted"); }
            catch (InvalidDataException ex) when (ex.Message.Contains("Refusing", StringComparison.OrdinalIgnoreCase)) { }
            var restored = repository.LoadValidated(catalog);
            if (restored.Items.Count != state.Items.Count) throw new InvalidOperationException("failed validated save replaced authoritative item state");
        });

        Check("validated-world-item-backup-recovery", () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "ReforgedItemRecoverySmoke", Guid.NewGuid().ToString("N"));
            var repository = new RuntimeItemWorldRepository(folder, worldId);
            var baseline = engine.Snapshot() with { ActiveItemState = new Dictionary<string, string> { ["recovery_generation"] = "baseline" } };
            var current = engine.Snapshot() with { ActiveItemState = new Dictionary<string, string> { ["recovery_generation"] = "current" } };
            repository.SaveValidated(catalog, baseline);
            repository.SaveValidatedWithBackup(catalog, current);
            var savePath = Directory.GetFiles(Path.Combine(RuntimeSavePaths.RealmRoot(folder, "classic"), "worlds"), "*.items.json").Single();
            File.WriteAllText(savePath, "{corrupt-primary");
            var recovered = repository.LoadValidatedWithRecovery(catalog);
            if (!recovered.RecoveredPrimary || recovered.Source != "backup") throw new InvalidOperationException("validated backup was not selected after primary corruption");
            if (!recovered.State.ActiveItemState.TryGetValue("recovery_generation", out var generation) || generation != "baseline") throw new InvalidOperationException("backup recovery returned the wrong generation");
            var restoredPrimary = repository.LoadValidated(catalog);
            if (!restoredPrimary.ActiveItemState.TryGetValue("recovery_generation", out generation) || generation != "baseline") throw new InvalidOperationException("backup recovery did not atomically restore the primary save");
        });

        Check("validated-world-item-integrity-sidecar", () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "ReforgedItemIntegritySmoke", Guid.NewGuid().ToString("N"));
            var repository = new RuntimeItemWorldRepository(folder, worldId);
            var state = engine.Snapshot();
            repository.SaveValidatedWithIntegrity(catalog, state);
            var loaded = repository.LoadValidatedWithIntegrity(catalog);
            if (loaded.Items.Count != state.Items.Count) throw new InvalidOperationException("integrity-validated load changed item count");
            var savePath = Directory.GetFiles(Path.Combine(RuntimeSavePaths.RealmRoot(folder, "classic"), "worlds"), "*.items.json").Single();
            File.AppendAllText(savePath, " ");
            try { repository.LoadValidatedWithIntegrity(catalog); throw new InvalidOperationException("tampered item state passed integrity verification"); }
            catch (InvalidDataException ex) when (ex.Message.Contains("integrity", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("validated-world-item-integrity-backup-recovery", () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "ReforgedItemIntegrityRecoverySmoke", Guid.NewGuid().ToString("N"));
            var repository = new RuntimeItemWorldRepository(folder, worldId);
            var baseline = engine.Snapshot() with { ActiveItemState = new Dictionary<string, string> { ["integrity_generation"] = "baseline" } };
            var current = engine.Snapshot() with { ActiveItemState = new Dictionary<string, string> { ["integrity_generation"] = "current" } };
            repository.SaveValidatedWithIntegrity(catalog, baseline);
            repository.SaveValidatedWithIntegrityBackup(catalog, current);
            var savePath = Directory.GetFiles(Path.Combine(RuntimeSavePaths.RealmRoot(folder, "classic"), "worlds"), "*.items.json").Single(x => !x.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
            File.AppendAllText(savePath, "tamper");
            var recovered = repository.LoadValidatedWithIntegrityRecovery(catalog);
            if (!recovered.RecoveredPrimary || recovered.Source != "integrity-backup") throw new InvalidOperationException("integrity backup was not selected after primary tampering");
            if (!recovered.State.ActiveItemState.TryGetValue("integrity_generation", out var generation) || generation != "baseline") throw new InvalidOperationException("integrity backup recovery returned the wrong generation");
            var restored = repository.LoadValidatedWithIntegrity(catalog);
            if (!restored.ActiveItemState.TryGetValue("integrity_generation", out generation) || generation != "baseline") throw new InvalidOperationException("integrity backup recovery did not restore primary data and sidecar");
            File.AppendAllText(savePath + ".bak", "tamper");
            File.AppendAllText(savePath, "tamper-again");
            try { repository.LoadValidatedWithIntegrityRecovery(catalog); throw new InvalidOperationException("tampered integrity backup was accepted"); }
            catch (InvalidDataException ex) when (ex.Message.Contains("backup", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("exact-duplicate-item-and-corpse-reconciliation", () =>
        {
            var duplicateId = "smoke:duplicate:item";
            var duplicateItem = new RuntimeItemInstance(duplicateId, gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null);
            var duplicateCorpse = new RuntimeCorpseState("smoke:duplicate:corpse", owner, room, 0, DateTimeOffset.UnixEpoch, null);
            var duplicateState = new RuntimeItemState(new[] { duplicateItem, duplicateItem }, 0, new Dictionary<string, string>(), new[] { duplicateCorpse, duplicateCorpse });
            var reconciled = RuntimeItemStateReconciler.ReconcileExactDuplicates(catalog, duplicateState);
            if (reconciled.RemovedDuplicateItems != 1 || reconciled.RemovedDuplicateCorpses != 1 || reconciled.State.Items.Count != 1 || reconciled.State.Corpses?.Count != 1) throw new InvalidOperationException("exact duplicate reconciliation did not collapse duplicate records");
            var conflicting = duplicateItem with { StackQuantity = 2 };
            try { RuntimeItemStateReconciler.ReconcileExactDuplicates(catalog, duplicateState with { Items = new[] { duplicateItem, conflicting } }); throw new InvalidOperationException("conflicting duplicate item instance was silently reconciled"); }
            catch (InvalidDataException ex) when (ex.Message.Contains("conflicting duplicate item", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("missing-container-parent-reconciliation", () =>
        {
            var orphan = new RuntimeItemInstance("smoke:orphan:owned", gettable.Id, owner, RuntimeItemLocations.Container, "missing:container", null, 0, 1, null);
            var state = new RuntimeItemState(new[] { orphan }, 0, new Dictionary<string, string>());
            var invalid = RuntimeItemValidator.Validate(catalog, state.Items, state.Corpses);
            if (!invalid.Issues.Any(x => x.Code == "MISSING_CONTAINER_PARENT")) throw new InvalidOperationException("missing parent was not reported");
            var repaired = RuntimeItemStateReconciler.ReconcileMissingContainerParents(catalog, state);
            var recovered = repaired.State.Items.Single();
            if (repaired.RecoveredContainerItems != 1 || recovered.LocationKind != RuntimeItemLocations.Character || recovered.LocationId != owner || recovered.OwnerId != owner) throw new InvalidOperationException("owned orphan was not recovered to owner inventory");
            if (recovered.Metadata?.GetValueOrDefault("recovered_from_missing_container_id") != "missing:container") throw new InvalidOperationException("orphan recovery provenance was not persisted");
            var ownerless = orphan with { InstanceId = "smoke:orphan:ownerless", OwnerId = null };
            try { RuntimeItemStateReconciler.ReconcileMissingContainerParents(catalog, state with { Items = new[] { ownerless } }); throw new InvalidOperationException("ownerless orphan was silently recovered"); }
            catch (InvalidDataException ex) when (ex.Message.Contains("no authoritative owner", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("missing-corpse-parent-validation", () =>
        {
            var orphan = new RuntimeItemInstance("smoke:orphan:corpse", gettable.Id, null, RuntimeItemLocations.Corpse, "missing:corpse", null, 0, 1, null);
            var report = RuntimeItemValidator.Validate(catalog, new[] { orphan }, Array.Empty<RuntimeCorpseState>());
            if (!report.Issues.Any(x => x.Code == "MISSING_CORPSE_PARENT")) throw new InvalidOperationException("missing corpse parent was not reported");
        });

        Check("owner-location-state-reconciliation", () =>
        {
            var mismatched = new RuntimeItemInstance("smoke:owner-location", gettable.Id, owner, RuntimeItemLocations.Npc, "stale:owner", null, 0, 1, null);
            var roomOwned = new RuntimeItemInstance("smoke:room-owner", gettable.Id, owner, RuntimeItemLocations.Room, room, null, 0, 1, null);
            var missingSlot = new RuntimeItemInstance("smoke:equipment-missing-slot", equipment!.CanonicalItemId, owner, RuntimeItemLocations.Equipment, owner, null, 0, 1, null);
            var staleSlot = new RuntimeItemInstance("smoke:stale-slot", gettable.Id, owner, RuntimeItemLocations.Character, owner, "HEAD", 0, 1, null);
            var state = new RuntimeItemState(new[] { mismatched, roomOwned, missingSlot, staleSlot }, 0, new Dictionary<string, string>());
            var invalid = RuntimeItemValidator.Validate(catalog, state.Items, state.Corpses);
            foreach (var code in new[] { "OWNER_LOCATION_ID_MISMATCH", "NONOWNED_LOCATION_HAS_OWNER", "EQUIPMENT_MISSING_SLOT", "NON_EQUIPMENT_HAS_SLOT" })
                if (!invalid.Issues.Any(x => x.Code == code)) throw new InvalidOperationException("missing owner/location validation: " + code);
            var repaired = RuntimeItemStateReconciler.ReconcileOwnerLocationState(catalog, state);
            if (repaired.RecoveredItems != 4) throw new InvalidOperationException("owner/location reconciliation count mismatch");
            var byId = repaired.State.Items.ToDictionary(x => x.InstanceId);
            if (byId[mismatched.InstanceId].LocationId != owner) throw new InvalidOperationException("owned location identity was not normalized");
            if (byId[roomOwned.InstanceId].OwnerId is not null) throw new InvalidOperationException("room-held stale owner was not cleared");
            if (byId[missingSlot.InstanceId].LocationKind != RuntimeItemLocations.Character || byId[missingSlot.InstanceId].EquippedSlot is not null) throw new InvalidOperationException("slotless equipment was not recovered carried");
            if (byId[staleSlot.InstanceId].EquippedSlot is not null) throw new InvalidOperationException("non-equipment stale slot was not cleared");
            if (!RuntimeItemValidator.Validate(catalog, repaired.State.Items, repaired.State.Corpses).Passed) throw new InvalidOperationException("reconciled owner/location state remained invalid");
        });


        Check("container-owner-mismatch-reconciliation", () =>
        {
            var parent = new RuntimeItemInstance("smoke:container:owner-parent", containerDef!.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null);
            var child = new RuntimeItemInstance("smoke:container:owner-child", gettable.Id, "stale:owner", RuntimeItemLocations.Container, parent.InstanceId, null, 0, 1, null);
            var state = new RuntimeItemState(new[] { parent, child }, 0, new Dictionary<string, string>());
            var invalid = RuntimeItemValidator.Validate(catalog, state.Items, state.Corpses);
            if (!invalid.Issues.Any(x => x.Code == "CONTAINER_OWNER_MISMATCH")) throw new InvalidOperationException("container owner mismatch was not reported");
            var repaired = RuntimeItemStateReconciler.ReconcileContainerOwnerMismatches(catalog, state);
            var recovered = repaired.State.Items.Single(x => x.InstanceId == child.InstanceId);
            if (repaired.RecoveredItems != 1 || recovered.OwnerId != owner) throw new InvalidOperationException("child did not inherit authoritative parent owner");
            if (recovered.Metadata?.GetValueOrDefault("container_owner_source_instance_id") != parent.InstanceId) throw new InvalidOperationException("container owner recovery provenance was not persisted");
            var ownerlessParent = parent with { InstanceId = "smoke:container:ownerless-parent", OwnerId = null, LocationKind = RuntimeItemLocations.Room, LocationId = room };
            var claimedChild = child with { InstanceId = "smoke:container:claimed-child", LocationId = ownerlessParent.InstanceId };
            try { RuntimeItemStateReconciler.ReconcileContainerOwnerMismatches(catalog, new RuntimeItemState(new[] { ownerlessParent, claimedChild }, 0, new Dictionary<string, string>())); throw new InvalidOperationException("ownerless parent mismatch was silently reconciled"); }
            catch (InvalidDataException ex) when (ex.Message.Contains("cannot be inferred safely", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("cross-save-ownership-coordination", () =>
        {
            var characterItem = new RuntimeItemInstance("smoke:cross-save:character", gettable.Id, owner, RuntimeItemLocations.Character, owner, null, 0, 1, null, new Dictionary<string, string> { ["quest_id"] = "quest:known", ["event_id"] = "event:known" });
            var npcItem = new RuntimeItemInstance("smoke:cross-save:npc", gettable.Id, "npc:known", RuntimeItemLocations.Npc, "npc:known", null, 0, 1, null);
            var merchantItem = new RuntimeItemInstance("smoke:cross-save:merchant", gettable.Id, "merchant:known", RuntimeItemLocations.Merchant, "merchant:known", null, 0, 1, null);
            var state = new RuntimeItemState(new[] { characterItem, npcItem, merchantItem }, 0, new Dictionary<string, string>());
            var ownership = new RuntimeItemOwnershipSnapshot(
                new HashSet<string>(new[] { owner }, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(new[] { "npc:known" }, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(new[] { "merchant:known" }, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(new[] { "quest:known" }, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(new[] { "event:known" }, StringComparer.OrdinalIgnoreCase));
            var valid = RuntimeItemCrossSaveOwnershipCoordinator.Validate(state, ownership);
            if (!valid.Passed) throw new InvalidOperationException("valid cross-save ownership snapshot was rejected");
            var missing = ownership with { CharacterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
            var invalid = RuntimeItemCrossSaveOwnershipCoordinator.Validate(state, missing);
            if (!invalid.Issues.Any(x => x.Code == "MISSING_CHARACTER_OWNER")) throw new InvalidOperationException("missing Character owner was not detected");
            try { RuntimeItemCrossSaveOwnershipCoordinator.EnsureValid(state, missing); throw new InvalidOperationException("invalid cross-save ownership was accepted"); }
            catch (InvalidDataException ex) when (ex.Message.Contains("Cross-save", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("coordinated-save-generation-recovery", () =>
        {
            var baseState = new RuntimeItemState(Array.Empty<RuntimeItemInstance>(), 0, new Dictionary<string, string>());
            var generations = new RuntimeItemSaveGenerationSnapshot(
                "generation:0201",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["character"] = "generation:0201",
                    ["economy"] = "generation:0201",
                    ["quests"] = "generation:0201",
                    ["events"] = "generation:0201",
                    ["npc"] = "generation:0201",
                    ["world"] = "generation:0201"
                });
            var stamped = RuntimeItemSaveGenerationCoordinator.Stamp(baseState, generations);
            var valid = RuntimeItemSaveGenerationCoordinator.Validate(stamped, generations);
            if (!valid.Passed) throw new InvalidOperationException("matching coordinated save generations were rejected");
            var mismatched = generations with { SubsystemGenerations = new Dictionary<string, string>(generations.SubsystemGenerations, StringComparer.OrdinalIgnoreCase) { ["economy"] = "generation:stale" } };
            var invalid = RuntimeItemSaveGenerationCoordinator.Validate(stamped, mismatched);
            if (!invalid.Issues.Any(x => x.Code == "SUBSYSTEM_GENERATION_MISMATCH" && x.Subsystem == "economy")) throw new InvalidOperationException("stale subsystem generation was not detected");
            var staleItems = stamped with { ActiveItemState = new Dictionary<string, string> { ["coordinated_save_generation"] = "generation:stale" } };
            if (!RuntimeItemSaveGenerationCoordinator.Validate(staleItems, generations).Issues.Any(x => x.Code == "ITEM_GENERATION_MISMATCH")) throw new InvalidOperationException("stale item generation was not detected");
        });

        Check("global-limited-item-reconciliation", () =>
        {
            var ownerA = "character:limited:a";
            var ownerB = "character:limited:b";
            var ownerMeta = new Dictionary<string, string> { ["maximum_owned_quantity"] = "1" };
            var worldMeta = new Dictionary<string, string> { ["maximum_world_quantity"] = "2" };
            var validState = new RuntimeItemState(new[]
            {
                new RuntimeItemInstance("smoke:limit:a", gettable.Id, ownerA, RuntimeItemLocations.Character, ownerA, null, 0, 1, null, ownerMeta),
                new RuntimeItemInstance("smoke:limit:b", gettable.Id, ownerB, RuntimeItemLocations.Bank, ownerB, null, 0, 1, null, ownerMeta),
                new RuntimeItemInstance("smoke:world:1", gettable.Id, null, RuntimeItemLocations.Room, "room:one", null, 0, 1, null, worldMeta),
                new RuntimeItemInstance("smoke:world:2", gettable.Id, null, RuntimeItemLocations.Room, "room:two", null, 0, 1, null, worldMeta)
            }, 0, new Dictionary<string, string>());
            var valid = RuntimeItemLimitedReconciliationCoordinator.Validate(validState);
            if (!valid.Passed) throw new InvalidOperationException("valid global limited-item reservations were rejected");
            var ownerOverflow = validState with { Items = validState.Items.Append(new RuntimeItemInstance("smoke:limit:a:duplicate", gettable.Id, ownerA, RuntimeItemLocations.Bank, ownerA, null, 0, 1, null, ownerMeta)).ToArray() };
            if (!RuntimeItemLimitedReconciliationCoordinator.Validate(ownerOverflow).Issues.Any(x => x.Code == "OWNER_LIMIT_EXCEEDED")) throw new InvalidOperationException("owner limited-item overflow was not detected");
            var worldOverflow = validState with { Items = validState.Items.Append(new RuntimeItemInstance("smoke:world:3", gettable.Id, null, RuntimeItemLocations.Room, "room:three", null, 0, 1, null, worldMeta)).ToArray() };
            if (!RuntimeItemLimitedReconciliationCoordinator.Validate(worldOverflow).Issues.Any(x => x.Code == "WORLD_LIMIT_EXCEEDED")) throw new InvalidOperationException("world limited-item overflow was not detected");
            try { RuntimeItemLimitedReconciliationCoordinator.EnsureValid(ownerOverflow); throw new InvalidOperationException("invalid limited-item state was accepted"); }
            catch (InvalidDataException ex) when (ex.Message.Contains("limited-item", StringComparison.OrdinalIgnoreCase)) { }
        });

        Check("administrative-item-runtime", () =>
        {
            var adminOwner = "character:admin:owner";
            var item = engine.Create(gettable.Id, RuntimeItemLocations.Character, adminOwner, ownerId: adminOwner);
            var inspection = engine.AdministrativeInspect(item.InstanceId);
            if (!inspection.Item.InstanceId.Equals(item.InstanceId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("administrative inspection returned the wrong item");
            var moved = engine.AdministrativeMove(item.InstanceId, RuntimeItemLocations.Room, "room:admin");
            if (!moved.CurrentLocationKind.Equals(RuntimeItemLocations.Room, StringComparison.OrdinalIgnoreCase) || moved.CurrentOwnerId is not null) throw new InvalidOperationException("administrative movement did not normalize room ownership");
            var audit = engine.AdministrativeAudit();
            if (!audit.RuntimeValidation.Passed || !audit.LimitedValidation.Passed) throw new InvalidOperationException("administrative catalog/runtime audit failed");
            var removed = engine.AdministrativeRemove(item.InstanceId);
            if (!removed.AdministrativeOverride || engine.Find(item.InstanceId) is not null) throw new InvalidOperationException("administrative removal did not remove the item");
        });

        Check("disconnect-reconnect-snapshot", () =>
        {
            var state = engine.Snapshot();
            var restored = new RuntimeItemEngine(catalog, state.Items, state.Corpses);
            if (!restored.Validate().Passed) throw new InvalidOperationException("reconnect validation failed");
        });
        Check("runtime-validator", () => { var report = engine.Validate(); if (!report.Passed) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(x => x.Message))); });
        return new RuntimeItemSmokeResult(failures.Count == 0, checks, failures);
    }

    private static RuntimeCharacterRecord SmokeCharacter(string id, string worldId) => new(
        id, "smoke", "Smoke", worldId, "smoke-race", "Smoke Race", "smoke-class", "Smoke Class", 100, 0, 100, 100, 100, 100,
        "smoke:room", 1, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["strength"] = 100, ["str"] = 100, ["STR"] = 100 },
        new Dictionary<string, int>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string?>(),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["alignment"] = "neutral", ["gender"] = "neutral" }, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
