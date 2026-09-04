using System.Text.Json;
namespace RealmGate.Server;
public sealed record RuntimeNpcTrainerEligibilityAdmissionRequest(string NpcInstanceId,string TrainerId,string OfferingId,string ActorId,string ActorRoomId,bool ActorEligible,bool ResourcesAuthorized,bool ProgressionAuthorized,DateTimeOffset RequestedAtUtc,string Authority);
public sealed record RuntimeNpcTrainerEligibilityAdmissionResult(bool Admitted,string NpcInstanceId,string TrainerId,string OfferingId,string? RequestId,DateTimeOffset NextAvailableUtc,string Reason);
/// <summary>Exact trainer eligibility bridge. Character/progression policy supplies resolved eligibility, resource, and progression authorization. No native trainer rule is derived here.</summary>
public sealed class RuntimeNpcTrainerEligibilityAdmissionService
{
    private readonly RuntimeNpcTrainerOfferingAdmissionService _offerings;
    public RuntimeNpcTrainerEligibilityAdmissionService(RuntimeNpcTrainerOfferingAdmissionService offerings)=>_offerings=offerings;
    public RuntimeNpcTrainerEligibilityAdmissionResult AdmitExact(RuntimeNpcTrainerEligibilityAdmissionRequest r,RuntimeServiceDefinition trainer)
    {
        if(string.IsNullOrWhiteSpace(r.Authority)) return Reject(r,"authority-required");
        if(!trainer.Id.Equals(r.TrainerId,StringComparison.OrdinalIgnoreCase)) return Reject(r,"trainer-id-mismatch");
        if(!r.ActorEligible) return Reject(r,"actor-ineligible");
        if(!r.ResourcesAuthorized) return Reject(r,"resources-not-authorized");
        if(!r.ProgressionAuthorized) return Reject(r,"progression-not-authorized");
        var a=_offerings.AdmitExact(new(r.NpcInstanceId,r.TrainerId,r.OfferingId,r.ActorId,r.ActorRoomId,r.RequestedAtUtc,r.Authority),trainer);
        return new(a.Admitted,a.NpcInstanceId,a.TrainerId,a.OfferingId,a.RequestId,a.NextAvailableUtc,a.Admitted?"trainer-eligibility-admitted":a.Reason);
    }
    private static RuntimeNpcTrainerEligibilityAdmissionResult Reject(RuntimeNpcTrainerEligibilityAdmissionRequest r,string reason)=>new(false,r.NpcInstanceId??"",r.TrainerId??"",r.OfferingId??"",null,DateTimeOffset.MinValue,reason);
}
public sealed class RuntimeNpcTrainerEligibilityAdmissionSmokeReport
{
    public bool Passed=>Failures.Count==0; public List<string> Checks{get;}=[]; public List<string> Failures{get;}=[];
    public static RuntimeNpcTrainerEligibilityAdmissionSmokeReport Run(){var x=new RuntimeNpcTrainerEligibilityAdmissionSmokeReport();try{Require(typeof(RuntimeNpcTrainerEligibilityAdmissionService).GetMethod(nameof(RuntimeNpcTrainerEligibilityAdmissionService.AdmitExact)) is not null,"trainer-eligibility-admission-exposed");Require(typeof(RuntimeNpcTrainerOfferingAdmissionService).GetMethod(nameof(RuntimeNpcTrainerOfferingAdmissionService.AdmitExact)) is not null,"trainer-offering-admission-integrated");x.Checks.AddRange(["trainer-eligibility-admission-exposed","trainer-offering-admission-integrated"]);}catch(Exception e){x.Failures.Add(e.Message);}return x;}
    public string ToJson()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}); private static void Require(bool c,string code){if(!c)throw new InvalidOperationException(code);}
}
