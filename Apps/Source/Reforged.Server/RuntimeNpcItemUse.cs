using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcItemUseResult(bool Succeeded, string InstanceId, string ItemInstanceId, string Reason, RuntimeItemInstance? Item);

/// <summary>Exact host-authorized NPC item-use boundary. It consumes only an already NPC-owned usable item. It does not select items, targets, effects, ammunition, or timing.</summary>
public sealed class RuntimeNpcItemUseService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;
    public RuntimeNpcItemUseService(RuntimeLivingWorldService living, RuntimeItemEngine items) { _living=living; _items=items; }
    public RuntimeNpcItemUseResult Use(string instanceId, string itemInstanceId, string authority)
    {
        var npc=_living.InspectNpc(instanceId); if(npc is null) return Reject(instanceId,itemInstanceId,"instance-not-found");
        if(!npc.Alive) return Reject(instanceId,itemInstanceId,"npc-is-not-alive");
        if(string.IsNullOrWhiteSpace(authority)) return Reject(instanceId,itemInstanceId,"authority-required");
        try { var item=_items.UseNpc(itemInstanceId,npc.InstanceId); return new(true,npc.InstanceId,item.InstanceId,"used",item); }
        catch(InvalidOperationException ex) { return Reject(instanceId,itemInstanceId,Normalize(ex.Message)); }
    }
    private static RuntimeNpcItemUseResult Reject(string i,string item,string reason)=>new(false,i??string.Empty,item??string.Empty,reason,null);
    private static string Normalize(string value)=>string.Join('-',value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcItemUseSmokeReport
{
    public bool Passed=>Failures.Count==0; public List<string> Checks{get;}=[]; public List<string> Failures{get;}=[];
    public static RuntimeNpcItemUseSmokeReport Run(string projectRoot)
    {
        var r=new RuntimeNpcItemUseSmokeReport();
        try {
            var options=ServerRuntimeOptions.Load(projectRoot); var host=new ServerRuntimeHost(projectRoot,options); host.Bootstrap();
            var world=host.Worlds.TryGetValue(options.DefaultWorldId,out var w)?w:host.Worlds.Values.First();
            var living=new RuntimeLivingWorldService(projectRoot,world,Path.Combine(Path.GetTempPath(),"reforged-npc-item-use-"+Guid.NewGuid().ToString("N")));
            var npc=living.InspectNpcs(new(IncludeDead:false)).First(); var catalog=RuntimeItemCatalog.Load(projectRoot,world.Definition.Id); var items=new RuntimeItemEngine(catalog);
            var def=catalog.Items.First(x=>x.Consumable || x.DefaultCharges>0); var owned=items.Create(def.Id,RuntimeItemLocations.Npc,npc.InstanceId,1,npc.InstanceId);
            var service=new RuntimeNpcItemUseService(living,items);
            Require(!service.Use(npc.InstanceId,owned.InstanceId,string.Empty).Succeeded,"authority-enforced");
            Require(service.Use(npc.InstanceId,owned.InstanceId,"smoke-host").Succeeded,"exact-owned-item-use");
            r.Checks.AddRange(["authority-enforced","exact-owned-item-use"]);
        } catch(Exception ex){r.Failures.Add(ex.Message);} return r;
    }
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
    private static void Require(bool c,string code){if(!c)throw new InvalidOperationException(code);}
}
