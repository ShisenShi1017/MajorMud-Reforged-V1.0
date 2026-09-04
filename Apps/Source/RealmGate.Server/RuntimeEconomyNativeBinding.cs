using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public enum RuntimeEconomyNativeConfidence { Proven, StronglyCorrelated, Weak, Unknown }
public enum RuntimeEconomyNativeSourceKind { WccDat, WbbDat, LibrariumProjection, SimulatorEvidence, PolicyOverride }

public sealed record RuntimeEconomyNativeSource(
    string SourceId,string RelativePath,RuntimeEconomyNativeSourceKind Kind,long Length,string Sha256,
    RuntimeEconomyNativeConfidence Confidence,string NativeDependency);

public sealed record RuntimeEconomyNativeShopBinding(
    string ShopId,int LegacyShopNumber,string Name,int ServiceType,long MinimumLevel,long MaximumLevel,
    int ClassLimit,string? RoomId,int MarkupPercent,IReadOnlyList<RuntimeEconomyNativeStockBinding> Inventory,
    RuntimeEconomyNativeConfidence Confidence,IReadOnlyList<string> EvidenceRefs);

public sealed record RuntimeEconomyNativeStockBinding(
    string ItemId,long NormalQuantity,long MaximumQuantity,long RegenerationQuantity,long RegenerationTime,
    int RegenerationPercent,RuntimeEconomyNativeConfidence Confidence);

public sealed record RuntimeEconomyNativeItemValueBinding(
    string ItemId,long BaseCost,int CostType,long GameLimit,int MaximumUses,bool Gettable,
    RuntimeEconomyNativeConfidence Confidence,string SourceRecordId);

public sealed record RuntimeEconomyNativeBindingCatalog(
    int SchemaVersion,string WorldId,IReadOnlyList<RuntimeEconomyNativeSource> Sources,
    IReadOnlyList<RuntimeEconomyNativeShopBinding> Shops,IReadOnlyList<RuntimeEconomyNativeItemValueBinding> Items,
    IReadOnlyDictionary<int,string> ServiceTypeOwnership,IReadOnlyDictionary<string,string> UnknownPolicies);

public sealed record RuntimeEconomyNativeBindingValidation(bool Valid,IReadOnlyList<string> Errors,string CatalogDigest);

public static class RuntimeEconomyNativeBindingEngine
{
    public const int CurrentSchemaVersion=1;

    public static RuntimeEconomyNativeBindingValidation Validate(RuntimeEconomyNativeBindingCatalog catalog)
    {
        var errors=new List<string>();
        if(catalog.SchemaVersion!=CurrentSchemaVersion) errors.Add("native-economy-schema-unsupported");
        if(string.IsNullOrWhiteSpace(catalog.WorldId)) errors.Add("native-economy-world-required");
        if(catalog.Sources.GroupBy(x=>x.SourceId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("native-economy-source-id-duplicate");
        if(catalog.Sources.Any(x=>x.Length<0||!IsSha256(x.Sha256)||string.IsNullOrWhiteSpace(x.RelativePath))) errors.Add("native-economy-source-invalid");
        if(catalog.Shops.GroupBy(x=>x.ShopId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("native-economy-shop-id-duplicate");
        if(catalog.Items.GroupBy(x=>x.ItemId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add("native-economy-item-id-duplicate");
        foreach(var shop in catalog.Shops)
        {
            if(shop.LegacyShopNumber<0||shop.MinimumLevel<0||shop.MaximumLevel<shop.MinimumLevel) errors.Add($"native-economy-shop-invalid:{shop.ShopId}");
            if(shop.MarkupPercent<0) errors.Add($"native-economy-markup-invalid:{shop.ShopId}");
            if(shop.Inventory.GroupBy(x=>x.ItemId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1)) errors.Add($"native-economy-stock-duplicate:{shop.ShopId}");
            foreach(var stock in shop.Inventory)
                if(stock.NormalQuantity<0||stock.MaximumQuantity<0||stock.NormalQuantity>stock.MaximumQuantity||stock.RegenerationQuantity<0||stock.RegenerationTime<0||stock.RegenerationPercent is <0 or >100)
                    errors.Add($"native-economy-stock-invalid:{shop.ShopId}:{stock.ItemId}");
        }
        foreach(var item in catalog.Items)
            if(item.BaseCost<0||item.GameLimit<0||item.MaximumUses< -1) errors.Add($"native-economy-item-value-invalid:{item.ItemId}");
        foreach(var shop in catalog.Shops)
            foreach(var stock in shop.Inventory)
                if(!catalog.Items.Any(x=>x.ItemId.Equals(stock.ItemId,StringComparison.OrdinalIgnoreCase))) errors.Add($"native-economy-item-reference-missing:{shop.ShopId}:{stock.ItemId}");
        return new(errors.Count==0,errors,ComputeDigest(catalog));
    }

    public static RuntimeMerchantLifecycleDefinition BindMerchantLifecycle(RuntimeEconomyNativeShopBinding shop,string merchantId,TimeSpan nativeTimeUnit)
    {
        if(nativeTimeUnit<=TimeSpan.Zero) throw new InvalidOperationException("native-economy-time-unit-required");
        var entries=shop.Inventory.Select(x=>new RuntimeMerchantRestockEntry(x.ItemId,RuntimeMerchantRestockMode.FillToTarget,x.RegenerationQuantity,x.NormalQuantity,x.MaximumQuantity,x.RegenerationPercent,true)).ToArray();
        var interval=shop.Inventory.Count==0?TimeSpan.Zero:TimeSpan.FromTicks(checked(nativeTimeUnit.Ticks*Math.Max(1,shop.Inventory.Min(x=>x.RegenerationTime))));
        return new(merchantId,shop.ShopId,new HashSet<string>(StringComparer.OrdinalIgnoreCase){$"native-service-type.{shop.ServiceType}"},Array.Empty<RuntimeMerchantScheduleWindow>(),
            new(RuntimeMerchantVisibilityMode.Public,new HashSet<string>(),new HashSet<string>(),new HashSet<string>(),shop.RoomId,shop.MinimumLevel,shop.MaximumLevel,null,null),
            entries,interval,0,true,true,"Economy.MerchantLifecycle","WCCSHOPS exact timer unit remains policy-bound");
    }

    public static RuntimeItemValueDefinition BindItemValueDefinition(RuntimeEconomyNativeItemValueBinding item)
    {
        return new(item.ItemId,item.BaseCost,false,item.Gettable,false,false,false,item.GameLimit>0,item.GameLimit==1,0,item.MaximumUses,
            "WCCITEMS base value and limit projected; durability, charge semantics and modifier order remain policy-bound");
    }

    public static RuntimeItemValuePolicy BindItemValuePolicy(RuntimeEconomicRoundingMode rounding)
    {
        return new(10_000,10_000,10_000,10_000,10_000,10_000,rounding,0,0,0,0,false,false,false,false);
    }

    public static string ComputeDigest(RuntimeEconomyNativeBindingCatalog catalog)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(catalog,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool IsSha256(string value)=>value.Length==64&&value.All(Uri.IsHexDigit);
}
