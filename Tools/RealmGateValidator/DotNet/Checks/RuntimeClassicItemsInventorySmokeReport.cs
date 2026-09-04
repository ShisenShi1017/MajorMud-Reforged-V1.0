using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicItemsInventorySmokeReport(bool Passed, IReadOnlyList<string> Checks)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeClassicItemsInventorySmokeReport Run(string projectRoot)
    {
        var catalog = RuntimeItemCatalog.Load(projectRoot, "Classic");
        var checks = new List<string>();
        void Check(string name, bool value) => checks.Add(name + ":" + value);

        Check("canonical-item-count-1950", catalog.Items.Count == 1950);

        var firegut = catalog.Require("item.1728");
        var firegutCasts = firegut.OrderedAbilities.Where(x => x.AbilityId == 43).OrderBy(x => x.Slot).ToArray();
        Check("firegut-repeated-cast-slots-preserved", firegutCasts.Length >= 2);
        Check("firegut-first-cast-119", firegutCasts.Length >= 1 && firegutCasts[0].Value == 119);
        Check("firegut-second-cast-317", firegutCasts.Length >= 2 && firegutCasts[1].Value == 317);

        var vial = catalog.Require("item.1608");
        Check("small-vial-is-drink", vial.ItemType.Equals("drink", StringComparison.OrdinalIgnoreCase));
        Check("small-vial-no-cast-effect", !vial.OrderedAbilities.Any(x => x.AbilityId == 43 && x.Value > 0));

        const string owner = "items-0363-smoke";
        RuntimeItemEngine Engine(string itemId, int uses) => new(
            catalog,
            new[] { new RuntimeItemInstance("smoke", itemId, owner, RuntimeItemLocations.Character, owner, null, uses, 1, null) },
            Array.Empty<RuntimeCorpseState>());

        var unlimited = Engine("item.1408", -1);
        var unlimitedResult = unlimited.SpendClassicUse("smoke", owner);
        Check("uses-minus-one-unlimited", unlimitedResult.PreviousUses == -1 && unlimitedResult.RemainingUses == -1 && !unlimitedResult.Destroyed && unlimited.Find("smoke")?.Charges == -1);

        var finite = Engine("item.957", 2);
        var finiteResult = finite.SpendClassicUse("smoke", owner);
        Check("finite-use-decrements-one", finiteResult.PreviousUses == 2 && finiteResult.RemainingUses == 1 && finite.Find("smoke")?.Charges == 1);

        var destroy = Engine("item.957", 1);
        var destroyResult = destroy.SpendClassicUse("smoke", owner);
        Check("zero-nonretain-removes-copy", destroyResult.Destroyed && destroy.Find("smoke") is null);

        var retained = Engine("item.407", 1);
        var retainedZero = retained.SpendClassicUse("smoke", owner);
        Check("zero-retain-keeps-copy", !retainedZero.Destroyed && retained.Find("smoke")?.Charges == 0);
        var retainedUnlimited = retained.SpendClassicUse("smoke", owner);
        Check("retained-zero-next-use-becomes-minus-one", !retainedUnlimited.Destroyed && retained.Find("smoke")?.Charges == -1);

        var passed = checks.All(check => check.EndsWith(":True", StringComparison.OrdinalIgnoreCase));
        return new RuntimeClassicItemsInventorySmokeReport(passed, checks);
    }
}
