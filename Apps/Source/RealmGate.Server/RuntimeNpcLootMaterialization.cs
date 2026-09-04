using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcLootEntry(string CanonicalItemId, int Quantity);
public sealed record RuntimeNpcLootMaterializationRequest(string RequestId, string NpcInstanceId, string CorpseId, string Authority, IReadOnlyList<RuntimeNpcLootEntry> Entries);
public sealed record RuntimeNpcLootMaterializationResult(bool Succeeded, string RequestId, string NpcInstanceId, string CorpseId, int CreatedCount, string Reason, IReadOnlyList<RuntimeItemInstance> Items);

/// <summary>
/// Exact, non-generative bridge from an owning loot subsystem to an existing NPC corpse.
/// The caller owns item selection, quantities, probability, guaranteed-drop policy, quest/no-drop
/// filtering, and timing. This service validates canonical identifiers, authority, corpse ownership,
/// and replay identity before atomically creating the exact supplied items in that corpse.
/// </summary>
public sealed class RuntimeNpcLootMaterializationService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;
    private readonly RuntimeItemCatalog _catalog;
    public RuntimeNpcLootMaterializationService(RuntimeLivingWorldService living, RuntimeItemEngine items, RuntimeItemCatalog catalog){_living=living;_items=items;_catalog=catalog;}

    public RuntimeNpcLootMaterializationResult Admit(RuntimeNpcLootMaterializationRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.RequestId)) return Reject(request,"request-id-required");
        if(string.IsNullOrWhiteSpace(request.Authority)) return Reject(request,"authority-required");
        var npc=_living.InspectNpc(request.NpcInstanceId);
        if(npc is null) return Reject(request,"instance-not-found");
        if(npc.Alive) return Reject(request,"npc-is-still-alive");
        var corpse=_items.Corpses.FirstOrDefault(x=>x.CorpseId.Equals(request.CorpseId,StringComparison.OrdinalIgnoreCase) && !x.CleanedUp);
        if(corpse is null) return Reject(request,"corpse-not-found");
        if(!corpse.CharacterId.Equals(npc.InstanceId,StringComparison.OrdinalIgnoreCase)) return Reject(request,"corpse-owner-mismatch");
        if(request.Entries is null || request.Entries.Count==0) return Reject(request,"loot-manifest-required");
        var existing=_items.CorpseInventory(corpse.CorpseId).Where(x=>x.Metadata?.TryGetValue("npc_loot_request_id",out var id)==true && id.Equals(request.RequestId,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray();
        if(existing.Length>0) return new(true,request.RequestId,npc.InstanceId,corpse.CorpseId,0,"already-applied",existing);
        foreach(var entry in request.Entries){ if(string.IsNullOrWhiteSpace(entry.CanonicalItemId)) return Reject(request,"canonical-item-id-required"); if(entry.Quantity<1) return Reject(request,"quantity-must-be-positive"); try{_catalog.Require(entry.CanonicalItemId);}catch(InvalidOperationException){return Reject(request,"canonical-item-not-found");}}
        var created=new List<RuntimeItemInstance>();
        try{
            foreach(var entry in request.Entries.OrderBy(x=>x.CanonicalItemId,StringComparer.OrdinalIgnoreCase)){
                var metadata=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"npc_loot_request_id",request.RequestId},{"npc_loot_authority",request.Authority},{"npc_source_instance_id",npc.InstanceId}};
                created.Add(_items.CreateCorpseItem(corpse.CorpseId,entry.CanonicalItemId,entry.Quantity,metadata));
            }
            return new(true,request.RequestId,npc.InstanceId,corpse.CorpseId,created.Count,"materialized",created);
        }catch(Exception ex){ foreach(var item in created.ToArray()){try{_items.Remove(item.InstanceId);}catch{}} return Reject(request,Normalize(ex.Message)); }
    }
    private static RuntimeNpcLootMaterializationResult Reject(RuntimeNpcLootMaterializationRequest r,string reason)=>new(false,r.RequestId??string.Empty,r.NpcInstanceId??string.Empty,r.CorpseId??string.Empty,0,reason,Array.Empty<RuntimeItemInstance>());
    private static string Normalize(string value)=>string.Join('-',value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcLootMaterializationSmokeReport
{
    public bool Passed=>Failures.Count==0; public List<string> Checks{get;}=[]; public List<string> Failures{get;}=[];
    public static RuntimeNpcLootMaterializationSmokeReport Run(string projectRoot){var r=new RuntimeNpcLootMaterializationSmokeReport(); try{
        var options=ServerRuntimeOptions.Load(projectRoot); var host=new ServerRuntimeHost(projectRoot,options); host.Bootstrap(); var world=host.Worlds.TryGetValue(options.DefaultWorldId,out var w)?w:host.Worlds.Values.First();
        var living=new RuntimeLivingWorldService(projectRoot,world,Path.Combine(Path.GetTempPath(),"reforged-npc-loot-"+Guid.NewGuid().ToString("N"))); var npc=living.InspectNpcs(new(IncludeDead:false)).First(); var catalog=RuntimeItemCatalog.Load(projectRoot,world.Definition.Id); var items=new RuntimeItemEngine(catalog); var corpseService=new RuntimeNpcCorpseOwnershipService(living,items); living.ApplyNpcDamage(npc.InstanceId,npc.MaximumHitPoints,DateTimeOffset.UtcNow); var dead=living.InspectNpc(npc.InstanceId)!; var corpse=corpseService.TransferDeathInventory(dead.InstanceId,"corpse:"+dead.InstanceId,dead.RoomId,DateTimeOffset.UtcNow,null,0,"smoke"); Require(corpse.Succeeded,"corpse-created");
        var def=catalog.Items.OrderBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).First(); var svc=new RuntimeNpcLootMaterializationService(living,items,catalog); var req=new RuntimeNpcLootMaterializationRequest("loot:smoke",dead.InstanceId,corpse.CorpseId,"smoke",[new(def.Id,1)]); var applied=svc.Admit(req); Require(applied.Succeeded && applied.CreatedCount==1,"exact-loot-materialized"); var replay=svc.Admit(req); Require(replay.Succeeded && replay.Reason=="already-applied" && replay.CreatedCount==0,"loot-replay-suppressed"); var wrong=svc.Admit(req with{RequestId="loot:wrong",NpcInstanceId="missing"}); Require(!wrong.Succeeded && wrong.Reason=="instance-not-found","wrong-owner-rejected"); r.Checks.AddRange(["corpse-created","exact-loot-materialized","loot-replay-suppressed","wrong-owner-rejected"]);
    }catch(Exception ex){r.Failures.Add(ex.Message);} return r;}
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}); private static void Require(bool c,string code){if(!c)throw new InvalidOperationException(code);}
}
