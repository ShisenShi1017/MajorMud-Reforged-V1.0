using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcEquipmentSelectionAdmission(bool Admitted,string InstanceId,string ItemInstanceId,string ExactSlot,string? RequestId,string Reason);
public sealed record RuntimeNpcEquipmentSelectionCompletion(bool Succeeded,string InstanceId,string ItemInstanceId,string ExactSlot,string RequestId,bool Equipped,string Reason,RuntimeItemInstance? Item);

public sealed partial class RuntimeLivingWorldService
{
    public RuntimeNpcEquipmentSelectionAdmission AdmitNpcEquipmentSelection(string instanceId,string itemInstanceId,string exactSlot,string authority,DateTimeOffset nowUtc)
    {
        lock(_gate)
        {
            if(!_npcs.TryGetValue(instanceId,out var current)) return new(false,instanceId??string.Empty,itemInstanceId??string.Empty,exactSlot??string.Empty,null,"instance-not-found");
            if(!current.Alive) return new(false,current.InstanceId,itemInstanceId??string.Empty,exactSlot??string.Empty,null,"npc-is-not-alive");
            if(string.IsNullOrWhiteSpace(itemInstanceId)) return new(false,current.InstanceId,string.Empty,exactSlot??string.Empty,null,"item-instance-id-required");
            if(string.IsNullOrWhiteSpace(exactSlot)) return new(false,current.InstanceId,itemInstanceId,string.Empty,null,"exact-slot-required");
            if(string.IsNullOrWhiteSpace(authority)) return new(false,current.InstanceId,itemInstanceId,exactSlot,null,"authority-required");
            if(current.Metadata.TryGetValue("npc_equipment_selection_active_request_id",out var active)&&!string.IsNullOrWhiteSpace(active)) return new(false,current.InstanceId,itemInstanceId,exactSlot,active,"equipment-selection-already-active");
            var generation=ReadEquipmentGeneration(current.Metadata)+1;
            var requestId=$"{current.InstanceId}:equipment-selection:{generation.ToString(CultureInfo.InvariantCulture)}";
            var metadata=new Dictionary<string,string>(current.Metadata,StringComparer.OrdinalIgnoreCase)
            {
                ["npc_equipment_selection_generation"]=generation.ToString(CultureInfo.InvariantCulture),
                ["npc_equipment_selection_active_request_id"]=requestId,
                ["npc_equipment_selection_item_instance_id"]=itemInstanceId.Trim(),
                ["npc_equipment_selection_exact_slot"]=exactSlot.Trim(),
                ["npc_equipment_selection_authority"]=authority.Trim(),
                ["npc_equipment_selection_admitted_at_utc"]=nowUtc.ToString("O")
            };
            _npcs[current.InstanceId]=current with{Metadata=metadata}; Save();
            return new(true,current.InstanceId,itemInstanceId.Trim(),exactSlot.Trim(),requestId,"admitted");
        }
    }
    public bool CompleteNpcEquipmentSelection(string instanceId,string requestId,bool equipped,string outcome,DateTimeOffset nowUtc)
    {
        lock(_gate)
        {
            if(!_npcs.TryGetValue(instanceId,out var current)) return false;
            if(!current.Metadata.TryGetValue("npc_equipment_selection_active_request_id",out var active)||!active.Equals(requestId,StringComparison.Ordinal)) return false;
            var metadata=new Dictionary<string,string>(current.Metadata,StringComparer.OrdinalIgnoreCase)
            {
                ["npc_equipment_selection_last_request_id"]=requestId,
                ["npc_equipment_selection_last_equipped"]=equipped?"true":"false",
                ["npc_equipment_selection_last_outcome"]=string.IsNullOrWhiteSpace(outcome)?(equipped?"equipped":"not-equipped"):outcome.Trim(),
                ["npc_equipment_selection_completed_at_utc"]=nowUtc.ToString("O")
            };
            foreach(var key in new[]{"npc_equipment_selection_active_request_id","npc_equipment_selection_item_instance_id","npc_equipment_selection_exact_slot","npc_equipment_selection_authority","npc_equipment_selection_admitted_at_utc"}) metadata.Remove(key);
            _npcs[current.InstanceId]=current with{Metadata=metadata}; Save(); return true;
        }
    }
    private static long ReadEquipmentGeneration(IReadOnlyDictionary<string,string> metadata)=>metadata.TryGetValue("npc_equipment_selection_generation",out var raw)&&long.TryParse(raw,NumberStyles.None,CultureInfo.InvariantCulture,out var value)&&value>=0?value:0;
}

/// <summary>Two-phase equipment-selection handoff. A policy subsystem must identify the exact already-owned item and exact generated slot; this service validates and commits only that request.</summary>
public sealed class RuntimeNpcEquipmentSelectionService
{
    private readonly RuntimeLivingWorldService _living; private readonly RuntimeItemEngine _items; private readonly RuntimeNpcEquipmentOwnershipService _equipment;
    public RuntimeNpcEquipmentSelectionService(RuntimeLivingWorldService living,RuntimeItemEngine items){_living=living;_items=items;_equipment=new(living,items);}
    public RuntimeNpcEquipmentSelectionAdmission Admit(string instanceId,string itemInstanceId,string exactSlot,string authority,DateTimeOffset nowUtc)
    {
        var npc=_living.InspectNpc(instanceId); if(npc is null) return new(false,instanceId??string.Empty,itemInstanceId??string.Empty,exactSlot??string.Empty,null,"instance-not-found");
        try{var item=_items.RequireInstance(itemInstanceId); if(!string.Equals(item.OwnerId,npc.InstanceId,StringComparison.OrdinalIgnoreCase)||!item.LocationKind.Equals(RuntimeItemLocations.Npc,StringComparison.OrdinalIgnoreCase)) return new(false,npc.InstanceId,itemInstanceId,exactSlot??string.Empty,null,"item-is-not-carried-by-npc");}
        catch(InvalidOperationException ex){return new(false,npc.InstanceId,itemInstanceId??string.Empty,exactSlot??string.Empty,null,Normalize(ex.Message));}
        return _living.AdmitNpcEquipmentSelection(instanceId,itemInstanceId,exactSlot,authority,nowUtc);
    }
    public RuntimeNpcEquipmentSelectionCompletion Complete(string instanceId,string requestId,bool shouldEquip,string outcome,DateTimeOffset nowUtc)
    {
        var npc=_living.InspectNpc(instanceId); if(npc is null) return new(false,instanceId??string.Empty,string.Empty,string.Empty,requestId??string.Empty,false,"instance-not-found",null);
        if(!npc.Metadata.TryGetValue("npc_equipment_selection_active_request_id",out var active)||!active.Equals(requestId,StringComparison.Ordinal)) return new(false,npc.InstanceId,string.Empty,string.Empty,requestId??string.Empty,false,"request-id-mismatch",null);
        var itemId=npc.Metadata.TryGetValue("npc_equipment_selection_item_instance_id",out var i)?i:string.Empty; var slot=npc.Metadata.TryGetValue("npc_equipment_selection_exact_slot",out var s)?s:string.Empty; RuntimeItemInstance? item=null;
        if(shouldEquip){var result=_equipment.Equip(instanceId,itemId,slot,"equipment-selection-completion"); if(!result.Succeeded) return new(false,npc.InstanceId,itemId,slot,requestId,false,result.Reason,null); item=result.Item;}
        if(!_living.CompleteNpcEquipmentSelection(instanceId,requestId,shouldEquip,outcome,nowUtc)) return new(false,npc.InstanceId,itemId,slot,requestId,shouldEquip,"completion-race-rejected",item);
        return new(true,npc.InstanceId,itemId,slot,requestId,shouldEquip,shouldEquip?"equipped":"selection-declined",item);
    }
    private static string Normalize(string value)=>string.Join('-',value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcEquipmentSelectionSmokeReport
{
    public bool Passed=>Failures.Count==0; public List<string> Checks{get;}=[]; public List<string> Failures{get;}=[];
    public static RuntimeNpcEquipmentSelectionSmokeReport Run(string projectRoot)
    {
        var r=new RuntimeNpcEquipmentSelectionSmokeReport(); try{
            var options=ServerRuntimeOptions.Load(projectRoot); var host=new ServerRuntimeHost(projectRoot,options); host.Bootstrap(); var world=host.Worlds.TryGetValue(options.DefaultWorldId,out var w)?w:host.Worlds.Values.First();
            var living=new RuntimeLivingWorldService(projectRoot,world,Path.Combine(Path.GetTempPath(),"reforged-npc-equipment-selection-"+Guid.NewGuid().ToString("N"))); var npc=living.InspectNpcs(new(IncludeDead:false)).First(); var catalog=RuntimeItemCatalog.Load(projectRoot,world.Definition.Id); var items=new RuntimeItemEngine(catalog); var def=catalog.EquipmentDefinitions.First(x=>x.Slot!="NONE"); var owned=items.Create(def.ItemId,RuntimeItemLocations.Npc,npc.InstanceId,1,npc.InstanceId); var service=new RuntimeNpcEquipmentSelectionService(living,items);
            Require(!service.Admit(npc.InstanceId,owned.InstanceId,def.Slot,string.Empty,DateTimeOffset.UtcNow).Admitted,"authority-enforced"); var admission=service.Admit(npc.InstanceId,owned.InstanceId,def.Slot,"smoke-policy",DateTimeOffset.UtcNow); Require(admission.Admitted&&admission.RequestId is not null,"exact-selection-admitted"); Require(!service.Complete(npc.InstanceId,admission.RequestId+":stale",true,"equip",DateTimeOffset.UtcNow).Succeeded,"stale-completion-rejected"); Require(service.Complete(npc.InstanceId,admission.RequestId!,true,"equip",DateTimeOffset.UtcNow).Succeeded,"exact-selection-equipped"); r.Checks.AddRange(["authority-enforced","exact-selection-admitted","stale-completion-rejected","exact-selection-equipped"]);
        }catch(Exception ex){r.Failures.Add(ex.Message);} return r;
    }
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}); private static void Require(bool c,string code){if(!c)throw new InvalidOperationException(code);}
}
