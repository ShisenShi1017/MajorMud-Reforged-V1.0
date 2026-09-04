using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcEquipmentResult(bool Succeeded, string InstanceId, string ItemInstanceId, string? Slot, string Reason, RuntimeItemInstance? Item);

/// <summary>Exact host-authorized NPC equipment ownership boundary. It equips or unequips an already NPC-owned item in an exact generated slot. It does not select equipment, infer class/race eligibility, or invoke item-use AI.</summary>
public sealed class RuntimeNpcEquipmentOwnershipService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeItemEngine _items;
    public RuntimeNpcEquipmentOwnershipService(RuntimeLivingWorldService living, RuntimeItemEngine items) { _living=living; _items=items; }
    public IReadOnlyList<RuntimeItemInstance> Inspect(string instanceId) => _items.NpcEquipment(instanceId);
    public RuntimeNpcEquipmentResult Equip(string instanceId, string itemInstanceId, string exactSlot, string authority)
    {
        var npc=_living.InspectNpc(instanceId); if (npc is null) return Reject(instanceId,itemInstanceId,exactSlot,"instance-not-found");
        if (!npc.Alive) return Reject(instanceId,itemInstanceId,exactSlot,"npc-is-not-alive");
        if (string.IsNullOrWhiteSpace(authority)) return Reject(instanceId,itemInstanceId,exactSlot,"authority-required");
        try { var item=_items.EquipNpc(itemInstanceId,npc.InstanceId,exactSlot); return new(true,npc.InstanceId,item.InstanceId,item.EquippedSlot,"equipped",item); }
        catch(InvalidOperationException ex) { return Reject(instanceId,itemInstanceId,exactSlot,Normalize(ex.Message)); }
    }
    public RuntimeNpcEquipmentResult Unequip(string instanceId, string itemInstanceId, string authority)
    {
        var npc=_living.InspectNpc(instanceId); if (npc is null) return Reject(instanceId,itemInstanceId,null,"instance-not-found");
        if (!npc.Alive) return Reject(instanceId,itemInstanceId,null,"npc-is-not-alive");
        if (string.IsNullOrWhiteSpace(authority)) return Reject(instanceId,itemInstanceId,null,"authority-required");
        try { var item=_items.UnequipNpc(itemInstanceId,npc.InstanceId); return new(true,npc.InstanceId,item.InstanceId,null,"unequipped",item); }
        catch(InvalidOperationException ex) { return Reject(instanceId,itemInstanceId,null,Normalize(ex.Message)); }
    }
    private static RuntimeNpcEquipmentResult Reject(string i,string item,string? slot,string reason)=>new(false,i??string.Empty,item??string.Empty,slot,reason,null);
    private static string Normalize(string value)=>string.Join('-',value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcEquipmentOwnershipSmokeReport
{
    public bool Passed=>Failures.Count==0; public List<string> Checks{get;}=[]; public List<string> Failures{get;}=[];
    public static RuntimeNpcEquipmentOwnershipSmokeReport Run(string projectRoot)
    {
        var r=new RuntimeNpcEquipmentOwnershipSmokeReport();
        try {
            var options=ServerRuntimeOptions.Load(projectRoot); var host=new ServerRuntimeHost(projectRoot,options); host.Bootstrap();
            var world=host.Worlds.TryGetValue(options.DefaultWorldId,out var w)?w:host.Worlds.Values.First();
            var living=new RuntimeLivingWorldService(projectRoot,world,Path.Combine(Path.GetTempPath(),"reforged-npc-equipment-"+Guid.NewGuid().ToString("N")));
            var npc=living.InspectNpcs(new(IncludeDead:false)).First(); var catalog=RuntimeItemCatalog.Load(projectRoot,world.Definition.Id); var items=new RuntimeItemEngine(catalog);
            var def=catalog.EquipmentDefinitions.First(x=>x.Slot!="NONE"); var carried=items.Create(def.ItemId,RuntimeItemLocations.Npc,npc.InstanceId,1,npc.InstanceId);
            var service=new RuntimeNpcEquipmentOwnershipService(living,items);
            Require(!service.Equip(npc.InstanceId,carried.InstanceId,def.Slot,string.Empty).Succeeded,"authority-enforced");
            var equipped=service.Equip(npc.InstanceId,carried.InstanceId,def.Slot,"smoke-host"); Require(equipped.Succeeded && service.Inspect(npc.InstanceId).Count==1,"exact-npc-equip");
            var removed=service.Unequip(npc.InstanceId,carried.InstanceId,"smoke-host"); Require(removed.Succeeded && service.Inspect(npc.InstanceId).Count==0,"exact-npc-unequip");
            r.Checks.AddRange(["authority-enforced","exact-npc-equip","exact-npc-unequip"]);
        } catch(Exception ex){r.Failures.Add(ex.Message);} return r;
    }
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
    private static void Require(bool c,string code){if(!c)throw new InvalidOperationException(code);}
}
