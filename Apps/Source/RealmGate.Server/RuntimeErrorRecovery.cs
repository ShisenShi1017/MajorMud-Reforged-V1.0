namespace RealmGate.Server;

public enum RuntimeFailureKind
{
    InvalidRecord, MissingReference, CorruptSave, DuplicateRuntimeId, TimerOverflow,
    ArithmeticOverflow, PersistenceFailure, PartialWorldLoad, ScriptFailure,
    EventFailure, ResetFailure, GameplayActionRejected, Unknown
}

public enum RuntimeRecoveryAction
{
    Reject, SkipRecord, UseValidatedDefault, Rollback, DisconnectCharacter,
    DisableSubsystem, StopWorldLoad, LogAndContinue, NoOp
}

public sealed record RuntimeFailureContext(
    RuntimeFailureKind Kind,
    string OwnerSubsystem,
    string Operation,
    bool StateMutationStarted = false,
    bool CharacterScoped = false,
    bool WorldLoadCritical = false,
    bool ValidatedDefaultAvailable = false,
    bool RollbackAvailable = false,
    string? RecordId = null,
    string? CorrelationId = null);

public sealed record RuntimeRecoveryDecision(
    bool Accepted,
    string Code,
    RuntimeRecoveryAction Action,
    bool RequiresAudit,
    bool RequiresRollback,
    bool DisableSubsystem,
    bool StopWorldLoad,
    string OwnerSubsystem,
    string? RecordId = null);

public sealed class RuntimeErrorRecoveryService
{
    private readonly HashSet<string> _disabledSubsystems = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeRecoveryDecision Decide(RuntimeFailureContext context)
    {
        if (string.IsNullOrWhiteSpace(context.OwnerSubsystem))
            return Reject(context, "recovery-owner-required");
        if (string.IsNullOrWhiteSpace(context.Operation))
            return Reject(context, "recovery-operation-required");

        if (context.WorldLoadCritical)
            return Decision(context, "recovery-stop-world-load", RuntimeRecoveryAction.StopWorldLoad, stopWorldLoad: true);

        return context.Kind switch
        {
            RuntimeFailureKind.InvalidRecord when context.ValidatedDefaultAvailable
                => Decision(context, "recovery-use-validated-default", RuntimeRecoveryAction.UseValidatedDefault),
            RuntimeFailureKind.InvalidRecord
                => Decision(context, "recovery-skip-invalid-record", RuntimeRecoveryAction.SkipRecord),
            RuntimeFailureKind.MissingReference
                => Decision(context, "recovery-reject-missing-reference", RuntimeRecoveryAction.Reject),
            RuntimeFailureKind.CorruptSave when context.ValidatedDefaultAvailable
                => Decision(context, "recovery-use-validated-save-default", RuntimeRecoveryAction.UseValidatedDefault),
            RuntimeFailureKind.CorruptSave
                => Decision(context, "recovery-disable-corrupt-save-owner", RuntimeRecoveryAction.DisableSubsystem, disableSubsystem: true),
            RuntimeFailureKind.DuplicateRuntimeId
                => Decision(context, "recovery-stop-duplicate-runtime-id", RuntimeRecoveryAction.StopWorldLoad, stopWorldLoad: true),
            RuntimeFailureKind.TimerOverflow or RuntimeFailureKind.ArithmeticOverflow
                => context.RollbackAvailable
                    ? Decision(context, "recovery-rollback-overflow", RuntimeRecoveryAction.Rollback, requiresRollback: true)
                    : Decision(context, "recovery-reject-overflow", RuntimeRecoveryAction.Reject),
            RuntimeFailureKind.PersistenceFailure
                => context.RollbackAvailable
                    ? Decision(context, "recovery-rollback-persistence", RuntimeRecoveryAction.Rollback, requiresRollback: true)
                    : Decision(context, "recovery-disable-persistence-owner", RuntimeRecoveryAction.DisableSubsystem, disableSubsystem: true),
            RuntimeFailureKind.PartialWorldLoad
                => Decision(context, "recovery-stop-partial-world-load", RuntimeRecoveryAction.StopWorldLoad, stopWorldLoad: true),
            RuntimeFailureKind.ScriptFailure or RuntimeFailureKind.EventFailure or RuntimeFailureKind.ResetFailure
                => context.RollbackAvailable
                    ? Decision(context, "recovery-rollback-owner-operation", RuntimeRecoveryAction.Rollback, requiresRollback: true)
                    : Decision(context, "recovery-disable-failed-owner", RuntimeRecoveryAction.DisableSubsystem, disableSubsystem: true),
            // Operator-confirmed Classic boundary: an ordinary rejected gameplay action leaves state
            // untouched. This branch must never be generalized to wrong/corrupt native data files,
            // catastrophic persistence failures, or Worldgroup host recovery. Exact player-visible
            // failure text versus silence remains command/action-specific and evidence-gated.
            RuntimeFailureKind.GameplayActionRejected
                => Decision(context, "classic-gameplay-action-no-op", RuntimeRecoveryAction.NoOp),
            _ => context.CharacterScoped
                ? Decision(context, "recovery-disconnect-character", RuntimeRecoveryAction.DisconnectCharacter)
                : Decision(context, "recovery-reject-unknown-failure", RuntimeRecoveryAction.Reject)
        };
    }

    public RuntimeRecoveryDecision Apply(RuntimeFailureContext context)
    {
        var decision = Decide(context);
        if (decision.Accepted && decision.DisableSubsystem)
            _disabledSubsystems.Add(context.OwnerSubsystem);
        return decision;
    }

    public bool IsSubsystemDisabled(string subsystem) => _disabledSubsystems.Contains(subsystem);

    public IReadOnlyList<string> ValidateState()
        => _disabledSubsystems.Where(string.IsNullOrWhiteSpace).Select(_ => "recovery-disabled-subsystem-invalid").ToArray();

    private static RuntimeRecoveryDecision Reject(RuntimeFailureContext context, string code)
        => new(false, code, RuntimeRecoveryAction.Reject, true, false, false, false, context.OwnerSubsystem, context.RecordId);

    private static RuntimeRecoveryDecision Decision(RuntimeFailureContext context, string code, RuntimeRecoveryAction action,
        bool requiresRollback = false, bool disableSubsystem = false, bool stopWorldLoad = false)
        => new(true, code, action, true, requiresRollback, disableSubsystem, stopWorldLoad, context.OwnerSubsystem, context.RecordId);
}
