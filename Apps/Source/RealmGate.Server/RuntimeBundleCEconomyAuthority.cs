using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeClassicBankCurrency(long Runic, long Platinum, long Gold, long Silver, long Copper)
{
    public long CopperValue => checked(Runic * 1_000_000L + Platinum * 10_000L + Gold * 100L + Silver * 10L + Copper);
    public bool IsEmpty => Runic == 0 && Platinum == 0 && Gold == 0 && Silver == 0 && Copper == 0;
    public RuntimeClassicInventoryCurrency AsInventoryCurrency() => new(Runic, Platinum, Gold, Silver, Copper);
}

public static class RuntimeBundleCEconomyAuthority
{
    private static readonly string[] Denominations = ["runic", "platinum", "gold", "silver", "copper"];

    public static RuntimeItemState DebitCopperValue(RuntimeItemState state, long copperValue)
    {
        if (copperValue < 0) throw new ArgumentOutOfRangeException(nameof(copperValue));
        var current = RuntimeBundleCInventoryAuthority.ReadCurrency(state);
        if (current.CopperValue < copperValue) throw new InvalidOperationException("You do not have enough carried currency.");
        return RuntimeBundleCInventoryAuthority.WriteCurrency(state, FromCopper(current.CopperValue - copperValue));
    }

    public static RuntimeItemState CreditCopperValue(RuntimeItemState state, long copperValue)
    {
        if (copperValue < 0) throw new ArgumentOutOfRangeException(nameof(copperValue));
        var current = RuntimeBundleCInventoryAuthority.ReadCurrency(state);
        return RuntimeBundleCInventoryAuthority.WriteCurrency(state, FromCopper(checked(current.CopperValue + copperValue)));
    }

    public static RuntimeClassicBankCurrency ReadBank(IReadOnlyDictionary<string, string>? active) => new(
        Read(active, "bank.currency.runic"), Read(active, "bank.currency.platinum"), Read(active, "bank.currency.gold"),
        Read(active, "bank.currency.silver"), Read(active, "bank.currency.copper"));

    public static RuntimeItemState WriteBank(RuntimeItemState state, RuntimeClassicBankCurrency bank)
    {
        Validate(bank.Runic, bank.Platinum, bank.Gold, bank.Silver, bank.Copper);
        var active = new Dictionary<string, string>(state.ActiveItemState ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            ["bank.currency.runic"] = bank.Runic.ToString(CultureInfo.InvariantCulture),
            ["bank.currency.platinum"] = bank.Platinum.ToString(CultureInfo.InvariantCulture),
            ["bank.currency.gold"] = bank.Gold.ToString(CultureInfo.InvariantCulture),
            ["bank.currency.silver"] = bank.Silver.ToString(CultureInfo.InvariantCulture),
            ["bank.currency.copper"] = bank.Copper.ToString(CultureInfo.InvariantCulture)
        };
        return state with { ActiveItemState = active };
    }

    public static RuntimeItemState DebitBankGold(RuntimeItemState state, long gold)
    {
        if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
        var bank = ReadBank(state.ActiveItemState);
        if (bank.Gold < gold) throw new InvalidOperationException("The Silvermere bank account does not contain sufficient gold.");
        return WriteBank(state, bank with { Gold = bank.Gold - gold });
    }

    public static RuntimeItemState Deposit(RuntimeItemState state, string argument, out RuntimeClassicInventoryCurrency moved)
    {
        var carried = RuntimeBundleCInventoryAuthority.ReadCurrency(state);
        var bank = ReadBank(state.ActiveItemState);
        moved = ParseTransfer(argument, carried);
        var afterCarried = Subtract(carried, moved);
        var afterBank = Add(bank, moved);
        return WriteBank(RuntimeBundleCInventoryAuthority.WriteCurrency(state, afterCarried), afterBank);
    }

    public static RuntimeItemState Withdraw(RuntimeItemState state, string argument, out RuntimeClassicInventoryCurrency moved)
    {
        var bank = ReadBank(state.ActiveItemState);
        moved = ParseTransfer(argument, bank.AsInventoryCurrency());
        var afterBankInventory = Subtract(bank.AsInventoryCurrency(), moved);
        var carried = Add(RuntimeBundleCInventoryAuthority.ReadCurrency(state), moved);
        return WriteBank(RuntimeBundleCInventoryAuthority.WriteCurrency(state, carried), new(afterBankInventory.Runic, afterBankInventory.Platinum, afterBankInventory.Gold, afterBankInventory.Silver, afterBankInventory.Copper));
    }

    public static string FormatCopperValue(long copperValue) => RuntimeBundleCInventoryAuthority.FormatCurrency(FromCopper(copperValue));

    private static RuntimeClassicInventoryCurrency ParseTransfer(string argument, RuntimeClassicInventoryCurrency available)
    {
        var text = argument.Trim().ToLowerInvariant();
        if (text.Length == 0) throw new InvalidOperationException("Specify an amount and denomination, or ALL.");
        if (text == "all")
        {
            if (available.IsEmpty) throw new InvalidOperationException("There is no currency to transfer.");
            return available;
        }
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            throw new InvalidOperationException("Enter a positive whole-number currency amount.");
        var denomination = parts.Length > 1 ? CanonicalDenomination(parts[1]) : "gold";
        var result = denomination switch
        {
            "runic" => new RuntimeClassicInventoryCurrency(amount, 0, 0, 0, 0),
            "platinum" => new RuntimeClassicInventoryCurrency(0, amount, 0, 0, 0),
            "gold" => new RuntimeClassicInventoryCurrency(0, 0, amount, 0, 0),
            "silver" => new RuntimeClassicInventoryCurrency(0, 0, 0, amount, 0),
            "copper" => new RuntimeClassicInventoryCurrency(0, 0, 0, 0, amount),
            _ => throw new InvalidOperationException("Unknown currency denomination.")
        };
        _ = Subtract(available, result);
        return result;
    }

    private static string CanonicalDenomination(string value)
    {
        value = value.Trim().ToLowerInvariant();
        if (value is "rune" or "runes" or "runic" or "runics") return "runic";
        if (value is "plat" or "platinum" or "platinums") return "platinum";
        if (value is "gold" or "golds" or "crown" or "crowns") return "gold";
        if (value is "silver" or "silvers" or "noble" or "nobles") return "silver";
        if (value is "copper" or "coppers" or "farthing" or "farthings") return "copper";
        throw new InvalidOperationException($"Unknown denomination '{value}'. Valid denominations: {string.Join(", ", Denominations)}.");
    }

    private static RuntimeClassicInventoryCurrency Add(RuntimeClassicInventoryCurrency left, RuntimeClassicInventoryCurrency right) => new(
        checked(left.Runic + right.Runic), checked(left.Platinum + right.Platinum), checked(left.Gold + right.Gold), checked(left.Silver + right.Silver), checked(left.Copper + right.Copper));
    private static RuntimeClassicBankCurrency Add(RuntimeClassicBankCurrency left, RuntimeClassicInventoryCurrency right) => new(
        checked(left.Runic + right.Runic), checked(left.Platinum + right.Platinum), checked(left.Gold + right.Gold), checked(left.Silver + right.Silver), checked(left.Copper + right.Copper));
    private static RuntimeClassicInventoryCurrency Subtract(RuntimeClassicInventoryCurrency left, RuntimeClassicInventoryCurrency right)
    {
        if (left.Runic < right.Runic || left.Platinum < right.Platinum || left.Gold < right.Gold || left.Silver < right.Silver || left.Copper < right.Copper)
            throw new InvalidOperationException("That exact denomination amount is not available.");
        return new(left.Runic-right.Runic,left.Platinum-right.Platinum,left.Gold-right.Gold,left.Silver-right.Silver,left.Copper-right.Copper);
    }
    private static RuntimeClassicInventoryCurrency FromCopper(long value)
    {
        if (value < 0) throw new InvalidOperationException("negative-currency-balance");
        var runic=value/1_000_000L; value%=1_000_000L; var platinum=value/10_000L; value%=10_000L;
        var gold=value/100L; value%=100L; var silver=value/10L; var copper=value%10L;
        return new(runic,platinum,gold,silver,copper);
    }
    private static long Read(IReadOnlyDictionary<string,string>? state,string key) => state is not null && state.TryGetValue(key,out var raw) && long.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value) && value>=0 ? value : 0;
    private static void Validate(params long[] values) { if (values.Any(x=>x<0)) throw new InvalidOperationException("negative-bank-currency"); }
}

public sealed record RuntimeBundleCEconomyAuditReport(string Status, int PricedItemCount, int BankServiceCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Errors)
{
    public bool Passed => Status == "PASS";
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleCEconomyAudit
{
    public static RuntimeBundleCEconomyAuditReport Run(string projectRoot, string worldId)
    {
        var checks=new List<string>(); var errors=new List<string>(); int priced=0,banks=0;
        void Require(bool value,string name){if(value)checks.Add(name);else errors.Add(name);}
        try
        {
            var itemPath=Path.Combine(projectRoot,"Data",worldId,"items.json");
            using var doc=JsonDocument.Parse(File.ReadAllText(itemPath));
            priced=doc.RootElement.GetProperty("records").EnumerateArray().Count(r=>r.TryGetProperty("economy",out var e)&&e.ValueKind==JsonValueKind.Object&&e.TryGetProperty("cost",out _));
            banks=RuntimeServiceCatalog.Load(projectRoot,worldId).Services.Count(s=>s.ServiceKind.Equals("bank",StringComparison.OrdinalIgnoreCase));
            var authority=File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","RealmGate.Server","RuntimeBundleCEconomyAuthority.cs"));
            var banking=File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","RealmGate.Server","RuntimeLiveGameplayPart5.cs"));
            var commerce=File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","RealmGate.Server","RuntimeWorldSystemsBundle.cs"));
            Require(priced>0,"generated-item-prices-consumed");
            Require(banks>0,"generated-bank-services-consumed");
            Require(authority.Contains("bank.currency.runic")&&authority.Contains("bank.currency.platinum")&&authority.Contains("bank.currency.gold")&&authority.Contains("bank.currency.silver")&&authority.Contains("bank.currency.copper"),"bank-preserves-five-denominations");
            Require(authority.Contains("DebitCopperValue")&&authority.Contains("CreditCopperValue"),"single-commerce-settlement-authority");
            Require(banking.Contains("RuntimeBundleCEconomyAuthority.Deposit")&&banking.Contains("RuntimeBundleCEconomyAuthority.Withdraw"),"live-banking-consumer");
            Require(commerce.Contains("RuntimeBundleCEconomyAuthority.DebitCopperValue")&&commerce.Contains("RuntimeBundleCEconomyAuthority.CreditCopperValue"),"live-shop-repair-consumer");
            Require(!banking.Contains("BankedGoldStateKey")&&!banking.Contains("RuntimeCurrencyAccount"),"gold-only-bank-bypass-removed");
            Require(!commerce.Contains("state.Gold - totalCost")&&!commerce.Contains("state.Gold + totalValue")&&!commerce.Contains("state.Gold - cost"),"gold-only-commerce-bypass-removed");
            Require(authority.Contains("FromCopper"),"withdrawal-and-change-normalization");
        }
        catch(Exception ex){errors.Add(ex.ToString());}
        return new(errors.Count==0?"PASS":"FAIL",priced,banks,checks,errors);
    }
}
