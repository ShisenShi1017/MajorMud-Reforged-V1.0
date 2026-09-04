namespace RealmGate.Server;

public enum RuntimeBuilderSurface { Room, Exit, Item, Npc, Spell, Quest, Textblock, Merchant, Trainer, Spawn, Event }
public enum RuntimeBuilderEnvironment { Classic, Custom, Ai, TestRealm }
public enum RuntimeBuilderOperation { Inspect, Validate, Preview, Stage, Publish, Rollback, LiveReload }
public sealed record RuntimeBuilderActor(string ActorId,bool Authenticated,bool HostOperator,bool BuilderAuthorized);
public sealed record RuntimeBuilderRequest(string ChangeId,RuntimeBuilderActor Actor,RuntimeBuilderEnvironment Environment,RuntimeBuilderSurface Surface,RuntimeBuilderOperation Operation,string TargetId,int ExpectedRevision,bool ClassicCompatible,bool HasValidationEvidence);
public sealed record RuntimeBuilderChangeState(string ChangeId,string TargetId,RuntimeBuilderSurface Surface,int Revision,bool Staged,bool Published,bool RolledBack);
public sealed record RuntimeBuilderResult(bool Success,string Code,RuntimeBuilderChangeState? State,bool RequiresOwnerValidation,bool RequiresRestart,bool RequiresTestRealm,IReadOnlyList<string> AuditFacts);

public sealed class RuntimeBuilderContentPolicyService
{
    private readonly Dictionary<string,RuntimeBuilderChangeState> _changes=new(StringComparer.OrdinalIgnoreCase);
    public RuntimeBuilderResult Evaluate(RuntimeBuilderRequest request)
    {
        var audit=new List<string>{"change:"+request.ChangeId,"surface:"+request.Surface,"operation:"+request.Operation,"environment:"+request.Environment};
        if(string.IsNullOrWhiteSpace(request.ChangeId)||string.IsNullOrWhiteSpace(request.TargetId)||string.IsNullOrWhiteSpace(request.Actor.ActorId)) return Fail("builder-request-invalid",audit);
        if(!request.Actor.Authenticated||!request.Actor.HostOperator||!request.Actor.BuilderAuthorized) return Fail("builder-authorization-denied",audit);
        if(request.ExpectedRevision<0) return Fail("builder-revision-invalid",audit);
        if(request.Environment==RuntimeBuilderEnvironment.Classic && !request.ClassicCompatible) return Fail("classic-incompatible-change",audit);
        if(request.Operation is RuntimeBuilderOperation.Publish or RuntimeBuilderOperation.LiveReload)
        {
            if(request.Environment==RuntimeBuilderEnvironment.Classic && !request.HasValidationEvidence) return Fail("classic-validation-evidence-required",audit);
            if(request.Environment!=RuntimeBuilderEnvironment.TestRealm && request.Operation==RuntimeBuilderOperation.LiveReload) return Fail("live-reload-test-realm-only",audit);
        }
        _changes.TryGetValue(request.ChangeId,out var current);
        if(current is not null && current.Revision!=request.ExpectedRevision) return Fail("builder-stale-revision",audit);
        current ??= new RuntimeBuilderChangeState(request.ChangeId,request.TargetId,request.Surface,0,false,false,false);
        RuntimeBuilderChangeState next=request.Operation switch
        {
            RuntimeBuilderOperation.Stage => current with{Revision=current.Revision+1,Staged=true,Published=false,RolledBack=false},
            RuntimeBuilderOperation.Publish when current.Staged => current with{Revision=current.Revision+1,Published=true,RolledBack=false},
            RuntimeBuilderOperation.Rollback when current.Staged||current.Published => current with{Revision=current.Revision+1,Published=false,RolledBack=true},
            RuntimeBuilderOperation.Inspect or RuntimeBuilderOperation.Validate or RuntimeBuilderOperation.Preview => current,
            RuntimeBuilderOperation.LiveReload when current.Staged => current with{Revision=current.Revision+1,Published=true,RolledBack=false},
            _ => current
        };
        if(request.Operation==RuntimeBuilderOperation.Publish && !current.Staged) return Fail("builder-change-not-staged",audit);
        if(request.Operation==RuntimeBuilderOperation.Rollback && !current.Staged && !current.Published) return Fail("builder-nothing-to-rollback",audit);
        if(request.Operation==RuntimeBuilderOperation.LiveReload && !current.Staged) return Fail("builder-change-not-staged",audit);
        _changes[request.ChangeId]=next;
        audit.Add("revision:"+next.Revision);
        return new(true,"builder-policy-approved",next,true,request.Operation==RuntimeBuilderOperation.Publish&&request.Environment!=RuntimeBuilderEnvironment.TestRealm,request.Environment!=RuntimeBuilderEnvironment.TestRealm&&request.Operation is RuntimeBuilderOperation.Publish or RuntimeBuilderOperation.LiveReload,audit);
    }
    public IReadOnlyList<string> ValidateState()
    {
        var errors=new List<string>();
        foreach(var state in _changes.Values){if(state.Revision<0)errors.Add("negative-revision:"+state.ChangeId);if(state.Published&&state.RolledBack)errors.Add("published-and-rolled-back:"+state.ChangeId);}
        return errors;
    }
    private static RuntimeBuilderResult Fail(string code,IReadOnlyList<string> audit)=>new(false,code,null,true,false,false,audit);
}
