using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeNpcAiState(string InstanceId,string Mode,string? TargetActorId,string HomeRoomId,string CurrentRoomId,int LeashSteps,DateTimeOffset UpdatedAtUtc);
public sealed record RuntimeLivingCorpseState(string CorpseId,string NpcInstanceId,string RoomId,DateTimeOffset CreatedAtUtc,DateTimeOffset ExpiresAtUtc);
public sealed record RuntimeLivingWorldEvent(long Sequence,string Kind,string EntityId,string RoomId,DateTimeOffset OccurredAtUtc,IReadOnlyDictionary<string,string> Metadata);
public sealed record RuntimeExitGateDefinition(string ExitId,string RoomId,string Direction,string GateKind,string Qualifier,string? RequiredItemId,long TollAmount,int Damage,int SpellId,int PreSpellId,int PostSpellId,IReadOnlyList<string> Commands,string RuntimeDisposition)
{
    public string? ReciprocalExitId { get; init; }
    public string StateIdentity { get; init; } = string.Empty;
    public string OwnerScope { get; init; } = string.Empty;
    public bool ReciprocalStateLink { get; init; }
    public bool SaveWorldStateRisk { get; init; }
    public int RequiredActionCount { get; init; }
    public IReadOnlyList<string> RequiredActionOrder { get; init; } = Array.Empty<string>();
    public int? AllowedClassId { get; init; }
    public int? DeniedClassId { get; init; }
    public int? AllowedRaceId { get; init; }
    public int? DeniedRaceId { get; init; }
    public string MinimumAlignmentName { get; init; } = string.Empty;
    public string MaximumAlignmentName { get; init; } = string.Empty;
    public int? AbilityId { get; init; }
    public long? MinimumValue { get; init; }
    public long? MaximumValue { get; init; }
    public int? PicklocksDifficulty { get; init; }
    public int? PicklocksOrStrength { get; init; }
    public bool AnyPicklocks { get; init; }
    public bool AnyPicklocksOrStrength { get; init; }
    public int? StrengthAlternative { get; init; }
    public double TimeMultiplier { get; init; } = 1d;
    public bool AutomaticRouting { get; init; }
    public string FallbackBehavior { get; init; } = string.Empty;
}

public sealed record RuntimeExitGateContext(IReadOnlySet<string> ItemIds,long AvailableGold,IReadOnlySet<string> DiscoveredExitIds,string? EnteredCommand)
{
    public int? ClassId { get; init; }
    public int? RaceId { get; init; }
    public string AlignmentName { get; init; } = string.Empty;
    public IReadOnlyDictionary<int,long> AbilityValues { get; init; } = new Dictionary<int,long>();
    public int PicklocksValue { get; init; }
    public int StrengthValue { get; init; }
    public IReadOnlyList<string> CompletedActions { get; init; } = Array.Empty<string>();
}

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

    public void RecordQuestRemoteAction(string remoteActionId,string roomId,string source,IReadOnlyDictionary<string,string>? metadata=null)
    {
        if(string.IsNullOrWhiteSpace(remoteActionId))throw new ArgumentException("remote-action-id-required",nameof(remoteActionId));
        if(string.IsNullOrWhiteSpace(roomId))throw new ArgumentException("room-id-required",nameof(roomId));
        lock(_gate)
        {
            if(_world.FindRoomById(roomId) is null)throw new InvalidOperationException($"remote-action room {roomId} is not generated");
            var eventMetadata=new Dictionary<string,string>(metadata??new Dictionary<string,string>(),StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = string.IsNullOrWhiteSpace(source) ? "quest-dialogue" : source,
                ["native_behavior"] = "evidence_gated_not_mutated"
            };
            PublishLivingWorldEventLocked("quest-remote-action",remoteActionId.Trim(),roomId,DateTimeOffset.UtcNow,eventMetadata);
            SaveLivingWorldCompletionLocked();
        }
    }

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
            var exit=_world.FindExit(room,normalized);
            if(exit is null)return new(false,false,"exit-not-found",0,0,0,0,0);
            if(_world.ResolveExitDestination(exit) is null)return new(false,false,"destination-blocked",0,0,0,0,0);
            if(!_gates.TryGetValue(exit.Id,out var gate))return new(true,false,"open",0,0,0,0,0);
            if (gate.AllowedClassId is int allowedClass && context.ClassId != allowedClass) return new(false,false,"class-not-allowed",0,0,0,0,0);
            if (gate.DeniedClassId is int deniedClass && context.ClassId == deniedClass) return new(false,false,"class-denied",0,0,0,0,0);
            if (gate.AllowedRaceId is int allowedRace && context.RaceId != allowedRace) return new(false,false,"race-not-allowed",0,0,0,0,0);
            if (gate.DeniedRaceId is int deniedRace && context.RaceId == deniedRace) return new(false,false,"race-denied",0,0,0,0,0);
            if (!string.IsNullOrWhiteSpace(gate.MinimumAlignmentName) && CompareAlignment(context.AlignmentName, gate.MinimumAlignmentName) < 0) return new(false,false,"alignment-below-minimum",0,0,0,0,0);
            if (!string.IsNullOrWhiteSpace(gate.MaximumAlignmentName) && CompareAlignment(context.AlignmentName, gate.MaximumAlignmentName) > 0) return new(false,false,"alignment-above-maximum",0,0,0,0,0);
            if (gate.AbilityId is int abilityId)
            {
                context.AbilityValues.TryGetValue(abilityId, out var value);
                if (gate.MinimumValue is long minimum && value < minimum) return new(false,false,"ability-below-minimum",0,0,0,0,0);
                if (gate.MaximumValue is long maximum && value > maximum) return new(false,false,"ability-above-maximum",0,0,0,0,0);
            }
            if (gate.RequiredActionCount > 0)
            {
                if (context.CompletedActions.Count < gate.RequiredActionCount) return new(false,false,"required-actions-incomplete",0,0,0,0,0);
                for (var i=0;i<gate.RequiredActionOrder.Count;i++) if (i>=context.CompletedActions.Count || !gate.RequiredActionOrder[i].Equals(context.CompletedActions[i],StringComparison.OrdinalIgnoreCase)) return new(false,false,"required-action-order",0,0,0,0,0);
            }
            if (gate.PicklocksDifficulty is int difficulty && context.PicklocksValue < difficulty)
            {
                var strengthThreshold = gate.StrengthAlternative ?? gate.PicklocksOrStrength;
                if (strengthThreshold is null || context.StrengthValue < strengthThreshold.Value) return new(false,false,"picklocks-or-strength-failed",0,0,0,0,0);
            }
            if(!SupportedGateKinds.Contains(gate.GateKind))return new(false,true,"unsupported-evidence-only",0,0,0,0,0);
            switch(gate.GateKind.ToLowerInvariant())
            {
                case "open": return new(true,false,"open",0,0,0,0,0);
                case "door":
                    if(!_objects.TryGetValue(gate.ExitId,out var door))return new(false,true,"door-state-unavailable",0,0,0,0,0);
                    return door.Locked?new(false,false,"door-locked",0,0,0,0,0):!door.Open?new(false,false,"door-closed",0,0,0,0,0):new(true,false,"door-open",0,0,0,0,0);
                case "hidden": return gate.Qualifier.Contains("Passable",StringComparison.OrdinalIgnoreCase)||context.DiscoveredExitIds.Contains(gate.ExitId)?new(true,false,gate.Qualifier.Contains("Passable",StringComparison.OrdinalIgnoreCase)?"hidden-passable":"hidden-discovered",0,0,0,0,0):new(false,false,"hidden-undiscovered",0,0,0,0,0);
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
            JsonElement prerequisites=default;
            if(value.TryGetProperty("prerequisites",out prerequisites)&&prerequisites.ValueKind==JsonValueKind.Object){item=ReadScalarString(prerequisites,"required_item_id");toll=ReadLong(prerequisites,"toll_amount");}
            if(value.TryGetProperty("effects",out var effects)&&effects.ValueKind==JsonValueKind.Object){damage=(int)ReadLong(effects,"damage");spell=(int)ReadLong(effects,"spell_id");pre=(int)ReadLong(effects,"pre_spell_id");post=(int)ReadLong(effects,"post_spell_id");}
            JsonElement edge=default;
            value.TryGetProperty("edge_state_semantics",out edge);
            var gate=new RuntimeExitGateDefinition(id,room,direction,kind,qualifier,item,toll,damage,spell,pre,post,ReadStringArray(value,"commands"),disposition)
            {
                ReciprocalExitId = edge.ValueKind==JsonValueKind.Object ? ReadScalarString(edge,"reciprocal_exit_id") : null,
                StateIdentity = edge.ValueKind==JsonValueKind.Object ? ReadScalarString(edge,"state_identity") ?? string.Empty : string.Empty,
                OwnerScope = edge.ValueKind==JsonValueKind.Object ? ReadScalarString(edge,"owner_scope") ?? string.Empty : string.Empty,
                ReciprocalStateLink = edge.ValueKind==JsonValueKind.Object && ReadBool(edge,"reciprocal_state_link"),
                SaveWorldStateRisk = edge.ValueKind==JsonValueKind.Object && ReadBool(edge,"save_world_state_risk"),
                RequiredActionCount = prerequisites.ValueKind==JsonValueKind.Object ? (int)ReadLong(prerequisites,"required_action_count") : 0,
                RequiredActionOrder = prerequisites.ValueKind==JsonValueKind.Object ? ReadStringArray(prerequisites,"action_order") : Array.Empty<string>(),
                AllowedClassId = ReadNullableInt(prerequisites,"allowed_class_id"), DeniedClassId = ReadNullableInt(prerequisites,"denied_class_id"),
                AllowedRaceId = ReadNullableInt(prerequisites,"allowed_race_id"), DeniedRaceId = ReadNullableInt(prerequisites,"denied_race_id"),
                MinimumAlignmentName = ReadScalarString(prerequisites,"minimum_alignment_name") ?? string.Empty,
                MaximumAlignmentName = ReadScalarString(prerequisites,"maximum_alignment_name") ?? string.Empty,
                AbilityId = ReadNullableInt(prerequisites,"ability_id"), MinimumValue = ReadNullableLong(prerequisites,"minimum_value"), MaximumValue = ReadNullableLong(prerequisites,"maximum_value"),
                PicklocksDifficulty = ReadNullableInt(prerequisites,"picklocks_difficulty"), PicklocksOrStrength = ReadNullableInt(prerequisites,"picklocks_or_strength"),
                AnyPicklocks = ReadBool(prerequisites,"any_picklocks"), AnyPicklocksOrStrength = ReadBool(prerequisites,"any_picklocks_or_strength"), StrengthAlternative = ReadNullableInt(prerequisites,"strength_alternative"),
                TimeMultiplier = ReadDouble(prerequisites,"time_multiplier",1d), AutomaticRouting = edge.ValueKind==JsonValueKind.Object && ReadBool(edge,"automatic_routing"),
                FallbackBehavior = JsonValueReader.ReadString(value,"fallback_behavior",string.Empty)
            };
            _gates[id]=gate;
        }
    }

    private static bool ReadBool(JsonElement element,string name)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(name,out var value)&&value.ValueKind switch{JsonValueKind.True=>true,JsonValueKind.False=>false,JsonValueKind.Number when value.TryGetInt32(out var n)=>n!=0,JsonValueKind.String when bool.TryParse(value.GetString(),out var b)=>b,JsonValueKind.String when int.TryParse(value.GetString(),out var n)=>n!=0,_=>false};
    private static int? ReadNullableInt(JsonElement element,string name){if(element.ValueKind!=JsonValueKind.Object||!element.TryGetProperty(name,out var value)||value.ValueKind==JsonValueKind.Null)return null;return value.ValueKind switch{JsonValueKind.Number when value.TryGetInt32(out var n)=>n,JsonValueKind.String when int.TryParse(value.GetString(),out var n)=>n,_=>null};}
    private static long? ReadNullableLong(JsonElement element,string name){if(element.ValueKind!=JsonValueKind.Object||!element.TryGetProperty(name,out var value)||value.ValueKind==JsonValueKind.Null)return null;return value.ValueKind switch{JsonValueKind.Number when value.TryGetInt64(out var n)=>n,JsonValueKind.String when long.TryParse(value.GetString(),out var n)=>n,_=>null};}
    private static double ReadDouble(JsonElement element,string name,double fallback)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(name,out var value)?value.ValueKind switch{JsonValueKind.Number when value.TryGetDouble(out var n)=>n,JsonValueKind.String when double.TryParse(value.GetString(),out var n)=>n,_=>fallback}:fallback;
    private static int CompareAlignment(string actual,string required){static int Rank(string value)=>value.Trim().ToLowerInvariant() switch{"evil" or "fiend" or "villain"=>-1,"neutral"=>0,"good" or "saint"=>1,_=>0};return Rank(actual).CompareTo(Rank(required));}
    private static string? ReadScalarString(JsonElement element,string name)=>element.TryGetProperty(name,out var value)?value.ValueKind switch{JsonValueKind.String=>value.GetString(),JsonValueKind.Number=>value.GetRawText(),_=>null}:null;
    private static long ReadLong(JsonElement element,string name)=>element.TryGetProperty(name,out var value)?value.ValueKind switch{JsonValueKind.Number when value.TryGetInt64(out var number)=>number,JsonValueKind.String when long.TryParse(value.GetString(),out var textNumber)=>textNumber,_=>0}:0;
    private string CompletionPersistencePath=>Path.Combine(_persistenceRoot,WorldId,"living-world-completion.json");
    private void SaveLivingWorldCompletionLocked(){var path=CompletionPersistencePath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);var snapshot=new RuntimeLivingWorldCompletionSnapshot(WorldId,_eventSequence,_npcAi.Values.ToArray(),_corpses.Values.ToArray(),_recentEvents.ToArray(),DateTimeOffset.UtcNow);var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(snapshot,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,path,true);}
    private void RestoreLivingWorldCompletion(){lock(_gate){if(!File.Exists(CompletionPersistencePath))return;try{var x=JsonSerializer.Deserialize<RuntimeLivingWorldCompletionSnapshot>(File.ReadAllText(CompletionPersistencePath));if(x is null||!x.WorldId.Equals(WorldId,StringComparison.OrdinalIgnoreCase))return;_eventSequence=x.EventSequence;foreach(var ai in x.AiStates)if(_npcAi.ContainsKey(ai.InstanceId))_npcAi[ai.InstanceId]=ai;foreach(var c in x.Corpses)if(_npcs.ContainsKey(c.NpcInstanceId))_corpses[c.CorpseId]=c;foreach(var e in x.RecentEvents.OrderBy(e=>e.Sequence))_recentEvents.Enqueue(e);}catch(JsonException){}}}
}
