namespace RealmGate.Server;

public enum RuntimeOptionalActivityKind { Duel, Arena, Tournament, DiceGame, Lottery, Raffle, Contest, Scoreboard }
public enum RuntimeOptionalActivityState { Disabled, Open, Active, Completed, Cancelled }
public sealed record RuntimeOptionalActivityDefinition(string ActivityId, RuntimeOptionalActivityKind Kind, bool Enabled, int MinimumParticipants=2, int MaximumParticipants=2, long EntryCost=0, string CurrencyId="gold", bool ClassicEvidenceConfirmed=false);
public sealed record RuntimeOptionalActivityRequest(string OwnerSubsystem,string ActionId,string ActivityId,string? ActorId=null,long Amount=0,string? CurrencyId=null);
public sealed record RuntimeOptionalActivityResult(bool Success,string Code,IReadOnlyList<RuntimeOptionalActivityRequest>? Requests=null);

public sealed class RuntimeOptionalActivitiesService
{
    private sealed class ActivityState
    {
        public RuntimeOptionalActivityDefinition Definition { get; }
        public RuntimeOptionalActivityState State { get; set; }
        public HashSet<string> Participants { get; } = new(StringComparer.OrdinalIgnoreCase);
        public long Revision { get; set; }
        public ActivityState(RuntimeOptionalActivityDefinition definition){Definition=definition;State=definition.Enabled?RuntimeOptionalActivityState.Open:RuntimeOptionalActivityState.Disabled;}
    }
    private readonly Dictionary<string,ActivityState> _activities=new(StringComparer.OrdinalIgnoreCase);
    public RuntimeOptionalActivitiesService(IEnumerable<RuntimeOptionalActivityDefinition> definitions)
    {
        foreach(var d in definitions){
            if(string.IsNullOrWhiteSpace(d.ActivityId)) throw new ArgumentException("optional-activity-id-required");
            if(d.MinimumParticipants<1||d.MaximumParticipants<d.MinimumParticipants) throw new ArgumentException($"optional-activity-participants-invalid:{d.ActivityId}");
            if(d.EntryCost<0) throw new ArgumentException($"optional-activity-cost-invalid:{d.ActivityId}");
            if(!_activities.TryAdd(d.ActivityId,new ActivityState(d))) throw new ArgumentException($"optional-activity-duplicate:{d.ActivityId}");
        }
    }
    public RuntimeOptionalActivityResult Join(string activityId,string actorId)
    {
        if(!_activities.TryGetValue(activityId,out var a)) return new(false,"optional-activity-unknown");
        if(!a.Definition.Enabled) return new(false,"optional-activity-disabled");
        if(!a.Definition.ClassicEvidenceConfirmed) return new(false,"optional-activity-classic-evidence-required");
        if(a.State!=RuntimeOptionalActivityState.Open) return new(false,"optional-activity-not-open");
        if(a.Participants.Count>=a.Definition.MaximumParticipants) return new(false,"optional-activity-full");
        if(!a.Participants.Add(actorId)) return new(false,"optional-activity-already-joined");
        a.Revision++;
        var requests=new List<RuntimeOptionalActivityRequest>();
        if(a.Definition.EntryCost>0) requests.Add(new("Economy","charge-entry",activityId,actorId,a.Definition.EntryCost,a.Definition.CurrencyId));
        return new(true,"optional-activity-joined",requests);
    }
    public RuntimeOptionalActivityResult Leave(string activityId,string actorId)
    {
        if(!_activities.TryGetValue(activityId,out var a)) return new(false,"optional-activity-unknown");
        if(a.State==RuntimeOptionalActivityState.Active) return new(false,"optional-activity-active-leave-denied");
        if(!a.Participants.Remove(actorId)) return new(false,"optional-activity-not-participant");
        a.Revision++; return new(true,"optional-activity-left");
    }
    public RuntimeOptionalActivityResult Start(string activityId,bool administratorAuthorized=false)
    {
        if(!_activities.TryGetValue(activityId,out var a)) return new(false,"optional-activity-unknown");
        if(!a.Definition.Enabled||!a.Definition.ClassicEvidenceConfirmed) return new(false,"optional-activity-disabled-or-unproven");
        if(!administratorAuthorized) return new(false,"optional-activity-administrator-required");
        if(a.State!=RuntimeOptionalActivityState.Open) return new(false,"optional-activity-not-open");
        if(a.Participants.Count<a.Definition.MinimumParticipants) return new(false,"optional-activity-too-few-participants");
        a.State=RuntimeOptionalActivityState.Active;a.Revision++;
        return new(true,"optional-activity-started",new[]{new RuntimeOptionalActivityRequest("Combat","apply-activity-rules",activityId),new RuntimeOptionalActivityRequest("Communications","announce-activity-start",activityId)});
    }
    public RuntimeOptionalActivityResult Complete(string activityId,string winnerId,bool administratorAuthorized=false)
    {
        if(!_activities.TryGetValue(activityId,out var a)) return new(false,"optional-activity-unknown");
        if(!administratorAuthorized) return new(false,"optional-activity-administrator-required");
        if(a.State!=RuntimeOptionalActivityState.Active) return new(false,"optional-activity-not-active");
        if(!a.Participants.Contains(winnerId)) return new(false,"optional-activity-winner-not-participant");
        a.State=RuntimeOptionalActivityState.Completed;a.Revision++;
        return new(true,"optional-activity-completed",new[]{new RuntimeOptionalActivityRequest("RankingsStatistics","record-activity-result",activityId,winnerId),new RuntimeOptionalActivityRequest("Economy","resolve-activity-reward",activityId,winnerId)});
    }
    public IReadOnlyList<string> ValidateState()
    {
        var issues=new List<string>();
        foreach(var kv in _activities){var a=kv.Value;if(a.Participants.Count>a.Definition.MaximumParticipants)issues.Add($"optional-activity-over-capacity:{kv.Key}");if(!a.Definition.Enabled&&a.State!=RuntimeOptionalActivityState.Disabled)issues.Add($"optional-activity-disabled-state-invalid:{kv.Key}");}
        return issues;
    }
}
