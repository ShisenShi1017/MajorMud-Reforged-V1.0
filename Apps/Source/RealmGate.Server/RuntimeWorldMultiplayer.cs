using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeWorldEvent(string Kind, string ActorId, string? TargetId, string? RoomId, IReadOnlyDictionary<string,string> Metadata);
public interface IRuntimeWorldEventSink { void Apply(RuntimeWorldEvent value); void Rollback(RuntimeWorldEvent value); }
public interface IRuntimeWorldInteractionPolicy { IReadOnlyList<RuntimeWorldEvent> Resolve(RuntimeWorldInteractionRequest request, RuntimeWorldObjectState state); }
public sealed record RuntimeWorldInteractionRequest(string ActorId, string ObjectId, string Action, string RoomId, IReadOnlyDictionary<string,string> Metadata);
public sealed record RuntimeWorldObjectSnapshot(string ObjectId, string RoomId, IReadOnlyDictionary<string,string> State);

public sealed class RuntimeWorldObjectState
{
    private readonly Dictionary<string,string> _state = new(StringComparer.OrdinalIgnoreCase);
    public RuntimeWorldObjectState(string objectId, string roomId) { ObjectId=Require(objectId); RoomId=Require(roomId); }
    public string ObjectId { get; }
    public string RoomId { get; private set; }
    public IReadOnlyDictionary<string,string> State => _state;
    public void Set(string key,string value)=>_state[Require(key)]=value;
    public RuntimeWorldObjectSnapshot Capture()=>new(ObjectId,RoomId,new Dictionary<string,string>(_state,StringComparer.OrdinalIgnoreCase));
    public void Restore(RuntimeWorldObjectSnapshot s){ if(!ObjectId.Equals(s.ObjectId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("world-object-id-mismatch"); RoomId=Require(s.RoomId); _state.Clear(); foreach(var x in s.State)_state[x.Key]=x.Value; }
    private static string Require(string v)=>string.IsNullOrWhiteSpace(v)?throw new ArgumentException("value-required"):v;
}

public sealed class RuntimeWorldInteractionEngine
{
    private readonly IRuntimeWorldInteractionPolicy _policy; private readonly IRuntimeWorldEventSink _sink;
    public RuntimeWorldInteractionEngine(IRuntimeWorldInteractionPolicy policy, IRuntimeWorldEventSink sink){_policy=policy;_sink=sink;}
    public IReadOnlyList<RuntimeWorldEvent> Execute(RuntimeWorldInteractionRequest request, RuntimeWorldObjectState state)
    {
        var snapshot=state.Capture(); var applied=new List<RuntimeWorldEvent>();
        try { foreach(var e in _policy.Resolve(request,state)){_sink.Apply(e);applied.Add(e);} return applied; }
        catch { for(var i=applied.Count-1;i>=0;i--)_sink.Rollback(applied[i]); state.Restore(snapshot); throw; }
    }
}

public sealed record RuntimeMovementRequest(string ActorId,string FromRoomId,string ExitId,string? RequestedDestinationId,IReadOnlyDictionary<string,string> Metadata);
public sealed record RuntimeMovementDecision(bool Allowed,string? DestinationRoomId,IReadOnlyList<RuntimeWorldEvent> Events,string? FailureCode);
public interface IRuntimeMovementPolicy { RuntimeMovementDecision Resolve(RuntimeMovementRequest request); }
public sealed record RuntimeActorLocationSnapshot(string ActorId,string RoomId);
public sealed class RuntimeActorLocation
{
    public RuntimeActorLocation(string actorId,string roomId){ActorId=actorId;RoomId=roomId;} public string ActorId{get;} public string RoomId{get;private set;}
    public RuntimeActorLocationSnapshot Capture()=>new(ActorId,RoomId); public void Restore(RuntimeActorLocationSnapshot x)=>RoomId=x.RoomId;
    public void MoveTo(string roomId)=>RoomId=string.IsNullOrWhiteSpace(roomId)?throw new ArgumentException("room-required"):roomId;
}
public sealed class RuntimeMovementEngine
{
    private readonly IRuntimeMovementPolicy _policy; private readonly IRuntimeWorldEventSink _sink;
    public RuntimeMovementEngine(IRuntimeMovementPolicy policy,IRuntimeWorldEventSink sink){_policy=policy;_sink=sink;}
    public RuntimeMovementDecision Move(RuntimeMovementRequest request,RuntimeActorLocation location)
    {
        if(!location.ActorId.Equals(request.ActorId,StringComparison.OrdinalIgnoreCase)||!location.RoomId.Equals(request.FromRoomId,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("movement-origin-mismatch");
        var old=location.Capture(); var applied=new List<RuntimeWorldEvent>(); var d=_policy.Resolve(request); if(!d.Allowed)return d; if(string.IsNullOrWhiteSpace(d.DestinationRoomId))throw new InvalidOperationException("movement-destination-required");
        try{location.MoveTo(d.DestinationRoomId);foreach(var e in d.Events){_sink.Apply(e);applied.Add(e);}return d;}catch{for(var i=applied.Count-1;i>=0;i--)_sink.Rollback(applied[i]);location.Restore(old);throw;}
    }
}

public sealed record RuntimePartySnapshot(string PartyId,string LeaderId,IReadOnlyList<string> Members,IReadOnlyDictionary<string,string> Metadata);
public sealed class RuntimeParty
{
    private readonly HashSet<string> _members=new(StringComparer.OrdinalIgnoreCase); private readonly Dictionary<string,string> _metadata=new(StringComparer.OrdinalIgnoreCase);
    public RuntimeParty(string partyId,string leaderId){PartyId=partyId;LeaderId=leaderId;_members.Add(leaderId);} public string PartyId{get;} public string LeaderId{get;private set;} public IReadOnlyCollection<string> Members=>_members;
    public bool Add(string actorId)=>_members.Add(actorId); public bool Remove(string actorId){if(actorId.Equals(LeaderId,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("party-leader-cannot-be-removed");return _members.Remove(actorId);} public void ChangeLeader(string actorId){if(!_members.Contains(actorId))throw new InvalidOperationException("party-leader-must-be-member");LeaderId=actorId;}
    public RuntimePartySnapshot Capture()=>new(PartyId,LeaderId,_members.Order(StringComparer.OrdinalIgnoreCase).ToArray(),new Dictionary<string,string>(_metadata,StringComparer.OrdinalIgnoreCase));
    public static RuntimeParty Restore(RuntimePartySnapshot s){var p=new RuntimeParty(s.PartyId,s.LeaderId);foreach(var m in s.Members)p._members.Add(m);foreach(var x in s.Metadata)p._metadata[x.Key]=x.Value;return p;}
}
public sealed class RuntimePartyRegistry
{
    private readonly Dictionary<string,RuntimeParty> _parties=new(StringComparer.OrdinalIgnoreCase); private readonly Dictionary<string,string> _membership=new(StringComparer.OrdinalIgnoreCase);
    public void Register(RuntimeParty p){if(_parties.ContainsKey(p.PartyId))throw new InvalidOperationException("duplicate-party-id");foreach(var m in p.Members)if(_membership.ContainsKey(m))throw new InvalidOperationException("actor-already-in-party");_parties[p.PartyId]=p;foreach(var m in p.Members)_membership[m]=p.PartyId;}
    public void AddMember(string partyId,string actorId){var p=_parties[partyId];if(_membership.ContainsKey(actorId))throw new InvalidOperationException("actor-already-in-party");p.Add(actorId);_membership[actorId]=partyId;}
    public void RemoveMember(string partyId,string actorId){if(_parties[partyId].Remove(actorId))_membership.Remove(actorId);} public bool TryGet(string id,out RuntimeParty p)=>_parties.TryGetValue(id,out p!);
    public IReadOnlyList<RuntimePartySnapshot> Capture()=>_parties.Values.Select(x=>x.Capture()).ToArray();
}

public sealed record RuntimeReputationSnapshot(string ActorId,IReadOnlyDictionary<string,long> Standings);
public sealed class RuntimeReputationState
{
    private readonly Dictionary<string,long> _values=new(StringComparer.OrdinalIgnoreCase); public RuntimeReputationState(string actorId)=>ActorId=actorId; public string ActorId{get;} public long Get(string factionId)=>_values.GetValueOrDefault(factionId);
    public void Apply(string factionId,long delta)=>_values[factionId]=checked(Get(factionId)+delta); public RuntimeReputationSnapshot Capture()=>new(ActorId,new Dictionary<string,long>(_values,StringComparer.OrdinalIgnoreCase)); public void Restore(RuntimeReputationSnapshot x){_values.Clear();foreach(var v in x.Standings)_values[v.Key]=v.Value;}
}
public interface IRuntimeReputationPolicy { IReadOnlyList<RuntimeWorldEvent> Resolve(string actorId,string factionId,long oldStanding,long newStanding); }

public sealed record RuntimeMessageRequest(string SenderId,string Channel,string? TargetId,string? RoomId,string Text,IReadOnlyDictionary<string,string> Metadata);
public sealed record RuntimeMessageDelivery(string RecipientId,string Channel,string SenderId,string Text,IReadOnlyDictionary<string,string> Metadata);
public interface IRuntimeCommunicationPolicy { IReadOnlyList<RuntimeMessageDelivery> Route(RuntimeMessageRequest request); }
public interface IRuntimeMessageSink { void Deliver(RuntimeMessageDelivery delivery); void Retract(RuntimeMessageDelivery delivery); }
public sealed class RuntimeCommunicationRouter
{
    private readonly IRuntimeCommunicationPolicy _policy;private readonly IRuntimeMessageSink _sink; public RuntimeCommunicationRouter(IRuntimeCommunicationPolicy policy,IRuntimeMessageSink sink){_policy=policy;_sink=sink;}
    public IReadOnlyList<RuntimeMessageDelivery> Send(RuntimeMessageRequest request){if(string.IsNullOrWhiteSpace(request.Text))throw new InvalidOperationException("message-text-required");var a=new List<RuntimeMessageDelivery>();try{foreach(var d in _policy.Route(request)){_sink.Deliver(d);a.Add(d);}return a;}catch{for(var i=a.Count-1;i>=0;i--)_sink.Retract(a[i]);throw;}}
}

public sealed record RuntimeScheduledEventSnapshot(string EventId,DateTimeOffset DueAt,string Kind,IReadOnlyDictionary<string,string> Metadata);
public sealed class RuntimeWorldSchedulerState
{
    private readonly Dictionary<string,RuntimeScheduledEventSnapshot> _events=new(StringComparer.OrdinalIgnoreCase); public int Count=>_events.Count;
    public void Schedule(RuntimeScheduledEventSnapshot e){if(!_events.TryAdd(e.EventId,e))throw new InvalidOperationException("duplicate-scheduled-event-id");} public bool Cancel(string id)=>_events.Remove(id);
    public IReadOnlyList<RuntimeScheduledEventSnapshot> TakeDue(DateTimeOffset now){var due=_events.Values.Where(x=>x.DueAt<=now).OrderBy(x=>x.DueAt).ThenBy(x=>x.EventId,StringComparer.OrdinalIgnoreCase).ToArray();foreach(var e in due)_events.Remove(e.EventId);return due;}
    public IReadOnlyList<RuntimeScheduledEventSnapshot> Capture()=>_events.Values.OrderBy(x=>x.DueAt).ToArray(); public void Restore(IEnumerable<RuntimeScheduledEventSnapshot> events){_events.Clear();foreach(var e in events)Schedule(e);}
}

public static class RuntimeWorldMultiplayerValidator
{
    public static IReadOnlyList<string> Validate(RuntimePartyRegistry parties,RuntimeWorldSchedulerState scheduler,IEnumerable<RuntimeActorLocation> locations)
    {
        var errors=new List<string>();var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var l in locations){if(!ids.Add(l.ActorId))errors.Add("duplicate-actor-location:"+l.ActorId);if(string.IsNullOrWhiteSpace(l.RoomId))errors.Add("invalid-actor-room:"+l.ActorId);}var eventIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var e in scheduler.Capture())if(!eventIds.Add(e.EventId))errors.Add("duplicate-scheduled-event:"+e.EventId);foreach(var p in parties.Capture()){if(!p.Members.Contains(p.LeaderId,StringComparer.OrdinalIgnoreCase))errors.Add("party-leader-not-member:"+p.PartyId);if(p.Members.Count!=p.Members.Distinct(StringComparer.OrdinalIgnoreCase).Count())errors.Add("duplicate-party-member:"+p.PartyId);}return errors;
    }
}

public static class RuntimeWorldMultiplayerPersistence
{
    private static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    public static string Save(RuntimePartyRegistry parties,RuntimeWorldSchedulerState scheduler,IEnumerable<RuntimeActorLocation> locations,IEnumerable<RuntimeReputationState> reputations)=>JsonSerializer.Serialize(new RuntimeWorldMultiplayerSave(parties.Capture(),scheduler.Capture(),locations.Select(x=>x.Capture()).ToArray(),reputations.Select(x=>x.Capture()).ToArray()),Options);
    public static RuntimeWorldMultiplayerSave Load(string json)=>JsonSerializer.Deserialize<RuntimeWorldMultiplayerSave>(json,Options)??throw new InvalidOperationException("world-multiplayer-save-invalid");
}
public sealed record RuntimeWorldMultiplayerSave(IReadOnlyList<RuntimePartySnapshot> Parties,IReadOnlyList<RuntimeScheduledEventSnapshot> ScheduledEvents,IReadOnlyList<RuntimeActorLocationSnapshot> Locations,IReadOnlyList<RuntimeReputationSnapshot> Reputations);
