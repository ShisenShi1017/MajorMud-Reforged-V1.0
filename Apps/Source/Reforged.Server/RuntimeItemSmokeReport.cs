using System.Text.Json;

namespace Reforged.Server;

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
        var gettable = catalog.Items.First(x => x.Gettable && !x.Container);
        var containerDef = catalog.Items.FirstOrDefault(x => x.Container && x.Gettable);
        var equipDef = catalog.EquipmentDefinitions.First(x => x.Slot != "NONE" && !x.ClassRestricted && !x.RaceRestricted && x.MinimumLevel == 0 && x.MinimumStrength == 0 && string.IsNullOrWhiteSpace(x.Alignment) && string.IsNullOrWhiteSpace(x.Gender));
        var character = SmokeCharacter(owner, worldId);

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
            var savePath = Directory.GetFiles(Path.Combine(folder, "Saves", "Runtime0137", "worlds"), "*.items.json").Single();
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
            var savePath = Directory.GetFiles(Path.Combine(folder, "Saves", "Runtime0137", "worlds"), "*.items.json").Single();
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
            var savePath = Directory.GetFiles(Path.Combine(folder, "Saves", "Runtime0137", "worlds"), "*.items.json").Single(x => !x.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
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
