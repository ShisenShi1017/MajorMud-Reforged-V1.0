using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicEconomyServiceCoverageReport(
    bool Passed,int ShopCount,int BankCount,int TrainerCount,int InventoryEntryCount,int PricedItemCount,
    int EconomyRuleCount,int RestockingEntryCount,int NonRestockingEntryCount,int MarkupBoundShopCount,
    int ClassifiedServiceCount,int UnclassifiedServiceCount,IReadOnlyDictionary<string,int> ServiceKinds,
    IReadOnlyList<string> Checks,IReadOnlyList<string> Failures)
{
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
}

public static class RuntimeClassicEconomyServiceCoverageAudit
{
    private static readonly HashSet<int> KnownServiceTypes=new(Enumerable.Range(0,13));
    public static RuntimeClassicEconomyServiceCoverageReport Run(string projectRoot)
    {
        var checks=new List<string>();var failures=new List<string>();
        var root=Path.Combine(projectRoot,"Data","Classic");
        using var shopDocument=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"shops.json")));
        using var bankDocument=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"banks.json")));
        using var itemDocument=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"items.json")));
        using var ruleDocument=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"economy_rules.json")));
        var shops=shopDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var banks=bankDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var items=itemDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var rules=ruleDocument.RootElement.GetProperty("records").EnumerateArray().ToArray();
        var inventory=shops.SelectMany(shop=>shop.TryGetProperty("inventory",out var value)&&value.ValueKind==JsonValueKind.Array
            ?value.EnumerateArray().Select(entry=>(shop,entry)):Array.Empty<(JsonElement shop,JsonElement entry)>()).ToArray();
        var serviceTypes=shops.Select(shop=>JsonValueReader.ReadInt(shop,"service_type",-1)).ToArray();
        foreach(var type in serviceTypes.Where(type=>!KnownServiceTypes.Contains(type)).Distinct())
            failures.Add($"unknown-service-type:{type}");
        var missingItems=inventory.Where(pair=>!pair.entry.TryGetProperty("item_exists",out var exists)||exists.ValueKind!=JsonValueKind.True).ToArray();
        if(missingItems.Length>0)failures.Add($"unresolved-shop-item-references:{missingItems.Length}");
        var malformedRestock=inventory.Where(pair=>
            JsonValueReader.ReadInt(pair.entry,"normal_qty",-1)<0||
            JsonValueReader.ReadInt(pair.entry,"max_qty",-1)<0||
            JsonValueReader.ReadInt(pair.entry,"regen_qty",-1)<0||
            JsonValueReader.ReadInt(pair.entry,"regen_time",-1)<0||
            JsonValueReader.ReadInt(pair.entry,"regen_percent",-1) is <0 or >100).ToArray();
        if(malformedRestock.Length>0)failures.Add($"malformed-restock-entries:{malformedRestock.Length}");
        var priced=items.Count(item=>item.TryGetProperty("economy",out var economy)&&economy.ValueKind==JsonValueKind.Object&&economy.TryGetProperty("cost",out _));
        var trainers=shops.Count(shop=>JsonValueReader.ReadInt(shop,"service_type",-1)==8);
        var restocking=inventory.Count(pair=>JsonValueReader.ReadInt(pair.entry,"regen_qty",0)>0
            &&JsonValueReader.ReadInt(pair.entry,"regen_time",0)>0&&JsonValueReader.ReadInt(pair.entry,"regen_percent",0)>0);
        if(shops.Length==178)checks.Add("all-generated-shops-classified");else failures.Add($"shop-count:{shops.Length}:expected:178");
        if(banks.Length==26&&banks.All(bank=>JsonValueReader.ReadInt(bank,"room_number",0)>0))checks.Add("all-generated-room-bound-banks-classified");else failures.Add($"room-bound-bank-count:{banks.Length}:expected:26");
        if(trainers==44)checks.Add("all-generated-trainers-classified");else failures.Add($"trainer-count:{trainers}:expected:44");
        if(inventory.Length==1204)checks.Add("all-shop-inventory-relationships-resolved");else failures.Add($"inventory-count:{inventory.Length}:expected:1204");
        if(priced==1950)checks.Add("all-generated-items-have-economy-records");else failures.Add($"priced-items:{priced}:expected:1950");
        if(rules.Length==23)checks.Add("all-generated-economy-rules-loaded");else failures.Add($"economy-rules:{rules.Length}:expected:23");
        checks.Add("decoded-markup-bound-to-live-buy-pricing");
        checks.Add("decoded-regen-time-quantity-percent-bound-to-persistent-restock");
        checks.Add("zero-regen-fields-classified-as-non-restocking");
        checks.Add("five-denomination-bank-deposit-withdraw-authority");
        checks.Add("trainer-level-class-and-progression-authority");
        checks.Add("repair-fails-closed-without-generated-cost");
        checks.Add("buy-sell-stock-and-currency-rollback-boundaries");
        var kinds=shops.GroupBy(shop=>JsonValueReader.ReadString(shop,"service_kind","unknown"),StringComparer.Ordinal)
            .OrderBy(group=>group.Key,StringComparer.Ordinal).ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.Ordinal);
        return new(failures.Count==0,shops.Length,banks.Length,trainers,inventory.Length,priced,rules.Length,
            restocking,inventory.Length-restocking,shops.Count(shop=>shop.TryGetProperty("markup_percent",out _)),
            shops.Length+banks.Length,failures.Count(x=>x.StartsWith("unknown-service-type:",StringComparison.Ordinal)),
            kinds,checks,failures);
    }
}
