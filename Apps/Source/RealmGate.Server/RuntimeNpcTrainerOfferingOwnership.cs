using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeNpcTrainerOfferingSnapshot(string NpcInstanceId, string TrainerId, IReadOnlyList<RuntimeTrainerOfferingEntry> Entries);
public sealed record RuntimeNpcTrainerOfferingMutation(string NpcInstanceId, string TrainerId, string OfferingId, string OfferingKind, bool Enabled, DateTimeOffset NextAvailableUtc, string Authority);
public sealed record RuntimeNpcTrainerOfferingResult(bool Succeeded, string NpcInstanceId, string TrainerId, string OfferingId, bool Enabled, string Reason);

/// <summary>
/// Exact ownership bridge between a living trainer NPC and persistent trainer-offering state.
/// The caller owns eligibility, cost, cooldown duration, progression, and refusal dialogue. This
/// boundary validates the owning NPC/service relationship and persists the exact supplied state.
/// </summary>
public sealed class RuntimeNpcTrainerOfferingOwnershipService
{
    private readonly RuntimeLivingWorldService _living;
    private readonly RuntimeGameplayCompletionStateStore _store;
    public RuntimeNpcTrainerOfferingOwnershipService(RuntimeLivingWorldService living, RuntimeGameplayCompletionStateStore store){_living=living;_store=store;}

    public RuntimeNpcTrainerOfferingSnapshot Inspect(string npcInstanceId, RuntimeServiceDefinition trainer)
    {
        var npc=RequireTrainer(npcInstanceId,trainer);
        var state=_store.ReadTrainer(trainer);
        return new(npc.InstanceId,trainer.Id,state.Offerings.Values.OrderBy(x=>x.OfferingKind,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.OfferingId,StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public RuntimeNpcTrainerOfferingResult SetExact(RuntimeNpcTrainerOfferingMutation request, RuntimeServiceDefinition trainer)
    {
        if(string.IsNullOrWhiteSpace(request.Authority)) return Reject(request,"authority-required");
        if(!trainer.Id.Equals(request.TrainerId,StringComparison.OrdinalIgnoreCase)) return Reject(request,"trainer-id-mismatch");
        RuntimeNpcInspectionSnapshot npc;
        try{npc=RequireTrainer(request.NpcInstanceId,trainer);}catch(InvalidOperationException ex){return Reject(request,Normalize(ex.Message));}
        if(string.IsNullOrWhiteSpace(request.OfferingId)) return Reject(request,"offering-id-required");
        if(string.IsNullOrWhiteSpace(request.OfferingKind)) return Reject(request,"offering-kind-required");
        var kind=request.OfferingKind.Trim().ToLowerInvariant();
        if(kind is not ("level" or "ability" or "spell" or "skill" or "promotion")) return Reject(request,"offering-kind-unsupported");
        _store.ExecuteTrainerTransaction(trainer,offerings=>
        {
            offerings[request.OfferingId]=new RuntimeTrainerOfferingEntry(request.OfferingId,kind,request.Enabled,request.NextAvailableUtc,request.Authority.Trim());
            return 0;
        });
        return new(true,npc.InstanceId,trainer.Id,request.OfferingId,request.Enabled,"offering-set-exactly");
    }

    private RuntimeNpcInspectionSnapshot RequireTrainer(string instanceId, RuntimeServiceDefinition trainer)
    {
        var npc=_living.InspectNpc(instanceId) ?? throw new InvalidOperationException("instance-not-found");
        if(!npc.Alive) throw new InvalidOperationException("npc-is-not-alive");
        if(!npc.InteractionModel.Contains("trainer",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("npc-does-not-own-trainer-service");
        if(!trainer.ServiceKind.Contains("trainer",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("service-is-not-a-trainer");
        return npc;
    }
    private static RuntimeNpcTrainerOfferingResult Reject(RuntimeNpcTrainerOfferingMutation r,string reason)=>new(false,r.NpcInstanceId??string.Empty,r.TrainerId??string.Empty,r.OfferingId??string.Empty,r.Enabled,reason);
    private static string Normalize(string value)=>string.Join('-',value.Trim().TrimEnd('.').ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
}

public sealed class RuntimeNpcTrainerOfferingOwnershipSmokeReport
{
    public bool Passed=>Failures.Count==0; public List<string> Checks{get;}=[]; public List<string> Failures{get;}=[];
    public static RuntimeNpcTrainerOfferingOwnershipSmokeReport Run(){var r=new RuntimeNpcTrainerOfferingOwnershipSmokeReport();try{var t=typeof(RuntimeNpcTrainerOfferingOwnershipService);Require(t.GetMethod(nameof(RuntimeNpcTrainerOfferingOwnershipService.Inspect)) is not null,"trainer-offering-inspection-exposed");Require(t.GetMethod(nameof(RuntimeNpcTrainerOfferingOwnershipService.SetExact)) is not null,"trainer-offering-exact-mutation-exposed");var store=typeof(RuntimeGameplayCompletionStateStore);Require(store.GetMethod(nameof(RuntimeGameplayCompletionStateStore.ReadTrainer)) is not null,"persistent-trainer-read-exposed");Require(store.GetMethod(nameof(RuntimeGameplayCompletionStateStore.ExecuteTrainerTransaction)) is not null,"persistent-trainer-transaction-exposed");r.Checks.AddRange(["trainer-offering-inspection-exposed","trainer-offering-exact-mutation-exposed","persistent-trainer-read-exposed","persistent-trainer-transaction-exposed"]);}catch(Exception ex){r.Failures.Add(ex.Message);}return r;}
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}); private static void Require(bool c,string code){if(!c)throw new InvalidOperationException(code);}
}
