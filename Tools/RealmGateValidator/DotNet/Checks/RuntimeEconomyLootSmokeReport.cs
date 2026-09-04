namespace RealmGate.Server;

public static class RuntimeEconomyLootSmokeReport
{
    public static IReadOnlyDictionary<string, bool> Run()
    {
        var items = new[] {
            new RuntimeEconomyItemDefinition("item.common", 10, true, 99, 1, true, false, false),
            new RuntimeEconomyItemDefinition("item.rare", 100, true, 99, 1, true, false, false)
        };
        var currency = new RuntimeCurrencyDefinition("currency.major", "coin.copper", new[] { new RuntimeCurrencyDenomination("coin.copper", 1, 0) }, 999999, false, 0m);
        var math = new RuntimeCurrencyMath(currency);
        var table = new RuntimeLootTableDefinition("loot.test", currency.Id, 5,
            new[] { new RuntimeLootItemGrant("item.common", 1) },
            new[] {
                new RuntimeLootWeightedEntry("entry.common", 9, 1, new[] { new RuntimeLootItemGrant("item.common", 2) }),
                new RuntimeLootWeightedEntry("entry.rare", 1, 10, new[] { new RuntimeLootItemGrant("item.rare", 1) })
            }, 1, true);
        var first = RuntimeLootResolver.Resolve(table, 1234); var second = RuntimeLootResolver.Resolve(table, 1234);
        var corpse = new RuntimeLootCorpse("corpse.1", "npc.1", currency.Id, new[] { "character.1" }, false, first, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1));
        var wallet = new RuntimeCurrencyWallet("wallet.1", math);
        var inventory = new RuntimeEconomyInventory("inventory.1", 999, items);
        var denied = false; try { RuntimeLootClaimEngine.ClaimAll("claim.denied", "character.2", corpse, wallet, inventory, DateTimeOffset.UnixEpoch); } catch (UnauthorizedAccessException) { denied = true; }
        var result = RuntimeLootClaimEngine.ClaimAll("claim.1", "character.1", corpse, wallet, inventory, DateTimeOffset.UnixEpoch);
        var replay = RuntimeLootClaimEngine.ClaimAll("claim.1", "character.1", corpse, wallet, inventory, DateTimeOffset.UnixEpoch);
        var persisted = RuntimeLootPersistence.Deserialize(RuntimeLootPersistence.Serialize(new(RuntimeLootPersistence.CurrentSchemaVersion, new[] { corpse.Capture() })));
        return new Dictionary<string, bool> {
            ["deterministic-weighted-resolution"] = first.CurrencyBaseUnits == second.CurrencyBaseUnits && first.SelectedEntryIds.SequenceEqual(second.SelectedEntryIds) && first.ItemQuantities.OrderBy(x => x.Key).SequenceEqual(second.ItemQuantities.OrderBy(x => x.Key)),
            ["corpse-rights-enforced"] = denied,
            ["atomic-claim-all"] = result.CurrencyBaseUnits == first.CurrencyBaseUnits && corpse.IsEmpty(),
            ["idempotent-claim-replay"] = replay.Replayed,
            ["persistence-roundtrip"] = persisted.Corpses.Count == 1
        };
    }
}
