using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcMerchantStockSnapshot(string NpcInstanceId, string ShopId, IReadOnlyList<RuntimeShopStockEntry> Entries);
public sealed record RuntimeNpcMerchantStockMutation(string NpcInstanceId, string ShopId, string CanonicalItemId, int Quantity, DateTimeOffset NextRestockUtc, string Authority);
public sealed record RuntimeNpcMerchantStockResult(bool Succeeded, string NpcInstanceId, string ShopId, string CanonicalItemId, int Quantity, string Reason);

/// <summary>
/// Exact ownership bridge between a living merchant NPC and the existing persistent shop-state store.
/// The caller owns restock policy, restrictions, hours, pricing, and merchant-gold semantics. This
/// boundary only validates the owning NPC/service relationship and applies the exact supplied stock.
/// </summary>
public sealed class RuntimeNpcMerchantStockOwnershipService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeGameplayCompletionStateStore _store;
    private readonly RuntimeItemCatalog _items;
    public RuntimeNpcMerchantStockOwnershipService(RuntimeLivingWorldService living, RuntimeGameplayCompletionStateStore store, RuntimeItemCatalog items){_living=living;_store=store;_items=items;}

    public RuntimeNpcMerchantStockSnapshot Inspect(string npcInstanceId, RuntimeServiceDefinition shop)
    {
        var npc=RequireMerchant(npcInstanceId,shop);
        var state=_store.Read(shop);
        return new(npc.InstanceId,shop.Id,state.Stock.Values.OrderBy(x=>x.ItemId,StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public RuntimeNpcMerchantStockResult SetExact(RuntimeNpcMerchantStockMutation request, RuntimeServiceDefinition shop)
    {
        if(string.IsNullOrWhiteSpace(request.Authority)) return Reject(request,"authority-required");
        if(!shop.Id.Equals(request.ShopId,StringComparison.OrdinalIgnoreCase)) return Reject(request,"shop-id-mismatch");
        RuntimeNpcInspectionSnapshot npc;
        try{npc=RequireMerchant(request.NpcInstanceId,shop);}catch(InvalidOperationException ex){return Reject(request,Normalize(ex.Message));}
        if(string.IsNullOrWhiteSpace(request.CanonicalItemId)) return Reject(request,"canonical-item-id-required");
        try{_items.Require(request.CanonicalItemId);}catch(InvalidOperationException){return Reject(request,"canonical-item-not-found");}
        if(request.Quantity<0) return Reject(request,"quantity-must-be-nonnegative");
        _store.ExecuteShopTransaction(shop,stock=>{stock[request.CanonicalItemId]=new RuntimeShopStockEntry(request.CanonicalItemId,request.Quantity,request.NextRestockUtc); return 0;});
        return new(true,npc.InstanceId,shop.Id,request.CanonicalItemId,request.Quantity,"stock-set-exactly");
    }

    private RuntimeNpcInspectionSnapshot RequireMerchant(string instanceId, RuntimeServiceDefinition shop)
    {
        var npc=_living.InspectNpc(instanceId) ?? throw new InvalidOperationException("instance-not-found");
        if(!npc.Alive) throw new InvalidOperationException("npc-is-not-alive");
        if(!(npc.InteractionModel.Contains("service",StringComparison.OrdinalIgnoreCase)||npc.InteractionModel.Contains("shop",StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("npc-does-not-own-merchant-service");
        if(!shop.ServiceKind.Contains("shop",StringComparison.OrdinalIgnoreCase) && !shop.ServiceKind.Contains("merchant",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("service-is-not-a-shop");
        return npc;
    }
    private static RuntimeNpcMerchantStockResult Reject(RuntimeNpcMerchantStockMutation r,string reason)=>new(false,r.NpcInstanceId??string.Empty,r.ShopId??string.Empty,r.CanonicalItemId??string.Empty,r.Quantity,reason);
    private static string Normalize(string value)=>string.Join('-',value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcMerchantStockOwnershipSmokeReport
{
    public bool Passed=>Failures.Count==0; public List<string> Checks{get;}=[]; public List<string> Failures{get;}=[];
    public static RuntimeNpcMerchantStockOwnershipSmokeReport Run(){var r=new RuntimeNpcMerchantStockOwnershipSmokeReport();try{var t=typeof(RuntimeNpcMerchantStockOwnershipService);Require(t.GetMethod(nameof(RuntimeNpcMerchantStockOwnershipService.Inspect)) is not null,"merchant-stock-inspection-exposed");Require(t.GetMethod(nameof(RuntimeNpcMerchantStockOwnershipService.SetExact)) is not null,"merchant-stock-exact-mutation-exposed");r.Checks.AddRange(["merchant-stock-inspection-exposed","merchant-stock-exact-mutation-exposed"]);}catch(Exception ex){r.Failures.Add(ex.Message);}return r;}
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}); private static void Require(bool c,string code){if(!c)throw new InvalidOperationException(code);}
}
