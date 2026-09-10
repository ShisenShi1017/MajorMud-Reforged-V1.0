using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeAbilityDefinition(string Id, string Name, JsonElement GeneratedRecord);

public sealed class RuntimeAbilityCatalog
{
    private readonly Dictionary<string, RuntimeAbilityDefinition> _definitions;
    private RuntimeAbilityCatalog(IEnumerable<RuntimeAbilityDefinition> definitions) =>
        _definitions = definitions.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    public int Count => _definitions.Count;
    public IReadOnlyCollection<RuntimeAbilityDefinition> Definitions => _definitions.Values;
    public bool TryGet(string id, out RuntimeAbilityDefinition definition) => _definitions.TryGetValue(id, out definition!);

    public static RuntimeAbilityCatalog Load(string projectRoot, string worldId)
    {
        var path = Path.Combine(ServerDataRootResolver.Resolve(projectRoot, worldId).DataRoot, "abilities.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var records = document.RootElement.GetProperty("records");
        return new RuntimeAbilityCatalog(records.EnumerateArray().Select(x => new RuntimeAbilityDefinition(
            x.GetProperty("id").GetString() ?? string.Empty,
            x.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            x.Clone())));
    }
}

public sealed record RuntimeKnownAbility(string AbilityId, DateTimeOffset LearnedAtUtc, IReadOnlyDictionary<string,string> Metadata);
public sealed record RuntimeAbilityCooldown(string AbilityId, DateTimeOffset ReadyAtUtc);
public sealed record RuntimeActorAbilitySnapshot(string ActorId, IReadOnlyList<RuntimeKnownAbility> Known, IReadOnlyList<RuntimeAbilityCooldown> Cooldowns, IReadOnlyDictionary<string,string> Metadata);

public sealed class RuntimeActorAbilityState
{
    private readonly object _gate = new();
    private readonly Dictionary<string,RuntimeKnownAbility> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeAbilityCooldown> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,string> _metadata = new(StringComparer.OrdinalIgnoreCase);
    public RuntimeActorAbilityState(string actorId) => ActorId = string.IsNullOrWhiteSpace(actorId) ? throw new ArgumentException("actor-id-required") : actorId;
    public string ActorId { get; }
    public bool Learn(string abilityId, DateTimeOffset nowUtc, IReadOnlyDictionary<string,string>? metadata = null) { lock (_gate) { if (_known.ContainsKey(abilityId)) return false; _known.Add(abilityId,new(abilityId,nowUtc,metadata ?? new Dictionary<string,string>())); return true; } }
    public bool Forget(string abilityId) { lock (_gate) { _cooldowns.Remove(abilityId); return _known.Remove(abilityId); } }
    public bool Knows(string abilityId) { lock (_gate) return _known.ContainsKey(abilityId); }
    public bool IsReady(string abilityId, DateTimeOffset nowUtc) { lock (_gate) return !_cooldowns.TryGetValue(abilityId,out var c) || c.ReadyAtUtc <= nowUtc; }
    public void SetCooldown(string abilityId, DateTimeOffset readyAtUtc) { lock (_gate) _cooldowns[abilityId] = new(abilityId,readyAtUtc); }
    public void ClearExpiredCooldowns(DateTimeOffset nowUtc) { lock (_gate) foreach (var id in _cooldowns.Where(x=>x.Value.ReadyAtUtc<=nowUtc).Select(x=>x.Key).ToArray()) _cooldowns.Remove(id); }
    public RuntimeActorAbilitySnapshot Capture() { lock (_gate) return new(ActorId,_known.Values.ToArray(),_cooldowns.Values.ToArray(),new Dictionary<string,string>(_metadata,StringComparer.OrdinalIgnoreCase)); }
    public void Restore(RuntimeActorAbilitySnapshot s) { if (!ActorId.Equals(s.ActorId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("actor-mismatch"); lock (_gate) { _known.Clear(); foreach(var x in s.Known)_known.Add(x.AbilityId,x); _cooldowns.Clear(); foreach(var x in s.Cooldowns)_cooldowns.Add(x.AbilityId,x); _metadata.Clear(); foreach(var x in s.Metadata)_metadata[x.Key]=x.Value; } }
    public IReadOnlyList<string> Validate(RuntimeAbilityCatalog catalog) { lock (_gate) { var e=new List<string>(); if (_known.Keys.Any(x=>!catalog.TryGet(x,out _)))e.Add("unknown-known-ability"); if (_cooldowns.Keys.Any(x=>!_known.ContainsKey(x)))e.Add("cooldown-for-unknown-ability"); return e; } }
}

public sealed record RuntimeAbilityRequest(string RequestId,string ActorId,string AbilityId,string? TargetId,IReadOnlyDictionary<string,string>? Arguments=null);
public sealed record RuntimeAbilityEvent(string EventId,string EventType,string ActorId,string AbilityId,string? TargetId,long Magnitude,DateTimeOffset OccurredAtUtc,IReadOnlyDictionary<string,string>? Metadata=null);
public sealed record RuntimeAbilityPlan(bool Allowed,string? FailureCode,DateTimeOffset? CooldownReadyAtUtc,IReadOnlyList<RuntimeAbilityEvent> Events);
public sealed record RuntimeAbilityResult(bool Succeeded,string? FailureCode,IReadOnlyList<RuntimeAbilityEvent> Events);
public interface IRuntimeAbilityPolicy { RuntimeAbilityPlan Resolve(RuntimeAbilityDefinition definition, RuntimeActorAbilitySnapshot actor, RuntimeAbilityRequest request, DateTimeOffset nowUtc); }
public interface IRuntimeAbilityEventSink { void Apply(RuntimeAbilityEvent abilityEvent); void Revert(RuntimeAbilityEvent abilityEvent); }
public interface IRuntimeTrainerPolicy { RuntimeAbilityPlan ResolveTraining(RuntimeAbilityDefinition definition,string trainerInstanceId,RuntimeActorAbilitySnapshot student,DateTimeOffset nowUtc); }

public sealed class RuntimeAbilityEngine
{
    private readonly RuntimeAbilityCatalog _catalog; private readonly IRuntimeAbilityPolicy _policy; private readonly IRuntimeAbilityEventSink _sink;
    public RuntimeAbilityEngine(RuntimeAbilityCatalog catalog,IRuntimeAbilityPolicy policy,IRuntimeAbilityEventSink sink){_catalog=catalog;_policy=policy;_sink=sink;}
    public RuntimeAbilityResult Execute(RuntimeActorAbilityState state,RuntimeAbilityRequest request,DateTimeOffset nowUtc)
    {
        if (!state.ActorId.Equals(request.ActorId,StringComparison.OrdinalIgnoreCase)) return new(false,"actor-mismatch",Array.Empty<RuntimeAbilityEvent>());
        if (!_catalog.TryGet(request.AbilityId,out var definition)) return new(false,"unknown-ability",Array.Empty<RuntimeAbilityEvent>());
        if (!state.Knows(request.AbilityId)) return new(false,"ability-not-known",Array.Empty<RuntimeAbilityEvent>());
        if (!state.IsReady(request.AbilityId,nowUtc)) return new(false,"ability-on-cooldown",Array.Empty<RuntimeAbilityEvent>());
        var before=state.Capture(); var applied=new List<RuntimeAbilityEvent>();
        try { var plan=_policy.Resolve(definition,before,request,nowUtc); if(!plan.Allowed)return new(false,plan.FailureCode,Array.Empty<RuntimeAbilityEvent>()); foreach(var e in plan.Events){_sink.Apply(e);applied.Add(e);} if(plan.CooldownReadyAtUtc is not null)state.SetCooldown(request.AbilityId,plan.CooldownReadyAtUtc.Value); return new(true,null,applied); }
        catch { for(int i=applied.Count-1;i>=0;i--)try{_sink.Revert(applied[i]);}catch{} state.Restore(before); throw; }
    }
}

public enum RuntimeNpcLifeState { Spawned, Dead, Despawned, WaitingToRespawn }
public sealed record RuntimeNpcInstanceSnapshot(string InstanceId,string CanonicalNpcId,string RoomId,RuntimeNpcLifeState State,int CurrentHitPoints,int CurrentEnergy,DateTimeOffset? RespawnAtUtc,IReadOnlyDictionary<string,string> Metadata);
public interface IRuntimeNpcLifecyclePolicy { DateTimeOffset? ResolveRespawn(RuntimeNpcDefinition definition,RuntimeNpcInstanceSnapshot instance,DateTimeOffset nowUtc); string? ResolveSpawnRoom(RuntimeNpcDefinition definition,RuntimeNpcInstanceSnapshot instance,DateTimeOffset nowUtc); }
public sealed class RuntimeNpcInstance
{
    private readonly object _gate=new(); private readonly Dictionary<string,string> _metadata=new(StringComparer.OrdinalIgnoreCase);
    public RuntimeNpcInstance(string instanceId,RuntimeNpcDefinition definition,string roomId){InstanceId=instanceId;Definition=definition;RoomId=roomId;CurrentHitPoints=definition.HitPoints;CurrentEnergy=0;State=RuntimeNpcLifeState.Spawned;}
    public string InstanceId{get;} public RuntimeNpcDefinition Definition{get;} public string RoomId{get;private set;} public RuntimeNpcLifeState State{get;private set;} public int CurrentHitPoints{get;private set;} public int CurrentEnergy{get;private set;} public DateTimeOffset? RespawnAtUtc{get;private set;}
    public RuntimeNpcInstanceSnapshot Capture(){lock(_gate)return new(InstanceId,Definition.Id,RoomId,State,CurrentHitPoints,CurrentEnergy,RespawnAtUtc,new Dictionary<string,string>(_metadata,StringComparer.OrdinalIgnoreCase));}
    public void Restore(RuntimeNpcInstanceSnapshot s){if(!InstanceId.Equals(s.InstanceId,StringComparison.OrdinalIgnoreCase)||!Definition.Id.Equals(s.CanonicalNpcId,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("npc-snapshot-mismatch"); lock(_gate){RoomId=s.RoomId;State=s.State;CurrentHitPoints=s.CurrentHitPoints;CurrentEnergy=s.CurrentEnergy;RespawnAtUtc=s.RespawnAtUtc;_metadata.Clear();foreach(var x in s.Metadata)_metadata[x.Key]=x.Value;}}
    public void ApplyDamage(int value,IRuntimeNpcLifecyclePolicy policy,DateTimeOffset nowUtc){if(value<0)throw new ArgumentOutOfRangeException(nameof(value));lock(_gate){if(State!=RuntimeNpcLifeState.Spawned)return;CurrentHitPoints=Math.Max(0,CurrentHitPoints-value);if(CurrentHitPoints==0){State=RuntimeNpcLifeState.Dead;RespawnAtUtc=policy.ResolveRespawn(Definition,Capture(),nowUtc);if(RespawnAtUtc is not null)State=RuntimeNpcLifeState.WaitingToRespawn;}}}
    public bool Tick(IRuntimeNpcLifecyclePolicy policy,DateTimeOffset nowUtc){lock(_gate){if(State!=RuntimeNpcLifeState.WaitingToRespawn||RespawnAtUtc is null||RespawnAtUtc>nowUtc)return false;var room=policy.ResolveSpawnRoom(Definition,Capture(),nowUtc);if(string.IsNullOrWhiteSpace(room))return false;RoomId=room;CurrentHitPoints=Definition.HitPoints;CurrentEnergy=0;RespawnAtUtc=null;State=RuntimeNpcLifeState.Spawned;return true;}}
    public IReadOnlyList<string> Validate(){lock(_gate){var e=new List<string>();if(CurrentHitPoints<0||CurrentHitPoints>Definition.HitPoints)e.Add("invalid-npc-hp");if(CurrentEnergy<0)e.Add("invalid-npc-energy");if(State==RuntimeNpcLifeState.WaitingToRespawn&&RespawnAtUtc is null)e.Add("missing-respawn-time");return e;}}
}

public sealed class RuntimeNpcRegistry
{
    private readonly object _gate=new(); private readonly Dictionary<string,RuntimeNpcInstance> _instances=new(StringComparer.OrdinalIgnoreCase);
    public bool Add(RuntimeNpcInstance instance){lock(_gate){if(_instances.ContainsKey(instance.InstanceId))return false;_instances.Add(instance.InstanceId,instance);return true;}}
    public bool Remove(string id){lock(_gate)return _instances.Remove(id);} public bool TryGet(string id,out RuntimeNpcInstance instance){lock(_gate)return _instances.TryGetValue(id,out instance!);} public IReadOnlyList<RuntimeNpcInstanceSnapshot> Capture(){lock(_gate)return _instances.Values.Select(x=>x.Capture()).ToArray();}
    public IReadOnlyList<string> Validate(){lock(_gate){var e=new List<string>();if(_instances.Values.GroupBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1))e.Add("duplicate-npc-instance");e.AddRange(_instances.Values.SelectMany(x=>x.Validate()));return e;}}
}
