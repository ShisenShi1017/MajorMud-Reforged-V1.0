namespace RealmGate.Server;

public enum RuntimeSpellEffectPolarity { Beneficial, Harmful }

public sealed record RuntimeSpellEffectExecutionPolicy(
    string PolicyId,
    string SpellId,
    string EffectKind,
    RuntimeDamageDisposition TickDisposition,
    RuntimeSpellEffectPolarity Polarity,
    string DispelCategory,
    int DispelPriority,
    IReadOnlyList<string> EvidenceSources,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeSpellEffectTickCommit(
    RuntimeSpellEffectTick Tick,
    RuntimeCombatSpellMutation Mutation,
    bool Committed);

public sealed record RuntimeSpellEffectExecutionResult(
    RuntimeSpellEffectAdvanceResult Advance,
    IReadOnlyList<RuntimeSpellEffectTickCommit> Commits,
    bool Committed);

public sealed record RuntimeSpellDispelRequest(
    string DispelId,
    string SourceActorId,
    string TargetId,
    long WorldTick,
    string Category,
    RuntimeSpellEffectPolarity? Polarity,
    int MaximumEffects);

public sealed record RuntimeSpellDispelResult(
    string DispelId,
    IReadOnlyList<RuntimeSpellEffectInstance> Removed,
    bool Committed);

public sealed class RuntimeCombatSpellEffectExecution
{
    private readonly RuntimeSpellEffectLifecycle _lifecycle;
    private readonly IReadOnlyDictionary<string, RuntimeSpellEffectExecutionPolicy> _policies;
    private readonly IRuntimeCombatSpellMutationSink _sink;

    public RuntimeCombatSpellEffectExecution(RuntimeSpellEffectLifecycle lifecycle,
        IEnumerable<RuntimeSpellEffectExecutionPolicy> policies,
        IRuntimeCombatSpellMutationSink sink)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _policies = (policies ?? throw new ArgumentNullException(nameof(policies)))
            .ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        foreach (var policy in _policies.Values) ValidatePolicy(policy);
    }

    public RuntimeSpellEffectExecutionResult AdvanceAndCommit(long worldTick)
    {
        var lifecycleSnapshot = _lifecycle.CaptureSnapshot();
        var targetSnapshots = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var commits = new List<RuntimeSpellEffectTickCommit>();
        try
        {
            var advance = _lifecycle.Advance(worldTick);
            foreach (var tick in advance.Ticks)
            {
                if (!_policies.TryGetValue(tick.SpellId, out var policy))
                    throw new InvalidOperationException("SPELL_EFFECT_EXECUTION_POLICY_REQUIRED");
                if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
                    throw new InvalidOperationException("SPELL_EFFECT_EXECUTION_POLICY_NOT_PROMOTED");
                if (!targetSnapshots.ContainsKey(tick.TargetId))
                    targetSnapshots[tick.TargetId] = _sink.Capture(tick.TargetId);
                var mutation = new RuntimeCombatSpellMutation(
                    $"mutation:{tick.EffectId}:tick:{tick.WorldTick}", tick.TargetId,
                    policy.TickDisposition, checked(tick.Magnitude * tick.StackCount),
                    tick.SpellId, $"effect:{tick.EffectId}:tick:{tick.WorldTick}");
                _sink.Apply(mutation);
                commits.Add(new RuntimeSpellEffectTickCommit(tick, mutation, true));
            }
            return new RuntimeSpellEffectExecutionResult(advance, commits, true);
        }
        catch
        {
            foreach (var pair in targetSnapshots.Reverse()) _sink.Restore(pair.Key, pair.Value);
            _lifecycle.RestoreSnapshot(lifecycleSnapshot);
            throw;
        }
    }

    public RuntimeSpellDispelResult Dispel(RuntimeSpellDispelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateDispelRequest(request);
        if (request.WorldTick != _lifecycle.WorldTick)
            throw new InvalidOperationException("SPELL_DISPEL_TICK_MISMATCH");
        var snapshot = _lifecycle.CaptureSnapshot();
        try
        {
            var selected = _lifecycle.Active
                .Where(x => x.TargetId.Equals(request.TargetId, StringComparison.OrdinalIgnoreCase))
                .Select(x => (Effect:x, Policy:RequirePolicy(x.SpellId)))
                .Where(x => request.Category == "*" || x.Policy.DispelCategory.Equals(request.Category, StringComparison.OrdinalIgnoreCase))
                .Where(x => !request.Polarity.HasValue || x.Policy.Polarity == request.Polarity.Value)
                .OrderByDescending(x => x.Policy.DispelPriority)
                .ThenBy(x => x.Effect.AppliedTick)
                .ThenBy(x => x.Effect.EffectId, StringComparer.OrdinalIgnoreCase)
                .Take(request.MaximumEffects).ToArray();
            var removed = new List<RuntimeSpellEffectInstance>();
            foreach (var item in selected)
                removed.Add(_lifecycle.Remove(item.Effect.EffectId, request.WorldTick, $"dispel:{request.DispelId}"));
            return new RuntimeSpellDispelResult(request.DispelId, removed, true);
        }
        catch
        {
            _lifecycle.RestoreSnapshot(snapshot);
            throw;
        }
    }

    private RuntimeSpellEffectExecutionPolicy RequirePolicy(string spellId)
    {
        if (!_policies.TryGetValue(spellId, out var policy))
            throw new InvalidOperationException("SPELL_EFFECT_EXECUTION_POLICY_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("SPELL_EFFECT_EXECUTION_POLICY_NOT_PROMOTED");
        return policy;
    }

    private static void ValidatePolicy(RuntimeSpellEffectExecutionPolicy policy)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(policy.SpellId, @"^spell\.\d+$"))
            throw new InvalidOperationException("SPELL_EFFECT_EXECUTION_SPELL_ID_INVALID");
        if (string.IsNullOrWhiteSpace(policy.DispelCategory))
            throw new InvalidOperationException("SPELL_EFFECT_DISPEL_CATEGORY_REQUIRED");
        if (policy.DispelPriority < 0)
            throw new InvalidOperationException("SPELL_EFFECT_DISPEL_PRIORITY_INVALID");
        if (policy.EvidenceSources.Count == 0)
            throw new InvalidOperationException("SPELL_EFFECT_EXECUTION_EVIDENCE_REQUIRED");
        if (!policy.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SPELL_EFFECT_EXECUTION_SAFE_FALLBACK_REQUIRED");
    }

    private static void ValidateDispelRequest(RuntimeSpellDispelRequest request)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.DispelId, @"^dispel\.[A-Za-z0-9._-]+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.SourceActorId, @"^(character|npc)\.\d+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.TargetId, @"^(character|npc)\.\d+$"))
            throw new InvalidOperationException("SPELL_DISPEL_CANONICAL_REFERENCE_INVALID");
        if (request.WorldTick < 0) throw new InvalidOperationException("SPELL_DISPEL_TICK_INVALID");
        if (string.IsNullOrWhiteSpace(request.Category)) throw new InvalidOperationException("SPELL_DISPEL_CATEGORY_REQUIRED");
        if (request.MaximumEffects <= 0) throw new InvalidOperationException("SPELL_DISPEL_MAXIMUM_EFFECTS_INVALID");
    }
}
