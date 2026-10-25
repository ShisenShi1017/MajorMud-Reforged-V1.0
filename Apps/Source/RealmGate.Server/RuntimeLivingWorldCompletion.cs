using System.Globalization;
using System.Text.Json;

namespace RealmGate.Server;

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
    public IReadOnlyList<string> BoundActionIds { get; init; } = Array.Empty<string>();
    public bool RequiresSpecificActionOrder { get; init; }
    public bool NativePassiveEffect { get; init; }
    public string NativePassiveEffectClassification { get; init; } = string.Empty;
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
    public bool StrengthAlternative { get; init; }
    public int? MinimumLevel { get; init; }
    public int? MaximumLevel { get; init; }
    public double TimeMultiplier { get; init; } = 1d;
    public bool AutomaticRouting { get; init; }
    public string FallbackBehavior { get; init; } = string.Empty;
}

public sealed record RuntimeExitGateContext(IReadOnlySet<string> ItemIds,long AvailableGold,IReadOnlySet<string> DiscoveredExitIds,string? EnteredCommand)
{
    public int Level { get; init; }
    public int? ClassId { get; init; }
    public int? RaceId { get; init; }
    public string AlignmentName { get; init; } = string.Empty;
    public IReadOnlyDictionary<int,long> AbilityValues { get; init; } = new Dictionary<int,long>();
    public int PicklocksValue { get; init; }
    public int StrengthValue { get; init; }
    public IReadOnlyList<string> CompletedActions { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeExitGateDecision(bool Allowed,bool EvidenceOnly,string Code,long TollCharged,int Damage,int SpellId,int PreSpellId,int PostSpellId);
public sealed record RuntimeClassicExitActionEvidence(
    string ExitId, string Direction, string GateKind, bool Open, bool Locked,
    int? PicklocksDifficulty, int? PicklocksOrStrength, bool AnyPicklocks,
    bool AnyPicklocksOrStrength, bool StrengthAlternative)
{
    public bool AllowsPicklock => PicklocksDifficulty.HasValue || PicklocksOrStrength.HasValue || AnyPicklocks || AnyPicklocksOrStrength;
    public bool AllowsBash => PicklocksOrStrength.HasValue || AnyPicklocksOrStrength || StrengthAlternative;

    public int? RequirementFor(string action)
    {
        if (action.Equals("picklock", StringComparison.OrdinalIgnoreCase))
            return PicklocksOrStrength ?? PicklocksDifficulty;
        if (action.Equals("bash", StringComparison.OrdinalIgnoreCase))
            return PicklocksOrStrength ?? (StrengthAlternative ? PicklocksDifficulty : null);
        return null;
    }

    public bool IsAnyRequirementFor(string action)
    {
        if (action.Equals("picklock", StringComparison.OrdinalIgnoreCase))
            return AnyPicklocksOrStrength || AnyPicklocks;
        if (action.Equals("bash", StringComparison.OrdinalIgnoreCase))
            return AnyPicklocksOrStrength;
        return false;
    }
}
public sealed record RuntimeLivingWorldCompletionSnapshot(string WorldId,long EventSequence,IReadOnlyList<RuntimeNpcAiState> AiStates,IReadOnlyList<RuntimeLivingCorpseState> Corpses,IReadOnlyList<RuntimeLivingWorldEvent> RecentEvents,IReadOnlyDictionary<string,int>? NativeActionStates,DateTimeOffset SavedAtUtc)
{
    public IReadOnlyDictionary<string,DateTimeOffset>? NativeActionResetDeadlines { get; init; }
}
public sealed record RuntimeRemoteActionMutationDefinition(string Signature,string SourceExitId,string ReciprocalExitId,string StateEffect,string NativeStateKey,int InitialNativeState,int Selector);

public sealed partial class RuntimeLivingWorldService
{
    private readonly Dictionary<string,RuntimeNpcAiState> _npcAi = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeLivingCorpseState> _corpses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeExitGateDefinition> _gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,RuntimeRemoteActionMutationDefinition> _remoteActionMutations = new(StringComparer.Ordinal);
    private readonly Dictionary<string,int> _nativeActionStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,DateTimeOffset> _nativeActionResetDeadlines = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<RuntimeLivingWorldEvent> _recentEvents = new();
    private long _eventSequence;
    private static readonly HashSet<string> SupportedGateKinds = new(new[]{"door","key","hidden","required_item","ticket_item","cast_transition","spell_trap","damage_trap","text_command","toll","level_range","qualified_unknown","open"},StringComparer.OrdinalIgnoreCase);

    public int CorpseCount { get { lock(_gate) return _corpses.Count; } }
    public int GateCount { get { lock(_gate) return _gates.Count; } }
    public IReadOnlyList<RuntimeNpcAiState> CaptureNpcAi(){lock(_gate)return _npcAi.Values.OrderBy(x=>x.InstanceId,StringComparer.OrdinalIgnoreCase).ToArray();}
    public IReadOnlyList<RuntimeExitGateDefinition> CaptureGates(){lock(_gate)return _gates.Values.OrderBy(x=>x.ExitId,StringComparer.OrdinalIgnoreCase).ToArray();}
    public bool TryGetClassicExitActionEvidence(RuntimeRoom room,string direction,out RuntimeClassicExitActionEvidence evidence)
    {
        var normalized=NormalizeDirection(direction);
        lock(_gate)
        {
            var exit=_world.FindExit(room,normalized);
            if(exit is null||!_gates.TryGetValue(exit.Id,out var gate))
            {
                evidence=null!;
                return false;
            }
            if(!gate.GateKind.Equals("door",StringComparison.OrdinalIgnoreCase)
                &&!gate.GateKind.Equals("key",StringComparison.OrdinalIgnoreCase)
                &&!gate.GateKind.Equals("damage_trap",StringComparison.OrdinalIgnoreCase)
                &&!gate.GateKind.Equals("spell_trap",StringComparison.OrdinalIgnoreCase))
            {
                evidence=null!;
                return false;
            }
            var open=false; var locked=false;
            if(_objects.TryGetValue(gate.ExitId,out var state)){open=state.Open;locked=state.Locked;}
            evidence=new RuntimeClassicExitActionEvidence(
                gate.ExitId,normalized,gate.GateKind,open,locked,gate.PicklocksDifficulty,
                gate.PicklocksOrStrength,gate.AnyPicklocks,gate.AnyPicklocksOrStrength,gate.StrengthAlternative);
            return true;
        }
    }
    public IReadOnlyList<RuntimeLivingWorldEvent> EventsAfter(long sequence){lock(_gate)return _recentEvents.Where(x=>x.Sequence>sequence).OrderBy(x=>x.Sequence).ToArray();}
    public IReadOnlyList<RuntimeLivingCorpseState> CorpsesInRoom(string roomId){lock(_gate)return _corpses.Values.Where(x=>x.RoomId.Equals(roomId,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.CreatedAtUtc).ToArray();}

    private const string ClassicTrapStateKeyPrefix = "classic.trap-state.";
    private const string ClassicHiddenStateKeyPrefix = "classic.hidden-state.";
    private const string ClassicDoorStateKeyPrefix = "classic.door-state.";

    private RuntimeClassicRoomsWorldNativeAuthority ClassicRoomsWorldAuthority() => RuntimeClassicRoomsWorldNativeAuthority.Load(_projectRoot, WorldId);

    private int ReadClassicNativeExitState(string prefix,string exitId,int initialState)
    {
        if(string.IsNullOrWhiteSpace(exitId))throw new ArgumentException("exit-id-required",nameof(exitId));
        lock(_gate)
        {
            var key=prefix+exitId.Trim();
            if(!_nativeActionStates.TryGetValue(key,out var state))
            {
                state=initialState;
                _nativeActionStates[key]=state;
            }
            return state;
        }
    }

    private void WriteClassicNativeExitState(string prefix,string exitId,int state,DateTimeOffset? resetAtUtc)
    {
        if(string.IsNullOrWhiteSpace(exitId))throw new ArgumentException("exit-id-required",nameof(exitId));
        lock(_gate)
        {
            var key=prefix+exitId.Trim();
            _nativeActionStates[key]=state;
            if(resetAtUtc.HasValue)_nativeActionResetDeadlines[key]=resetAtUtc.Value;
            else _nativeActionResetDeadlines.Remove(key);
            SaveLivingWorldCompletionLocked();
        }
    }

    public int ReadClassicTrapState(string exitId,int initialState)
    {
        if(!RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState(initialState))
            throw new ArgumentOutOfRangeException(nameof(initialState),initialState,"Classic trap state must be in the native 0-4 range.");
        return ReadClassicNativeExitState(ClassicTrapStateKeyPrefix,exitId,initialState);
    }

    public void WriteClassicTrapState(string exitId,int state)
    {
        if(!RuntimeClassicRoomExitNativeFieldContract.IsKnownTrapState(state))
            throw new ArgumentOutOfRangeException(nameof(state),state,"Classic trap state must be in the native 0-4 range.");
        WriteClassicNativeExitState(ClassicTrapStateKeyPrefix,exitId,state,null);
    }

    public void WriteClassicTrapStateWithNativeReset(string exitId,int state,DateTimeOffset nowUtc)
    {
        if(state is not (1 or 4))throw new ArgumentOutOfRangeException(nameof(state),state,"Only native disarmed trap states 1/4 schedule reset.");
        WriteClassicNativeExitState(ClassicTrapStateKeyPrefix,exitId,state,nowUtc.AddSeconds(RuntimeClassicRoomsWorldNativeAuthority.ExitTimerQuantumSeconds));
    }

    public int ReadClassicHiddenExitState(string exitId,int initialState) => ReadClassicNativeExitState(ClassicHiddenStateKeyPrefix,exitId,initialState);

    public void WriteClassicHiddenExitStateWithNativeReset(string exitId,int state,DateTimeOffset nowUtc)
    {
        var record=ClassicRoomsWorldAuthority().Require(exitId);
        if(record.RoomType!=6)throw new InvalidOperationException($"Native hidden-state owner is not type 6: {exitId}");
        var resetAt=RuntimeClassicRoomsWorldNativeAuthority.ResetHiddenState(state,record.Para2)==state
            ? (DateTimeOffset?)null
            : nowUtc.AddSeconds(RuntimeClassicRoomsWorldNativeAuthority.ExitTimerQuantumSeconds);
        WriteClassicNativeExitState(ClassicHiddenStateKeyPrefix,exitId,state,resetAt);
    }

    public int ReadClassicDoorExitState(string exitId,int initialState) => ReadClassicNativeExitState(ClassicDoorStateKeyPrefix,exitId,initialState);

    public void WriteClassicDoorExitStateWithNativeReset(string exitId,int state,int timerCount,DateTimeOffset nowUtc)
    {
        var record=ClassicRoomsWorldAuthority().Require(exitId);
        if(record.RoomType is not (2 or 7 or 11))throw new InvalidOperationException($"Native door/key state owner is unsupported: {exitId}");
        lock(_gate)
        {
            WriteClassicDoorExitStateLocked(exitId,state,timerCount,nowUtc);
            if(_gates.TryGetValue(exitId,out var gate)&&!string.IsNullOrWhiteSpace(gate.ReciprocalExitId)
               &&ClassicRoomsWorldAuthority().TryGet(gate.ReciprocalExitId!,out var reciprocal)
               &&reciprocal.RoomType is 2 or 7 or 11)
            {
                var reciprocalTimer=reciprocal.RoomType==2?reciprocal.Para4:reciprocal.Para3;
                WriteClassicDoorExitStateLocked(reciprocal.ExitId,state,Math.Max(1,reciprocalTimer),nowUtc);
            }
            SaveLivingWorldCompletionLocked();
        }
    }

    private void WriteClassicDoorExitStateLocked(string exitId,int state,int timerCount,DateTimeOffset nowUtc)
    {
        var key=ClassicDoorStateKeyPrefix+exitId.Trim();
        _nativeActionStates[key]=state;
        _nativeActionResetDeadlines[key]=nowUtc.AddSeconds(RuntimeClassicRoomsWorldNativeAuthority.TimerSeconds(timerCount));
        SynchronizeClassicDoorObjectLocked(exitId,state,nowUtc);
    }

    public void WriteClassicDoorExitStateWithoutNewTimer(string exitId,int state,DateTimeOffset nowUtc)
    {
        var record=ClassicRoomsWorldAuthority().Require(exitId);
        if(record.RoomType is not (2 or 7 or 11))throw new InvalidOperationException($"Native door/key state owner is unsupported: {exitId}");
        lock(_gate)
        {
            WriteClassicDoorExitStateWithoutNewTimerLocked(exitId,state,nowUtc);
            if(_gates.TryGetValue(exitId,out var gate)&&!string.IsNullOrWhiteSpace(gate.ReciprocalExitId)
               &&ClassicRoomsWorldAuthority().TryGet(gate.ReciprocalExitId!,out var reciprocal)
               &&reciprocal.RoomType is 2 or 7 or 11)
                WriteClassicDoorExitStateWithoutNewTimerLocked(reciprocal.ExitId,state,nowUtc);
            SaveLivingWorldCompletionLocked();
        }
    }

    private void WriteClassicDoorExitStateWithoutNewTimerLocked(string exitId,int state,DateTimeOffset nowUtc)
    {
        var key=ClassicDoorStateKeyPrefix+exitId.Trim();
        _nativeActionStates[key]=state;
        // CLOSE/LOCK mutate the RoomRec but do not call add_exit_to_list.  Preserve an
        // already-scheduled native deadline if one exists; do not synthesize a new timer.
        SynchronizeClassicDoorObjectLocked(exitId,state,nowUtc);
    }

    public void WriteClassicActionHiddenExitState(string exitId,int state,DateTimeOffset nowUtc)
    {
        var record=ClassicRoomsWorldAuthority().Require(exitId);
        if(record.RoomType!=6)throw new InvalidOperationException($"Native action-hidden owner is not type 6: {exitId}");
        // Native common action finalization uses state 8 once all required action bits clear.
        // The background exit dispatcher only restores state-8 exits when Para2 is non-zero.
        var resetState=RuntimeClassicRoomsWorldNativeAuthority.ResetHiddenState(state,record.Para2);
        DateTimeOffset? resetAt=resetState==state?null:nowUtc.AddSeconds(RuntimeClassicRoomsWorldNativeAuthority.ExitTimerQuantumSeconds);
        WriteClassicNativeExitState(ClassicHiddenStateKeyPrefix,exitId,state,resetAt);
    }

    private void SynchronizeClassicDoorObjectLocked(string exitId,int state,DateTimeOffset nowUtc)
    {
        if(!_objects.TryGetValue(exitId,out var current))return;
        _objects[exitId]=state switch
        {
            0=>current with{Open=true,Locked=false,UpdatedAtUtc=nowUtc},
            1=>current with{Open=false,Locked=false,UpdatedAtUtc=nowUtc},
            2=>current with{Open=false,Locked=true,UpdatedAtUtc=nowUtc},
            _=>current
        };
        Save();
    }

    public void RecordQuestRemoteAction(string remoteActionId,string roomId,string source,IReadOnlyDictionary<string,string>? metadata=null)
    {
        if(string.IsNullOrWhiteSpace(remoteActionId))throw new ArgumentException("remote-action-id-required",nameof(remoteActionId));
        if(string.IsNullOrWhiteSpace(roomId))throw new ArgumentException("room-id-required",nameof(roomId));
        var intent=RuntimeNativeRemoteActionIntent.Parse(remoteActionId);
        lock(_gate)
        {
            if(_world.FindRoomById(roomId) is null)throw new InvalidOperationException($"remote-action room {roomId} is not generated");
            var mutated=false;
            var resultingNativeState=string.Empty;
            if(_remoteActionMutations.TryGetValue(intent.Signature,out var mutation))
            {
                if(mutation.StateEffect.Equals("toggle_paired_door_state",StringComparison.Ordinal))
                {
                    if(!_objects.TryGetValue(mutation.SourceExitId,out var sourceState)
                    ||!_objects.TryGetValue(mutation.ReciprocalExitId,out var reciprocalState)
                    ||!_objectDefinitions.TryGetValue(mutation.SourceExitId,out var sourceDefinition)
                    ||!_objectDefinitions.TryGetValue(mutation.ReciprocalExitId,out var reciprocalDefinition)
                    ||!sourceDefinition.Kind.Equals("door",StringComparison.OrdinalIgnoreCase)
                    ||!reciprocalDefinition.Kind.Equals("door",StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"remote-action {intent.Signature} paired generated doors are unavailable");
                    var open=!sourceState.Open;
                    var now=DateTimeOffset.UtcNow;
                    _objects[mutation.SourceExitId]=sourceState with{Open=open,Locked=!open,UpdatedAtUtc=now};
                    _objects[mutation.ReciprocalExitId]=reciprocalState with{Open=open,Locked=!open,UpdatedAtUtc=now};
                    _nativeActionStates[mutation.NativeStateKey]=open?0:2;
                    mutated=true;
                    resultingNativeState=open?"0":"2";
                    Save();
                }
                else if(mutation.StateEffect.Equals("clear_native_state_bit",StringComparison.Ordinal))
                {
                    var current=_nativeActionStates.GetValueOrDefault(mutation.NativeStateKey,mutation.InitialNativeState);
                    var mask=WorldgroupRemoteActionDispatchRegistry.ModeClearedMasks[mutation.Selector];
                    var next=current&~mask;
                    if((next&WorldgroupRemoteActionDispatchRegistry.ModeClearedMasks[0])==0)next=8;
                    _nativeActionStates[mutation.NativeStateKey]=next;
                    if(next==8&&metadata?.TryGetValue("bound_exit_id",out var boundExitId)==true
                       &&!string.IsNullOrWhiteSpace(boundExitId))
                        WriteClassicActionHiddenExitState(boundExitId,8,DateTimeOffset.UtcNow);
                    mutated=true;
                    resultingNativeState=next.ToString(CultureInfo.InvariantCulture);
                }
                else if(mutation.StateEffect.Equals("native_common_finalization",StringComparison.Ordinal))
                {
                    mutated=true;
                    resultingNativeState=_nativeActionStates.GetValueOrDefault(mutation.NativeStateKey,mutation.InitialNativeState).ToString(CultureInfo.InvariantCulture);
                }
                else throw new InvalidOperationException($"remote-action {intent.Signature} has unsupported generated state effect");
            }
            var eventMetadata=new Dictionary<string,string>(metadata??new Dictionary<string,string>(),StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = string.IsNullOrWhiteSpace(source) ? "quest-dialogue" : source,
                ["native_behavior"] = mutated ? _remoteActionMutations[intent.Signature].StateEffect : "evidence_gated_not_mutated",
                ["action_record_id"] = intent.ActionRecordId.ToString(),
                ["optional_message_id"] = intent.OptionalMessageId.ToString(),
                ["selector"] = intent.Selector.ToString(),
                ["action_slot"] = intent.ActionSlot.ToString(),
                ["dispatcher_entry"] = $"0x{WorldgroupRemoteActionDispatchRegistry.Entry:X8}"
            };
            if(mutated)
            {
                eventMetadata["resulting_native_state"]=resultingNativeState;
                if(!string.IsNullOrWhiteSpace(_remoteActionMutations[intent.Signature].SourceExitId))
                    eventMetadata["source_exit_id"]=_remoteActionMutations[intent.Signature].SourceExitId;
                if(!string.IsNullOrWhiteSpace(_remoteActionMutations[intent.Signature].ReciprocalExitId))
                    eventMetadata["reciprocal_exit_id"]=_remoteActionMutations[intent.Signature].ReciprocalExitId;
            }
            PublishLivingWorldEventLocked("quest-remote-action",intent.Signature,roomId,DateTimeOffset.UtcNow,eventMetadata);
            SaveLivingWorldCompletionLocked();
        }
    }

    private void InitializeLivingWorldCompletion()
    {
        LoadGeneratedGateDefinitions();
        LoadGeneratedRemoteActionMutations();
        foreach(var npc in _npcs.Values)
            _npcAi[npc.InstanceId]=new(npc.InstanceId,"idle",null,npc.HomeRoomId,npc.RoomId,0,DateTimeOffset.UtcNow);
    }

    private void LoadGeneratedRemoteActionMutations()
    {
        var path=Path.Combine(_projectRoot,_world.Definition.DataRoot,"room_interactions.json");
        if(!File.Exists(path))return;
        foreach(var value in RuntimeChunkedClassicCatalog.LoadRecords(path))
        {
            if(!JsonValueReader.ReadString(value,"interaction_kind",string.Empty).Equals("remote_action_mutation",StringComparison.OrdinalIgnoreCase))continue;
            var signature=JsonValueReader.ReadString(value,"remote_action_signature",string.Empty);
            var sourceExit=JsonValueReader.ReadString(value,"source_exit_id",string.Empty);
            var reciprocalExit=JsonValueReader.ReadString(value,"reciprocal_exit_id",string.Empty);
            var nativeStateKey=JsonValueReader.ReadString(value,"native_state_key",string.Empty);
            var initialNativeState=JsonValueReader.ReadInt(value,"initial_native_state",0);
            var stateEffect=string.Empty;
            if(value.TryGetProperty("command_semantics",out var semantics)&&semantics.ValueKind==JsonValueKind.Object)
                stateEffect=JsonValueReader.ReadString(semantics,"state_effect",string.Empty);
            var selector=semantics.ValueKind==JsonValueKind.Object?JsonValueReader.ReadInt(semantics,"selector",0):0;
            if(string.IsNullOrWhiteSpace(signature)||string.IsNullOrWhiteSpace(nativeStateKey)||string.IsNullOrWhiteSpace(stateEffect))
                throw new InvalidOperationException("generated remote-action mutation contract is incomplete");
            if(stateEffect.Equals("toggle_paired_door_state",StringComparison.Ordinal)
               &&(string.IsNullOrWhiteSpace(sourceExit)||string.IsNullOrWhiteSpace(reciprocalExit)))
                throw new InvalidOperationException("generated paired remote-action mutation contract is incomplete");
            if(selector<0||selector>=WorldgroupRemoteActionDispatchRegistry.ModeClearedMasks.Length)
                throw new InvalidOperationException("generated remote-action selector is outside the native mode table");
            _=RuntimeNativeRemoteActionIntent.Parse(signature);
            if(!_remoteActionMutations.TryAdd(signature,new(signature,sourceExit,reciprocalExit,stateEffect,nativeStateKey,initialNativeState,selector)))
                throw new InvalidOperationException($"duplicate generated remote-action mutation {signature}");
            _nativeActionStates.TryAdd(nativeStateKey,initialNativeState);
        }
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
            if(ClassicRoomsWorldAuthority().TryGet(exit.Id,out var nativeExit))
            {
                if(nativeExit.RoomType==6)
                {
                    var state=ReadClassicNativeExitState(ClassicHiddenStateKeyPrefix,exit.Id,nativeExit.Para1);
                    if(!RuntimeClassicRoomsWorldNativeAuthority.HiddenStateAllowsTraversal(state))
                        return new(false,false,"hidden-native-blocked",0,0,0,0,0);
                    if(gate.GateKind.Equals("hidden",StringComparison.OrdinalIgnoreCase))
                        return new(true,false,"hidden-native-open",0,0,0,0,0);
                }
                if(nativeExit.RoomType is 2 or 7 or 11 && gate.GateKind is "door" or "key")
                {
                    var initial=nativeExit.RoomType==2?nativeExit.Para2:nativeExit.Para1;
                    var state=ReadClassicNativeExitState(ClassicDoorStateKeyPrefix,exit.Id,initial);
                    if(state==0)return new(true,false,"door-open",0,0,0,0,0);
                    if(nativeExit.RoomType==2 && nativeExit.Para1>0
                       &&context.ItemIds.Any(id=>ParseClassicLegacyId(id)==nativeExit.Para1))
                        return new(true,false,"native-key-present",0,0,0,0,0);
                    if(state==2)return new(false,false,"door-locked",0,0,0,0,0);
                    if(state==1)return new(false,false,"door-closed",0,0,0,0,0);
                }
            }
            if (gate.AllowedClassId is int allowedClass && context.ClassId != allowedClass) return new(false,false,"class-not-allowed",0,0,0,0,0);
            if (gate.DeniedClassId is int deniedClass && context.ClassId == deniedClass) return new(false,false,"class-denied",0,0,0,0,0);
            if (gate.AllowedRaceId is int allowedRace && context.RaceId != allowedRace) return new(false,false,"race-not-allowed",0,0,0,0,0);
            if (gate.DeniedRaceId is int deniedRace && context.RaceId == deniedRace) return new(false,false,"race-denied",0,0,0,0,0);
            if (gate.MinimumLevel is int minimumLevel && context.Level < minimumLevel) return new(false,false,"level-below-minimum",0,0,0,0,0);
            if (gate.MaximumLevel is int maximumLevel && maximumLevel > 0 && context.Level > maximumLevel) return new(false,false,"level-above-maximum",0,0,0,0,0);
            if (!string.IsNullOrWhiteSpace(gate.MinimumAlignmentName) && CompareAlignment(context.AlignmentName, gate.MinimumAlignmentName) < 0) return new(false,false,"alignment-below-minimum",0,0,0,0,0);
            if (!string.IsNullOrWhiteSpace(gate.MaximumAlignmentName) && CompareAlignment(context.AlignmentName, gate.MaximumAlignmentName) > 0) return new(false,false,"alignment-above-maximum",0,0,0,0,0);
            if (gate.AbilityId is int abilityId)
            {
                context.AbilityValues.TryGetValue(abilityId, out var value);
                if (gate.MinimumValue is long minimum && value < minimum) return new(false,false,"ability-below-minimum",0,0,0,0,0);
                if (gate.MaximumValue is long maximum && value > maximum) return new(false,false,"ability-above-maximum",0,0,0,0,0);
            }
            var requiredActionsComplete=false;
            if (gate.RequiredActionCount > 0)
            {
                if (gate.BoundActionIds.Count < gate.RequiredActionCount)
                    return gate.NativePassiveEffect
                        ? new(false,true,"native-passive-event-pending",0,0,0,0,0)
                        : new(false,true,"required-actions-unbound-evidence-only",0,0,0,0,0);
                var completed = gate.BoundActionIds
                    .Select(id => _objects.TryGetValue(id, out var state) && state.Activated ? state : null)
                    .Where(state => state is not null)
                    .Cast<RuntimeLivingWorldObjectState>()
                    .ToArray();
                if (completed.Length < gate.RequiredActionCount)
                    return new(false,false,"required-actions-incomplete",0,0,0,0,0);
                if (gate.RequiresSpecificActionOrder)
                {
                    var actualOrder = completed.OrderBy(state => state.UpdatedAtUtc).Select(state => state.Id).ToArray();
                    var requiredOrder = gate.BoundActionIds.Take(gate.RequiredActionCount).ToArray();
                    if (!actualOrder.Take(requiredOrder.Length).SequenceEqual(requiredOrder, StringComparer.OrdinalIgnoreCase))
                        return new(false,false,"required-action-order",0,0,0,0,0);
                }
                requiredActionsComplete=true;
            }
            // Door/Gate Para2 describes the explicit PICKLOCK/BASH action boundary.
            // It is not an automatic traversal stat check: walking through an exit only
            // evaluates the door's resulting open/locked state.
            if(!SupportedGateKinds.Contains(gate.GateKind))return new(false,true,"unsupported-evidence-only",0,0,0,0,0);
            switch(gate.GateKind.ToLowerInvariant())
            {
                case "open": return new(true,false,"open",0,0,0,0,0);
                case "door":
                case "key":
                    if(!_objects.TryGetValue(gate.ExitId,out var door))return new(false,true,"door-state-unavailable",0,0,0,0,0);
                    return door.Locked?new(false,false,"door-locked",0,0,0,0,0):!door.Open?new(false,false,"door-closed",0,0,0,0,0):new(true,false,"door-open",0,0,0,0,0);
                case "hidden": return requiredActionsComplete||gate.Qualifier.Contains("Passable",StringComparison.OrdinalIgnoreCase)||context.DiscoveredExitIds.Contains(gate.ExitId)
                    ? new(true,false,requiredActionsComplete?"hidden-action-open":gate.Qualifier.Contains("Passable",StringComparison.OrdinalIgnoreCase)?"hidden-passable":"hidden-discovered",0,0,0,0,0)
                    : new(false,false,"hidden-undiscovered",0,0,0,0,0);
                case "required_item": return !string.IsNullOrWhiteSpace(gate.RequiredItemId)&&context.ItemIds.Contains(gate.RequiredItemId)?new(true,false,"required-item-present",0,0,0,0,0):new(false,false,"required-item-missing",0,0,0,0,0);
                case "ticket_item":
                    // Native type 17 identifies an explicit Ticket/Item prerequisite.
                    // Traversal currently preserves the ticket because consumption has not
                    // been proven; do not invent ticket destruction at this boundary.
                    return !string.IsNullOrWhiteSpace(gate.RequiredItemId)&&context.ItemIds.Contains(gate.RequiredItemId)?new(true,false,"ticket-item-present",0,0,0,0,0):new(false,false,"required-item-missing",0,0,0,0,0);
                case "text_command": return gate.Commands.Any(x=>x.Equals(context.EnteredCommand,StringComparison.OrdinalIgnoreCase))?new(true,false,"text-command-matched",0,0,0,0,0):new(false,false,"text-command-required",0,0,0,0,0);
                case "toll": return context.AvailableGold>=gate.TollAmount?new(true,false,"toll-paid",gate.TollAmount,0,0,0,0):new(false,false,"toll-insufficient",0,0,0,0,0);
                case "level_range": return new(true,false,"level-range-allowed",0,0,0,0,0);
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
        foreach(var deadline in _nativeActionResetDeadlines.Where(pair=>pair.Value<=nowUtc).ToArray())
        {
            var key=deadline.Key;
            if(!_nativeActionStates.TryGetValue(key,out var current)){_nativeActionResetDeadlines.Remove(key);continue;}
            if(key.StartsWith(ClassicTrapStateKeyPrefix,StringComparison.OrdinalIgnoreCase))
            {
                var reset=current switch{1=>0,4=>3,_=>current};
                _nativeActionStates[key]=reset;
                results.Add($"classic-trap-reset:{key[ClassicTrapStateKeyPrefix.Length..]}:{current}->{reset}");
            }
            else if(key.StartsWith(ClassicHiddenStateKeyPrefix,StringComparison.OrdinalIgnoreCase))
            {
                var exitId=key[ClassicHiddenStateKeyPrefix.Length..];
                var record=ClassicRoomsWorldAuthority().Require(exitId);
                var reset=RuntimeClassicRoomsWorldNativeAuthority.ResetHiddenState(current,record.Para2);
                _nativeActionStates[key]=reset;
                results.Add($"classic-hidden-reset:{exitId}:{current}->{reset}");
            }
            else if(key.StartsWith(ClassicDoorStateKeyPrefix,StringComparison.OrdinalIgnoreCase))
            {
                var exitId=key[ClassicDoorStateKeyPrefix.Length..];
                var record=ClassicRoomsWorldAuthority().Require(exitId);
                var reset=RuntimeClassicRoomsWorldNativeAuthority.ResetDoorState(record,current);
                _nativeActionStates[key]=reset;
                SynchronizeClassicDoorObjectLocked(exitId,reset,nowUtc);
                results.Add($"classic-door-reset:{exitId}:{current}->{reset}");
            }
            _nativeActionResetDeadlines.Remove(key);
        }
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
        var actionsByExit = new Dictionary<string,List<(int Order,string Id)>>(StringComparer.OrdinalIgnoreCase);
        var actionsByEdge = new Dictionary<string,List<(int Order,string Id)>>(StringComparer.OrdinalIgnoreCase);
        var interactionsPath=Path.Combine(_projectRoot,_world.Definition.DataRoot,"room_interactions.json");
        if(File.Exists(interactionsPath))
        {
            foreach(var interaction in RuntimeChunkedClassicCatalog.LoadRecords(interactionsPath))
            {
                var actionId=JsonValueReader.ReadString(interaction,"id",string.Empty);
                if(string.IsNullOrWhiteSpace(actionId))continue;
                var order=(int)ReadLong(interaction,"action_number");
                AddActionBinding(actionsByExit,JsonValueReader.ReadString(interaction,"bound_exit_id",string.Empty),order,actionId);
                AddActionBinding(actionsByEdge,JsonValueReader.ReadString(interaction,"bound_edge_state_id",string.Empty),order,actionId);
            }
        }
        foreach(var value in RuntimeChunkedClassicCatalog.LoadRecords(path))
        {
            var id=JsonValueReader.ReadString(value,"id",string.Empty); if(string.IsNullOrWhiteSpace(id))continue;
            var kind=JsonValueReader.ReadString(value,"gate_kind","open"); var room=ReadRelationship(value,"from_room_id","from_room"); var direction=JsonValueReader.ReadString(value,"direction",string.Empty);
            var qualifier=JsonValueReader.ReadString(value,"qualifier",string.Empty); var disposition=JsonValueReader.ReadString(value,"runtime_disposition",string.Empty);
            string? item=null; long toll=0; int damage=0,spell=0,pre=0,post=0;
            JsonElement prerequisites=default;
            if(value.TryGetProperty("prerequisites",out prerequisites)&&prerequisites.ValueKind==JsonValueKind.Object){item=NormalizeCanonicalItemId(ReadScalarString(prerequisites,"required_item_id") ?? string.Empty);toll=ReadLong(prerequisites,"toll_amount");}
            if(value.TryGetProperty("effects",out var effects)&&effects.ValueKind==JsonValueKind.Object){damage=(int)ReadLong(effects,"damage");spell=(int)ReadLong(effects,"spell_id");pre=(int)ReadLong(effects,"pre_spell_id");post=(int)ReadLong(effects,"post_spell_id");}
            JsonElement edge=default;
            value.TryGetProperty("edge_state_semantics",out edge);
            var stateIdentity=edge.ValueKind==JsonValueKind.Object ? ReadScalarString(edge,"state_identity") ?? string.Empty : string.Empty;
            var boundActions=new List<(int Order,string Id)>();
            if(actionsByExit.TryGetValue(id,out var directActions))boundActions.AddRange(directActions);
            if(!string.IsNullOrWhiteSpace(stateIdentity)&&actionsByEdge.TryGetValue(stateIdentity,out var edgeActions))boundActions.AddRange(edgeActions);
            var boundActionIds=boundActions
                .GroupBy(action=>action.Id,StringComparer.OrdinalIgnoreCase)
                .Select(group=>group.OrderBy(action=>action.Order).First())
                .OrderBy(action=>action.Order)
                .ThenBy(action=>action.Id,StringComparer.OrdinalIgnoreCase)
                .Select(action=>action.Id)
                .ToArray();
            var actionOrder=prerequisites.ValueKind==JsonValueKind.Object ? ReadScalarString(prerequisites,"action_order") ?? string.Empty : string.Empty;
            JsonElement actionBinding=default;
            if(edge.ValueKind==JsonValueKind.Object)edge.TryGetProperty("action_binding",out actionBinding);
            var passiveClassification=actionBinding.ValueKind==JsonValueKind.Object
                ? ReadScalarString(actionBinding,"classification") ?? string.Empty
                : string.Empty;
            var nativePassiveEffect=passiveClassification.Equals("native_or_passive_room_effect",StringComparison.OrdinalIgnoreCase)
                && actionBinding.ValueKind==JsonValueKind.Object
                && !ReadBool(actionBinding,"player_command_promoted");
            var gate=new RuntimeExitGateDefinition(id,room,direction,kind,qualifier,item,toll,damage,spell,pre,post,ReadStringArray(value,"commands"),disposition)
            {
                ReciprocalExitId = edge.ValueKind==JsonValueKind.Object ? ReadScalarString(edge,"reciprocal_exit_id") : null,
                StateIdentity = stateIdentity,
                OwnerScope = edge.ValueKind==JsonValueKind.Object ? ReadScalarString(edge,"owner_scope") ?? string.Empty : string.Empty,
                ReciprocalStateLink = edge.ValueKind==JsonValueKind.Object && ReadBool(edge,"reciprocal_state_link"),
                SaveWorldStateRisk = edge.ValueKind==JsonValueKind.Object && ReadBool(edge,"save_world_state_risk"),
                RequiredActionCount = prerequisites.ValueKind==JsonValueKind.Object ? (int)ReadLong(prerequisites,"required_action_count") : 0,
                RequiredActionOrder = prerequisites.ValueKind==JsonValueKind.Object ? ReadStringArray(prerequisites,"action_order") : Array.Empty<string>(),
                BoundActionIds = boundActionIds,
                RequiresSpecificActionOrder = actionOrder.Equals("specific",StringComparison.OrdinalIgnoreCase),
                NativePassiveEffect = nativePassiveEffect,
                NativePassiveEffectClassification = passiveClassification,
                AllowedClassId = ReadNullableInt(prerequisites,"allowed_class_id"), DeniedClassId = ReadNullableInt(prerequisites,"denied_class_id"),
                AllowedRaceId = ReadNullableInt(prerequisites,"allowed_race_id"), DeniedRaceId = ReadNullableInt(prerequisites,"denied_race_id"),
                MinimumAlignmentName = ReadScalarString(prerequisites,"minimum_alignment_name") ?? string.Empty,
                MaximumAlignmentName = ReadScalarString(prerequisites,"maximum_alignment_name") ?? string.Empty,
                AbilityId = ReadNullableInt(prerequisites,"ability_id"), MinimumValue = ReadNullableLong(prerequisites,"minimum_value"), MaximumValue = ReadNullableLong(prerequisites,"maximum_value"),
                PicklocksDifficulty = ReadNullableInt(prerequisites,"picklocks_difficulty"), PicklocksOrStrength = ReadNullableInt(prerequisites,"picklocks_or_strength"),
                AnyPicklocks = ReadBool(prerequisites,"any_picklocks"), AnyPicklocksOrStrength = ReadBool(prerequisites,"any_picklocks_or_strength"), StrengthAlternative = ReadBool(prerequisites,"strength_alternative"),
                MinimumLevel = ReadNullableInt(prerequisites,"minimum_level"), MaximumLevel = ReadNullableInt(prerequisites,"maximum_level"),
                TimeMultiplier = ReadDouble(prerequisites,"time_multiplier",1d), AutomaticRouting = edge.ValueKind==JsonValueKind.Object && ReadBool(edge,"automatic_routing"),
                FallbackBehavior = JsonValueReader.ReadString(value,"fallback_behavior",string.Empty)
            };
            _gates[id]=gate;
        }
    }

    private static void AddActionBinding(Dictionary<string,List<(int Order,string Id)>> bindings,string key,int order,string id)
    {
        if(string.IsNullOrWhiteSpace(key))return;
        if(!bindings.TryGetValue(key,out var values))bindings[key]=values=[];
        values.Add((order,id));
    }

    private static bool ReadBool(JsonElement element,string name)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(name,out var value)&&value.ValueKind switch{JsonValueKind.True=>true,JsonValueKind.False=>false,JsonValueKind.Number when value.TryGetInt32(out var n)=>n!=0,JsonValueKind.String when bool.TryParse(value.GetString(),out var b)=>b,JsonValueKind.String when int.TryParse(value.GetString(),out var n)=>n!=0,_=>false};
    private static int? ReadNullableInt(JsonElement element,string name){if(element.ValueKind!=JsonValueKind.Object||!element.TryGetProperty(name,out var value)||value.ValueKind==JsonValueKind.Null)return null;return value.ValueKind switch{JsonValueKind.Number when value.TryGetInt32(out var n)=>n,JsonValueKind.String when int.TryParse(value.GetString(),out var n)=>n,_=>null};}
    private static long? ReadNullableLong(JsonElement element,string name){if(element.ValueKind!=JsonValueKind.Object||!element.TryGetProperty(name,out var value)||value.ValueKind==JsonValueKind.Null)return null;return value.ValueKind switch{JsonValueKind.Number when value.TryGetInt64(out var n)=>n,JsonValueKind.String when long.TryParse(value.GetString(),out var n)=>n,_=>null};}
    private static double ReadDouble(JsonElement element,string name,double fallback)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(name,out var value)?value.ValueKind switch{JsonValueKind.Number when value.TryGetDouble(out var n)=>n,JsonValueKind.String when double.TryParse(value.GetString(),out var n)=>n,_=>fallback}:fallback;
    private static int CompareAlignment(string actual,string required)
    {
        // Native MajorMUD reputation bands are ordered, not interchangeable.
        // In particular Villain is eligible for a gate ending at Villain;
        // only Fiend falls below that minimum. Do not quietly treat unknown
        // reputation labels as Neutral.
        static int Rank(string value) => value.Trim().ToLowerInvariant() switch
        {
            "fiend" => 0,
            "villain" => 1,
            "criminal" => 2,
            "outlaw" => 3,
            "seedy" => 4,
            "evil" => 4,
            "neutral" => 5,
            "good" => 6,
            "saint" => 7,
            "lawful" => 8,
            _ => throw new InvalidDataException($"Unknown Classic alignment name '{value}'.")
        };
        return Rank(actual).CompareTo(Rank(required));
    }
    private static string? ReadScalarString(JsonElement element,string name)=>element.TryGetProperty(name,out var value)?value.ValueKind switch{JsonValueKind.String=>value.GetString(),JsonValueKind.Number=>value.GetRawText(),_=>null}:null;
    private static long ReadLong(JsonElement element,string name)=>element.TryGetProperty(name,out var value)?value.ValueKind switch{JsonValueKind.Number when value.TryGetInt64(out var number)=>number,JsonValueKind.String when long.TryParse(value.GetString(),out var textNumber)=>textNumber,_=>0}:0;
    private static int ParseClassicLegacyId(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return 0;
        var normalized=value.Trim();
        var separator=normalized.LastIndexOf('.');
        if(separator>=0)normalized=normalized[(separator+1)..];
        return int.TryParse(normalized,out var parsed)?parsed:0;
    }
    private string CompletionPersistencePath=>Path.Combine(_persistenceRoot,WorldId,"living-world-completion.json");
    private void SaveLivingWorldCompletionLocked(){var path=CompletionPersistencePath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);var snapshot=new RuntimeLivingWorldCompletionSnapshot(WorldId,_eventSequence,_npcAi.Values.ToArray(),_corpses.Values.ToArray(),_recentEvents.ToArray(),new Dictionary<string,int>(_nativeActionStates,StringComparer.OrdinalIgnoreCase),DateTimeOffset.UtcNow){NativeActionResetDeadlines=new Dictionary<string,DateTimeOffset>(_nativeActionResetDeadlines,StringComparer.OrdinalIgnoreCase)};var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(snapshot,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,path,true);}
    private void RestoreLivingWorldCompletion(){lock(_gate){if(!File.Exists(CompletionPersistencePath))return;try{var x=JsonSerializer.Deserialize<RuntimeLivingWorldCompletionSnapshot>(File.ReadAllText(CompletionPersistencePath));if(x is null||!x.WorldId.Equals(WorldId,StringComparison.OrdinalIgnoreCase))return;_eventSequence=x.EventSequence;foreach(var ai in x.AiStates)if(_npcAi.ContainsKey(ai.InstanceId))_npcAi[ai.InstanceId]=ai;foreach(var c in x.Corpses)if(_npcs.ContainsKey(c.NpcInstanceId))_corpses[c.CorpseId]=c;foreach(var e in x.RecentEvents.OrderBy(e=>e.Sequence))_recentEvents.Enqueue(e);if(x.NativeActionStates is not null)foreach(var state in x.NativeActionStates)if(_nativeActionStates.ContainsKey(state.Key)||state.Key.StartsWith(ClassicTrapStateKeyPrefix,StringComparison.OrdinalIgnoreCase)||state.Key.StartsWith(ClassicHiddenStateKeyPrefix,StringComparison.OrdinalIgnoreCase)||state.Key.StartsWith(ClassicDoorStateKeyPrefix,StringComparison.OrdinalIgnoreCase))_nativeActionStates[state.Key]=state.Value;if(x.NativeActionResetDeadlines is not null)foreach(var deadline in x.NativeActionResetDeadlines)_nativeActionResetDeadlines[deadline.Key]=deadline.Value;foreach(var state in _nativeActionStates.Where(pair=>pair.Key.StartsWith(ClassicDoorStateKeyPrefix,StringComparison.OrdinalIgnoreCase)).ToArray())SynchronizeClassicDoorObjectLocked(state.Key[ClassicDoorStateKeyPrefix.Length..],state.Value,DateTimeOffset.UtcNow);}catch(JsonException){}}}
}
