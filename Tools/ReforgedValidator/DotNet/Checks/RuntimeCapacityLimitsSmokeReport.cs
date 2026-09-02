using System.Text.Json;
namespace Reforged.Server;
public sealed record RuntimeCapacityLimitsSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeCapacityLimitsSmokeReport Run()
    {
        var checks=new List<string>(); var failures=new List<string>();
        void Check(string id,bool ok){checks.Add(id);if(!ok)failures.Add(id);}
        var service=new RuntimeCapacityLimitsService(new[]{
            new RuntimeCapacityDefinition(RuntimeCapacityResource.Users,2,"SessionOwnership"),
            new RuntimeCapacityDefinition(RuntimeCapacityResource.Timers,4,"ClockScheduling"),
            new RuntimeCapacityDefinition(RuntimeCapacityResource.WorldStateEntries,8,"GlobalWorldState")});
        Check("reserve", service.Reserve("SessionOwnership",RuntimeCapacityResource.Users,1).Success);
        Check("commit", service.Commit("SessionOwnership",RuntimeCapacityResource.Users,1).Snapshot?.Used==1);
        Check("hard-limit", service.Reserve("SessionOwnership",RuntimeCapacityResource.Users,2).Code=="capacity-limit-exceeded");
        Check("owner-boundary", service.Reserve("Wrong",RuntimeCapacityResource.Timers,1).Code=="capacity-owner-mismatch");
        Check("host-required", service.ChangeMaximum("Host",RuntimeCapacityResource.Users,3,false).Code=="capacity-host-authorization-required");
        Check("limit-change", service.ChangeMaximum("Host",RuntimeCapacityResource.Users,3,true).Snapshot?.Maximum==3);
        Check("release", service.Release("SessionOwnership",RuntimeCapacityResource.Users,1,true).Snapshot?.Used==0);
        Check("state-valid", service.ValidateState().Count==0);
        return new(failures.Count==0,checks,failures);
    }
}
