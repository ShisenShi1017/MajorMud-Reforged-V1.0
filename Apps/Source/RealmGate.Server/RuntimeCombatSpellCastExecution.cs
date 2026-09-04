namespace RealmGate.Server;

public sealed record RuntimeSpellTargetResolutionInput(
    string TargetId,
    int TargetResistance,
    int TargetSavingThrow);

public sealed record RuntimeSpellCastExecutionRequest(
    string CastId,
    string CombatInstanceId,
    long WorldTick,
    RuntimeSpellTargetRequest TargetRequest,
    IReadOnlyDictionary<string, RuntimeSpellTargetResolutionInput> TargetInputs,
    RuntimeDamageDisposition Disposition,
    int ActorPower,
    ulong RandomSeed);

public sealed record RuntimeSpellCastExecutionResult(
    RuntimeSpellCastInstance Cast,
    RuntimeSpellTargetDecision TargetDecision,
    IReadOnlyList<RuntimeCombatSpellCommitResult> Commits,
    bool Committed);

public sealed class RuntimeSpellCastExecution
{
    private readonly RuntimeSpellCastLifecycle _casts;
    private readonly RuntimeSpellTargeting _targeting;
    private readonly RuntimeCombatSpellBoundResolver _resolver;
    private readonly IRuntimeCombatSpellMutationSink _sink;

    public RuntimeSpellCastExecution(RuntimeSpellCastLifecycle casts, RuntimeSpellTargeting targeting,
        RuntimeCombatSpellBoundResolver resolver, IRuntimeCombatSpellMutationSink sink)
    {
        _casts = casts ?? throw new ArgumentNullException(nameof(casts));
        _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public RuntimeSpellCastExecutionResult Execute(RuntimeSpellCastExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cast = _casts.Get(request.CastId);
        if (cast.State != RuntimeSpellCastState.ReadyToComplete)
            throw new InvalidOperationException("SPELL_CAST_EXECUTION_NOT_READY");
        if (!cast.SpellId.Equals(request.TargetRequest.SpellId, StringComparison.OrdinalIgnoreCase) ||
            !cast.CastId.Equals(request.TargetRequest.CastId, StringComparison.OrdinalIgnoreCase) ||
            !cast.ActorId.Equals(request.TargetRequest.CasterId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SPELL_CAST_EXECUTION_REQUEST_MISMATCH");
        if (request.WorldTick < 0 || request.WorldTick > int.MaxValue)
            throw new InvalidOperationException("SPELL_CAST_EXECUTION_TICK_INVALID");

        RuntimeSpellTargetDecision decision;
        try { decision = _targeting.Resolve(request.TargetRequest); }
        catch
        {
            _casts.Fail(request.CastId, request.WorldTick, "target_validation_failed");
            throw;
        }

        var snapshots = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var commits = new List<RuntimeCombatSpellCommitResult>();
        try
        {
            foreach (var targetId in decision.TargetIds)
            {
                if (!request.TargetInputs.TryGetValue(targetId, out var input) ||
                    !input.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("SPELL_CAST_TARGET_INPUT_REQUIRED");
                snapshots[targetId] = _sink.Capture(targetId);
                var ordinal = commits.Count;
                var trace = _resolver.Resolve(new RuntimeCombatSpellBoundRequest(
                    $"{request.CastId}:target:{ordinal}", request.CombatInstanceId, checked((int)request.WorldTick),
                    cast.ActorId, targetId, cast.SpellId, request.Disposition, request.ActorPower,
                    input.TargetResistance, input.TargetSavingThrow, DeriveSeed(request.RandomSeed, ordinal)));
                var mutation = new RuntimeCombatSpellMutation($"mutation:{trace.ResolutionId}", targetId,
                    request.Disposition, trace.FinalMagnitude, cast.SpellId, trace.ResolutionId);
                _sink.Apply(mutation);
                commits.Add(new RuntimeCombatSpellCommitResult(trace, mutation, true));
            }
            var completed = _casts.Complete(request.CastId, request.WorldTick);
            return new RuntimeSpellCastExecutionResult(completed, decision, commits, true);
        }
        catch
        {
            foreach (var pair in snapshots.Reverse()) _sink.Restore(pair.Key, pair.Value);
            if (_casts.Get(request.CastId).State == RuntimeSpellCastState.ReadyToComplete)
                _casts.Fail(request.CastId, request.WorldTick, "effect_execution_failed");
            throw;
        }
    }

    private static ulong DeriveSeed(ulong seed, int ordinal) =>
        unchecked(seed ^ (0x9E3779B97F4A7C15UL * (ulong)(ordinal + 1)));
}
