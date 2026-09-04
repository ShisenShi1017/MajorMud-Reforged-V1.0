using System.Text.Json;
namespace RealmGate.Server;
public sealed record RuntimeErrorRecoverySmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    public static RuntimeErrorRecoverySmokeReport Run()
    {
        var checks=new List<string>(); var failures=new List<string>();
        void Check(string id,bool ok){checks.Add(id);if(!ok)failures.Add(id);}
        var service=new RuntimeErrorRecoveryService();
        Check("invalid-record-default", service.Decide(new(RuntimeFailureKind.InvalidRecord,"Catalog","load",ValidatedDefaultAvailable:true)).Action==RuntimeRecoveryAction.UseValidatedDefault);
        Check("missing-reference-fail-closed", service.Decide(new(RuntimeFailureKind.MissingReference,"Rooms","resolve",RecordId:"room:404")).Action==RuntimeRecoveryAction.Reject);
        Check("overflow-rollback", service.Decide(new(RuntimeFailureKind.ArithmeticOverflow,"Economy","transfer",StateMutationStarted:true,RollbackAvailable:true)).RequiresRollback);
        var persistence=service.Apply(new(RuntimeFailureKind.PersistenceFailure,"WorldState","save"));
        Check("disable-on-unrecoverable-persistence", persistence.DisableSubsystem && service.IsSubsystemDisabled("WorldState"));
        Check("critical-load-stop", service.Decide(new(RuntimeFailureKind.InvalidRecord,"Catalog","startup",WorldLoadCritical:true)).StopWorldLoad);
        Check("duplicate-id-stop", service.Decide(new(RuntimeFailureKind.DuplicateRuntimeId,"NPC","spawn")).StopWorldLoad);
        var noOp = service.Apply(new(RuntimeFailureKind.GameplayActionRejected,"ClassicGameplay","command",CharacterScoped:true,RollbackAvailable:true));
        Check("classic-gameplay-failure-no-op", noOp.Action==RuntimeRecoveryAction.NoOp && !noOp.RequiresRollback && !noOp.DisableSubsystem && !noOp.StopWorldLoad);
        Check("classic-gameplay-failure-no-disable-state", !service.IsSubsystemDisabled("ClassicGameplay"));
        Check("unknown-character-disconnect", service.Decide(new(RuntimeFailureKind.Unknown,"SessionOwnership","restore",CharacterScoped:true)).Action==RuntimeRecoveryAction.DisconnectCharacter);
        Check("state-valid", service.ValidateState().Count==0);
        return new(failures.Count==0,checks,failures);
    }
}
