namespace RealmGate.Server;

public sealed record RuntimePeriodicSpellResolutionRequest(
    RuntimeSpellEffectTick Tick,
    string CombatInstanceId,
    string SourceActorId,
    string ResistanceTypeId,
    string SaveTypeId,
    int CasterLevel,
    int TargetLevel,
    int TargetAttribute,
    int TargetResistanceBasisPoints,
    int TargetImmunityBasisPoints,
    int TargetAbsorptionBasisPoints,
    ulong RandomSeed);

public sealed record RuntimePeriodicSpellResolutionCommit(
    RuntimeSpellEffectTick Tick,
    RuntimeResistanceSavingThrowTrace Resolution,
    RuntimeCombatSpellMutation? Mutation,
    bool Suppressed,
    bool Absorbed,
    bool SecondaryEffectPrevented,
    bool Committed);

public sealed record RuntimeResolvedPeriodicAdvanceResult(
    RuntimeSpellEffectAdvanceResult Advance,
    IReadOnlyList<RuntimePeriodicSpellResolutionCommit> Commits,
    bool Committed);

public sealed class RuntimeResolvedPeriodicSpellExecution
{
    private readonly RuntimeSpellEffectLifecycle _lifecycle;
    private readonly IReadOnlyDictionary<string, RuntimeSpellEffectExecutionPolicy> _policies;
    private readonly RuntimeSpellResistanceSavingThrow _resolver;
    private readonly IRuntimeCombatSpellMutationSink _sink;

    public RuntimeResolvedPeriodicSpellExecution(
        RuntimeSpellEffectLifecycle lifecycle,
        IEnumerable<RuntimeSpellEffectExecutionPolicy> policies,
        RuntimeSpellResistanceSavingThrow resolver,
        IRuntimeCombatSpellMutationSink sink)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        var values = (policies ?? throw new ArgumentNullException(nameof(policies))).ToArray();
        _policies = values.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        if (_policies.Count != values.Length) throw new InvalidOperationException("PERIODIC_RESOLUTION_DUPLICATE_POLICY");
    }

    public RuntimeResolvedPeriodicAdvanceResult AdvanceAndCommit(
        long worldTick,
        Func<RuntimeSpellEffectTick, RuntimePeriodicSpellResolutionRequest> requestFactory)
    {
        ArgumentNullException.ThrowIfNull(requestFactory);
        var lifecycleSnapshot = _lifecycle.CaptureSnapshot();
        var targetSnapshots = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var commits = new List<RuntimePeriodicSpellResolutionCommit>();
        try
        {
            var advance = _lifecycle.Advance(worldTick);
            foreach (var tick in advance.Ticks)
            {
                if (!_policies.TryGetValue(tick.SpellId, out var policy))
                    throw new InvalidOperationException("PERIODIC_RESOLUTION_POLICY_REQUIRED");
                if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
                    throw new InvalidOperationException("PERIODIC_RESOLUTION_POLICY_NOT_PROMOTED");
                var request = requestFactory(tick) ?? throw new InvalidOperationException("PERIODIC_RESOLUTION_REQUEST_REQUIRED");
                ValidateRequest(tick, request);
                var trace = _resolver.Resolve(new RuntimeResistanceSavingThrowRequest(
                    $"resistsave.{tick.EffectId}.{tick.WorldTick}", request.CombatInstanceId, tick.WorldTick,
                    request.SourceActorId, tick.TargetId, tick.SpellId, request.ResistanceTypeId, request.SaveTypeId,
                    request.CasterLevel, request.TargetLevel, request.TargetAttribute,
                    request.TargetResistanceBasisPoints, request.TargetImmunityBasisPoints,
                    request.TargetAbsorptionBasisPoints, request.RandomSeed));

                var suppressed = trace.ResistanceOutcome == RuntimeResistanceOutcome.Immune ||
                    trace.SavingThrowEffect == RuntimeSavingThrowEffect.Negate;
                var absorbed = trace.ResistanceOutcome == RuntimeResistanceOutcome.Absorbed &&
                    policy.TickDisposition == RuntimeDamageDisposition.Damage;
                RuntimeCombatSpellMutation? mutation = null;
                if (!suppressed)
                {
                    if (!targetSnapshots.ContainsKey(tick.TargetId))
                        targetSnapshots[tick.TargetId] = _sink.Capture(tick.TargetId);
                    var raw = checked(tick.Magnitude * tick.StackCount);
                    var afterResistance = ScaleSigned(raw, trace.ResistanceMagnitudeBasisPoints);
                    var afterSave = ScaleReduction(afterResistance, trace.MagnitudeReductionBasisPoints);
                    var disposition = absorbed ? RuntimeDamageDisposition.Healing : policy.TickDisposition;
                    mutation = new RuntimeCombatSpellMutation(
                        $"mutation:{tick.EffectId}:resolved:{tick.WorldTick}", tick.TargetId, disposition,
                        Math.Max(0, afterSave), tick.SpellId, $"effect:{tick.EffectId}:tick:{tick.WorldTick}");
                    _sink.Apply(mutation);
                }
                commits.Add(new RuntimePeriodicSpellResolutionCommit(tick, trace, mutation, suppressed, absorbed,
                    trace.SecondaryEffectPrevented, true));
            }
            return new RuntimeResolvedPeriodicAdvanceResult(advance, commits, true);
        }
        catch
        {
            foreach (var pair in targetSnapshots.Reverse()) _sink.Restore(pair.Key, pair.Value);
            _lifecycle.RestoreSnapshot(lifecycleSnapshot);
            throw;
        }
    }

    public RuntimeSpellEffectInstance ApplyResolvedStatus(
        RuntimeSpellEffectApplication application,
        RuntimeResistanceSavingThrowTrace resolution)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(resolution);
        if (!application.SpellId.Equals(resolution.SpellId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("STATUS_RESOLUTION_SPELL_MISMATCH");
        if (resolution.ResistanceOutcome == RuntimeResistanceOutcome.Immune ||
            resolution.SavingThrowEffect == RuntimeSavingThrowEffect.Negate ||
            resolution.SecondaryEffectPrevented)
            throw new InvalidOperationException("STATUS_APPLICATION_SUPPRESSED");
        if (resolution.DurationReductionBasisPoints != 0)
            throw new InvalidOperationException("STATUS_DURATION_POLICY_REQUIRED");
        var magnitude = ScaleReduction(
            ScaleSigned(application.Magnitude, resolution.ResistanceMagnitudeBasisPoints),
            resolution.MagnitudeReductionBasisPoints);
        if (magnitude <= 0) throw new InvalidOperationException("STATUS_MAGNITUDE_SUPPRESSED");
        return _lifecycle.Apply(application with { Magnitude = magnitude });
    }

    private static int ScaleSigned(int value, int basisPoints)
    {
        var factor = checked(10000 - basisPoints);
        return checked((int)(((long)value * factor) / 10000));
    }

    private static int ScaleReduction(int value, int basisPoints)
    {
        if (basisPoints is < 0 or > 10000) throw new InvalidOperationException("PERIODIC_SAVE_REDUCTION_INVALID");
        return checked((int)(((long)value * (10000 - basisPoints)) / 10000));
    }

    private static void ValidateRequest(RuntimeSpellEffectTick tick, RuntimePeriodicSpellResolutionRequest request)
    {
        if (request.Tick != tick) throw new InvalidOperationException("PERIODIC_RESOLUTION_TICK_MISMATCH");
        if (!request.CombatInstanceId.StartsWith("combat.", StringComparison.OrdinalIgnoreCase) ||
            !(request.SourceActorId.StartsWith("character.", StringComparison.OrdinalIgnoreCase) ||
              request.SourceActorId.StartsWith("npc.", StringComparison.OrdinalIgnoreCase)) ||
            !request.ResistanceTypeId.StartsWith("resistance.", StringComparison.OrdinalIgnoreCase) ||
            !request.SaveTypeId.StartsWith("save.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PERIODIC_RESOLUTION_CANONICAL_REFERENCE_INVALID");
        if (request.CasterLevel < 0 || request.TargetLevel < 0 || request.TargetAttribute < 0)
            throw new InvalidOperationException("PERIODIC_RESOLUTION_INPUT_INVALID");
    }
}
