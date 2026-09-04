namespace RealmGate.Server;

public sealed record RuntimeResolvedSpellExecutionRequest(
    RuntimeResistanceSavingThrowRequest ResolutionRequest,
    RuntimeDirectSpellExecutionRequest DirectExecutionRequest,
    bool HasSecondaryEffect,
    int BaseSecondaryDurationTicks);

public sealed record RuntimeResolvedSpellExecutionTrace(
    RuntimeResistanceSavingThrowTrace ResistanceSavingThrow,
    RuntimeDirectSpellExecutionTrace DirectExecution,
    bool SecondaryEffectSuppressed,
    int SecondaryDurationTicks,
    bool Absorbed,
    bool Immune,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeResolvedSpellExecution
{
    private readonly RuntimeSpellResistanceSavingThrow _resolver;
    private readonly RuntimeDirectSpellExecution _direct;
    public RuntimeResolvedSpellExecution(RuntimeSpellResistanceSavingThrow resolver, RuntimeDirectSpellExecution direct)
    { _resolver=resolver??throw new ArgumentNullException(nameof(resolver)); _direct=direct??throw new ArgumentNullException(nameof(direct)); }

    public RuntimeResolvedSpellExecutionTrace Execute(RuntimeResolvedSpellExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var decision=_resolver.Resolve(request.ResolutionRequest);
        var direct=_direct.Execute(request.DirectExecutionRequest,decision);
        var suppressed=request.HasSecondaryEffect && decision.SecondaryEffectPrevented;
        var duration=suppressed ? 0 : ReduceDuration(request.BaseSecondaryDurationTicks,decision.DurationReductionBasisPoints);
        return new(decision,direct,suppressed,duration,
            decision.ResistanceOutcome==RuntimeResistanceOutcome.Absorbed,
            decision.ResistanceOutcome==RuntimeResistanceOutcome.Immune,
            decision.EvidenceReferences.Concat(direct.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static int ReduceDuration(int duration,int basisPoints)
    {
        if(duration<0||basisPoints is <0 or >10000) throw new InvalidOperationException("RESOLVED_SPELL_DURATION_INVALID");
        return checked(duration-(int)((long)duration*basisPoints/10000));
    }
    private static void Validate(RuntimeResolvedSpellExecutionRequest r)
    {
        var a=r.ResolutionRequest; var b=r.DirectExecutionRequest;
        if(!a.SpellId.Equals(b.SpellId,StringComparison.OrdinalIgnoreCase)||
           !a.CombatInstanceId.Equals(b.CombatInstanceId,StringComparison.OrdinalIgnoreCase)||
           !a.CasterId.Equals(b.CasterId,StringComparison.OrdinalIgnoreCase)||
           !a.TargetId.Equals(b.TargetId,StringComparison.OrdinalIgnoreCase)||a.WorldTick!=b.WorldTick)
            throw new InvalidOperationException("RESOLVED_SPELL_REQUEST_MISMATCH");
        if(r.BaseSecondaryDurationTicks<0) throw new InvalidOperationException("RESOLVED_SPELL_DURATION_INVALID");
    }
}
