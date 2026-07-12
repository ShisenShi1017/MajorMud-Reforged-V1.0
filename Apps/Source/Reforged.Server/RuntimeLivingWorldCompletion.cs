using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcAiState(string InstanceId,string Mode,string? TargetActorId,string HomeRoomId,string CurrentRoomId,int LeashSteps,DateTimeOffset UpdatedAtUtc);
public sealed record RuntimeLivingCorpseState(string CorpseId,string NpcInstanceId,string RoomId,DateTimeOffset CreatedAtUtc,DateTimeOffset ExpiresAtUtc);
public sealed record RuntimeLivingWorldEvent(long Sequence,string Kind,string EntityId,string RoomId,DateTimeOffset OccurredAtUtc,IReadOnlyDictionary<string,string> Metadata);
public sealed record RuntimeExitGateDefinition(string ExitId,string RoomId,string Direction,string GateKind,string Qualifier,string? RequiredItemId,long TollAmount,int Damage,int SpellId,int PreSpellId,int PostSpellId,IReadOnlyList<string> Commands,string RuntimeDisposition);
public sealed record RuntimeExitGateContext(IReadOnlySet<string> ItemIds,long AvailableGold,IReadOnlySet<string> DiscoveredExitIds,string? EnteredCommand);
public sealed record RuntimeExitGateDecision(bool Allowed,bool EvidenceOnly,string Code,long TollCharged,int Damage,int SpellId,int PreSpellId,int PostSpellId);
public sealed record RuntimeLivingWorldCompletionSnapshot(string WorldId,long EventSequence,IReadOnlyList<RuntimeNpcAiState> AiStates,IReadOnlyList<RuntimeLivingCorpseState> Corpses,IReadOnlyList<RuntimeLivingWorldEvent> RecentEvents,DateTimeOffset SavedAtUtc);

public sealed partial class RuntimeLivingWorldService
{
    private readonly Dictionary<string,RuntimeNpcAiState> _npcAi = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeLivingCorpseState> _corpses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeExitGateDefinition> _gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<RuntimeLivingWorldEvent> _recentEvents = new();
    private long _eventSequence;
    private static readonly HashSet<string> SupportedGateKinds = new(new[]{"door","hidden","required_item","cast_transition","spell_trap","damage_trap","text_command","toll","qualified_unknown","open"},StringComparer.OrdinalIgnoreCase);

    public int CorpseCount { get { lock(_gate) return _corpses.Count; } }
    public int GateCount { get { lock(_gate) return _gates.Count; } }
    public IReadOnlyList<RuntimeNpcAiState> CaptureNpcAi(){lock(_gate)return _npcAi.Values.OrderBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray();}
    public IReadOnlyList<RuntimeExitGateDefinition> CaptureGates(){lock(_gate)return _gates.Values.OrderBy(x=>x.ExitId,StringComparer.OrdinalIgnoreCase).ToArray();}
    public IReadOnlyList<RuntimeLivingWorldEvent> EventsAfter(long sequence){lock(_gate)return _recentEvents.Where(x=>x.Sequence>sequence).OrderBy(x=>x.Sequence).ToArray();}
    public IReadOnlyList<RuntimeLivingCorpseState> CorpsesInRoom(string roomId){lock(_gate)return _corpses.Values.Where(x=>x.RoomId.Equals(roomId,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.CreatedAtUtc).ToArray();}

    private void InitializeLivingWorldCompletion()
    {
        LoadGeneratedGateDefinitions();
        foreach(var npc in _npcs.Values)
            _npcAi[npc.InstanceId]=new(npc.InstanceId,"idle",null,npc.HomeRoomId,npc.RoomId,0,DateTimeOffset.UtcNow);
    }

    public RuntimeExitGateDecision EvaluateExitGate(RuntimeRoom room,string direction,RuntimeExitGateContext context)
    {
        var normalized=NormalizeDirection(direction);
        lock(_gate)
        {
            var exit=room.Exits.FirstOrDefault(x=>NormalizeDirection(x.Direction).Equals(normalized,StringComparison.OrdinalIgnoreCase));
            if(exit is null)return new(false,false,"exit-not-found",0,0,0,0,0);
            if(exit.HasBlockedDestination||_world.FindRoom(exit.TargetMapId,exit.TargetRoomNumber) is null)return new(false,false,"destination-blocked",0,0,0,0,0);
            if(!_gates.TryGetValue(exit.Id,out var gate))return new(true,false,"open",0,0,0,0,0);
            if(!SupportedGateKinds.Contains(gate.GateKind))return new(false,true,"unsupported-evidence-only",0,0,0,0,0);
            switch(gate.GateKind.ToLowerInvariant())
            {
                case "open": return new(true,false,"open",0,0,0,0,0);
                case "door":
                    if(!_objects.TryGetValue(gate.ExitId,out var door))return new(false,true,"door-state-unavailable",0,0,0,0,0);
                    return door.Locked?new(false,false,"door-locked",0,0,0,0,0):!door.Open?new(false,false,"door-closed",0,0,0,0,0):new(true,false,"door-open",0,0,0,0,0);
                case "hidden": return context.DiscoveredExitIds.Contains(gate.ExitId)?new(true,false,"hidden-discovered",0,0,0,0,0):new(false,false,"hidden-undiscovered",0,0,0,0,0);
                case "required_item": return !string.IsNullOrWhiteSpace(gate.RequiredItemId)&&context.ItemIds.Contains(gate.RequiredItemId)?new(true,false,"required-item-present",0,0,0,0,0):new(false,false,"required-item-missing",0,0,0,0,0);
                case "text_command": return gate.Commands.Any(x=>x.Equals(context.EnteredCommand,StringComparison.OrdinalIgnoreCase))?new(true,false,"text-command-matched",0,0,0,0,0):new(false,false,"text-command-required",0,0,0,0,0);
                case "toll": return context.AvailableGold>=gate.TollAmount?new(true,false,"toll-paid",gate.TollAmount,0,0,0,0):new(false,false,"toll-insufficient",0,0,0,0,0);
                case "damage_trap": return new(true,false,"damage-trap",0,gate.Damage,0,0,0);
                case "spell_trap": return gate.SpellId>0?new(true,false,"spell-trap",0,0,gate.SpellId,0,0):new(false,true,"spell-trap-evidence-only",0,0,0,0,0);
                case "cast_transition": return gate.PreSpellId>0||gate.PostSpellId>0?new(true,false,"cast-transition",0,0,0,gate.PreSpellId,gate.PostSpellId):new(false,true,"cast-transition-evidence-only",0,0,0,0,0);
                case "qualified_unknown": return new(false,true,"qualified-unknown-evidence-only",0,0,0,0,0);
                default: return new(false,true,"gate-evidence-only",0,0,0,0,0);
            }
        }
    }

    public bool AcquireNpcTarget(string instanceId,string actorId,string actorRoomId,DateTimeOffset nowUtc)
    {
        lock(_gate)
        {
            if(!_npcs.TryGetValue(instanceId,out var npc)||!npc.Alive||!npc.RoomId.Equals(actorRoomId,StringComparison.OrdinalIgnoreCase))return false;
            if(!npc.InteractionModel.Contains("combat",StringComparison.OrdinalIgnoreCase))return false;
            _npcAi[instanceId]=_npcAi[instanceId] with{Mode="combat",TargetActorId=actorId,UpdatedAtUtc=nowUtc};
            PublishLivingWorldEventLocked("npc-aggro",instanceId,npc.RoomId,nowUtc,new Dictionary<string,string>{{"target",actorId}}); SaveLivingWorldCompletionLocked(); return true;
        }
    }

    public bool ClearNpcTarget(string instanceId,DateTimeOffset nowUtc)
    {
        lock(_gate)
        {
            if(!_npcAi.TryGetValue(instanceId,out var ai))return false;
            _npcAi[instanceId]=ai with{Mode="return-home",TargetActorId=null,UpdatedAtUtc=nowUtc};
            return true;
        }
    }

    private IReadOnlyList<string> TickLivingWorldCompletionLocked(DateTimeOffset nowUtc)
    {
        var results=new List<string>();
        foreach(var corpse in _corpses.Values.Where(x=>x.ExpiresAtUtc<=nowUtc).ToArray())
        {
            _corpses.Remove(corpse.CorpseId); results.Add("corpse-expired:"+corpse.CorpseId); PublishLivingWorldEventLocked("corpse-expired",corpse.CorpseId,corpse.RoomId,nowUtc);
        }
        foreach(var npc in _npcs.Values.Where(x=>x.Alive).ToArray())
        {
            var ai=_npcAi[npc.InstanceId];
            if(ai.Mode=="dead")
            {
                _npcAi[npc.InstanceId]=ai with{Mode="idle",CurrentRoomId=npc.RoomId,TargetActorId=null,UpdatedAtUtc=nowUtc};
                results.Add("npc-ai-respawn:"+npc.InstanceId); PublishLivingWorldEventLocked("npc-respawn",npc.InstanceId,npc.RoomId,nowUtc);
                continue;
            }
            if(ai.Mode=="return-home")
            {
                _npcs[npc.InstanceId]=npc with{RoomId=npc.HomeRoomId};
                _npcAi[npc.InstanceId]=ai with{Mode="idle",CurrentRoomId=npc.HomeRoomId,LeashSteps=0,UpdatedAtUtc=nowUtc};
                results.Add("npc-return-home:"+npc.InstanceId); PublishLivingWorldEventLocked("npc-moved",npc.InstanceId,npc.HomeRoomId,nowUtc);
            }
        }
        return results;
    }

    private void RecordNpcDeathLocked(RuntimeLivingNpcState npc,DateTimeOffset nowUtc,DateTimeOffset? respawnAt)
    {
        var expiry=respawnAt??nowUtc.AddMinutes(5);
        var corpse=new RuntimeLivingCorpseState("corpse:"+npc.InstanceId,npc.InstanceId,npc.RoomId,nowUtc,expiry);
        _corpses[corpse.CorpseId]=corpse;
        _npcAi[npc.InstanceId]=_npcAi[npc.InstanceId] with{Mode="dead",TargetActorId=null,UpdatedAtUtc=nowUtc};
        PublishLivingWorldEventLocked("corpse-created",corpse.CorpseId,corpse.RoomId,nowUtc);
    }

    private void PublishLivingWorldEventLocked(string kind,string entityId,string roomId,DateTimeOffset at,IReadOnlyDictionary<string,string>? metadata=null)
    {
        _recentEvents.Enqueue(new RuntimeLivingWorldEvent(++_eventSequence,kind,entityId,roomId,at,metadata??new Dictionary<string,string>()));
        while(_recentEvents.Count>2048)_recentEvents.Dequeue();
    }

    private void LoadGeneratedGateDefinitions()
    {
        var path=Path.Combine(_projectRoot,_world.Definition.DataRoot,_world.Definition.ExitsFile); if(!File.Exists(path))return;
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        var records=document.RootElement.ValueKind==JsonValueKind.Array?document.RootElement:document.RootElement.TryGetProperty("records",out var r)&&r.ValueKind==JsonValueKind.Array?r:default;
        if(records.ValueKind!=JsonValueKind.Array)return;
        foreach(var value in records.EnumerateArray())
        {
            var id=JsonValueReader.ReadString(value,"id",string.Empty); if(string.IsNullOrWhiteSpace(id))continue;
            var kind=JsonValueReader.ReadString(value,"gate_kind","open"); var room=ReadRelationship(value,"from_room_id","from_room"); var direction=JsonValueReader.ReadString(value,"direction",string.Empty);
            var qualifier=JsonValueReader.ReadString(value,"qualifier",string.Empty); var disposition=JsonValueReader.ReadString(value,"runtime_disposition",string.Empty);
            string? item=null; long toll=0; int damage=0,spell=0,pre=0,post=0;
            if(value.TryGetProperty("prerequisites",out var prerequisites)&&prerequisites.ValueKind==JsonValueKind.Object){item=ReadScalarString(prerequisites,"required_item_id");toll=ReadLong(prerequisites,"toll_amount");}
            if(value.TryGetProperty("effects",out var effects)&&effects.ValueKind==JsonValueKind.Object){damage=(int)ReadLong(effects,"damage");spell=(int)ReadLong(effects,"spell_id");pre=(int)ReadLong(effects,"pre_spell_id");post=(int)ReadLong(effects,"post_spell_id");}
            _gates[id]=new(id,room,direction,kind,qualifier,item,toll,damage,spell,pre,post,ReadStringArray(value,"commands"),disposition);
        }
    }

    private static string? ReadScalarString(JsonElement element,string name)=>element.TryGetProperty(name,out var value)?value.ValueKind switch{JsonValueKind.String=>value.GetString(),JsonValueKind.Number=>value.GetRawText(),_=>null}:null;
    private static long ReadLong(JsonElement element,string name)=>element.TryGetProperty(name,out var value)?value.ValueKind switch{JsonValueKind.Number when value.TryGetInt64(out var number)=>number,JsonValueKind.String when long.TryParse(value.GetString(),out var textNumber)=>textNumber,_=>0}:0;
    private string CompletionPersistencePath=>Path.Combine(_persistenceRoot,WorldId,"living-world-completion.json");
    private void SaveLivingWorldCompletionLocked(){var path=CompletionPersistencePath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);var snapshot=new RuntimeLivingWorldCompletionSnapshot(WorldId,_eventSequence,_npcAi.Values.ToArray(),_corpses.Values.ToArray(),_recentEvents.ToArray(),DateTimeOffset.UtcNow);var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(snapshot,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,path,true);}
    private void RestoreLivingWorldCompletion(){lock(_gate){if(!File.Exists(CompletionPersistencePath))return;try{var x=JsonSerializer.Deserialize<RuntimeLivingWorldCompletionSnapshot>(File.ReadAllText(CompletionPersistencePath));if(x is null||!x.WorldId.Equals(WorldId,StringComparison.OrdinalIgnoreCase))return;_eventSequence=x.EventSequence;foreach(var ai in x.AiStates)if(_npcAi.ContainsKey(ai.InstanceId))_npcAi[ai.InstanceId]=ai;foreach(var c in x.Corpses)if(_npcs.ContainsKey(c.NpcInstanceId))_corpses[c.CorpseId]=c;foreach(var e in x.RecentEvents.OrderBy(e=>e.Sequence))_recentEvents.Enqueue(e);}catch(JsonException){}}}
}
