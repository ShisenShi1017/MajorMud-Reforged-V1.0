namespace Reforged.Server;

public enum RuntimeSpellEffectStackingMode
{
    Reject,
    Refresh,
    Replace,
    Independent
}

public enum RuntimeSpellEffectState
{
    Active,
    Expired,
    Removed
}

public sealed record RuntimeSpellEffectPolicy(
    string PolicyId,
    string SpellId,
    string EffectKind,
    int DurationTicks,
    int TickIntervalTicks,
    RuntimeSpellEffectStackingMode StackingMode,
    int MaximumStacks,
    bool RemoveOnDeath,
    bool PersistAcrossLogout,
    IReadOnlyList<string> EvidenceSources,
    RuntimeResolutionConfidence Confidence,
    string SafeFallback);

public sealed record RuntimeSpellEffectApplication(
    string EffectId,
    string SourceCastId,
    string SourceActorId,
    string TargetId,
    string SpellId,
    int Magnitude,
    long AppliedTick);

public sealed record RuntimeSpellEffectInstance(
    string EffectId,
    string SourceCastId,
    string SourceActorId,
    string TargetId,
    string SpellId,
    string EffectKind,
    int Magnitude,
    int StackCount,
    long AppliedTick,
    long ExpiresTick,
    long? NextTick,
    RuntimeSpellEffectState State,
    string? TerminalReason);

public sealed record RuntimeSpellEffectTick(
    string EffectId,
    string TargetId,
    string SpellId,
    string EffectKind,
    int Magnitude,
    int StackCount,
    long WorldTick);

public sealed record RuntimeSpellEffectAdvanceResult(
    IReadOnlyList<RuntimeSpellEffectTick> Ticks,
    IReadOnlyList<RuntimeSpellEffectInstance> Expired);

public sealed record RuntimeSpellEffectLifecycleSnapshot(
    long WorldTick,
    IReadOnlyDictionary<string, RuntimeSpellEffectInstance> Effects);

public sealed class RuntimeSpellEffectLifecycle
{
    private readonly IReadOnlyDictionary<string, RuntimeSpellEffectPolicy> _policies;
    private readonly Dictionary<string, RuntimeSpellEffectInstance> _effects =
        new(StringComparer.OrdinalIgnoreCase);
    private long _worldTick;

    public RuntimeSpellEffectLifecycle(IEnumerable<RuntimeSpellEffectPolicy> policies, long initialWorldTick = 0)
    {
        if (initialWorldTick < 0) throw new ArgumentOutOfRangeException(nameof(initialWorldTick));
        _worldTick = initialWorldTick;
        _policies = (policies ?? throw new ArgumentNullException(nameof(policies)))
            .ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        foreach (var policy in _policies.Values) ValidatePolicy(policy);
    }

    public long WorldTick => _worldTick;
    public IReadOnlyCollection<RuntimeSpellEffectInstance> Active =>
        _effects.Values.Where(x => x.State == RuntimeSpellEffectState.Active)
            .OrderBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase).ToArray();

    public RuntimeSpellEffectInstance Apply(RuntimeSpellEffectApplication application) =>
        Apply(application, null);

    public RuntimeSpellEffectInstance Apply(RuntimeSpellEffectApplication application, int? durationTicksOverride)
    {
        ArgumentNullException.ThrowIfNull(application);
        ValidateCanonical(application);
        if (application.AppliedTick != _worldTick)
            throw new InvalidOperationException("SPELL_EFFECT_APPLICATION_TICK_MISMATCH");
        if (!_policies.TryGetValue(application.SpellId, out var policy))
            throw new InvalidOperationException("SPELL_EFFECT_POLICY_REQUIRED");
        if (policy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("SPELL_EFFECT_POLICY_NOT_PROMOTED");
        if (_effects.ContainsKey(application.EffectId))
            throw new InvalidOperationException("SPELL_EFFECT_ID_DUPLICATE");
        var effectiveDurationTicks = durationTicksOverride ?? policy.DurationTicks;
        if (effectiveDurationTicks <= 0 || effectiveDurationTicks > policy.DurationTicks)
            throw new InvalidOperationException("SPELL_EFFECT_DURATION_OVERRIDE_INVALID");

        var matching = _effects.Values.Where(x => x.State == RuntimeSpellEffectState.Active &&
            x.TargetId.Equals(application.TargetId, StringComparison.OrdinalIgnoreCase) &&
            x.SpellId.Equals(application.SpellId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase).ToArray();

        if (matching.Length > 0)
        {
            if (policy.StackingMode == RuntimeSpellEffectStackingMode.Reject)
                throw new InvalidOperationException("SPELL_EFFECT_STACK_REJECTED");
            if (policy.StackingMode == RuntimeSpellEffectStackingMode.Refresh)
            {
                var current = matching[0];
                var refreshed = current with {
                    Magnitude = application.Magnitude,
                    AppliedTick = _worldTick,
                    ExpiresTick = checked(_worldTick + effectiveDurationTicks),
                    NextTick = policy.TickIntervalTicks > 0 ? checked(_worldTick + policy.TickIntervalTicks) : null,
                    TerminalReason = null
                };
                _effects[current.EffectId] = refreshed;
                return refreshed;
            }
            if (policy.StackingMode == RuntimeSpellEffectStackingMode.Replace)
                foreach (var current in matching) Terminate(current.EffectId, RuntimeSpellEffectState.Removed, "replaced");
            if (policy.StackingMode == RuntimeSpellEffectStackingMode.Independent &&
                matching.Length >= policy.MaximumStacks)
                throw new InvalidOperationException("SPELL_EFFECT_MAXIMUM_STACKS");
        }

        var instance = new RuntimeSpellEffectInstance(
            application.EffectId, application.SourceCastId, application.SourceActorId,
            application.TargetId, application.SpellId, policy.EffectKind, application.Magnitude,
            1, _worldTick, checked(_worldTick + effectiveDurationTicks),
            policy.TickIntervalTicks > 0 ? checked(_worldTick + policy.TickIntervalTicks) : null,
            RuntimeSpellEffectState.Active, null);
        _effects.Add(instance.EffectId, instance);
        return instance;
    }

    public RuntimeSpellEffectAdvanceResult Advance(long worldTick)
    {
        if (worldTick < _worldTick) throw new InvalidOperationException("SPELL_EFFECT_TICK_REGRESSION");
        _worldTick = worldTick;
        var ticks = new List<RuntimeSpellEffectTick>();
        var expired = new List<RuntimeSpellEffectInstance>();

        foreach (var id in _effects.Values.Where(x => x.State == RuntimeSpellEffectState.Active)
            .OrderBy(x => x.NextTick ?? long.MaxValue).ThenBy(x => x.ExpiresTick)
            .ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase).Select(x => x.EffectId).ToArray())
        {
            var effect = _effects[id];
            var policy = _policies[effect.SpellId];
            while (effect.NextTick.HasValue && effect.NextTick.Value <= _worldTick &&
                   effect.NextTick.Value < effect.ExpiresTick)
            {
                ticks.Add(new RuntimeSpellEffectTick(effect.EffectId, effect.TargetId, effect.SpellId,
                    effect.EffectKind, effect.Magnitude, effect.StackCount, effect.NextTick.Value));
                effect = effect with { NextTick = checked(effect.NextTick.Value + policy.TickIntervalTicks) };
                _effects[id] = effect;
            }
            if (effect.ExpiresTick <= _worldTick)
                expired.Add(Terminate(id, RuntimeSpellEffectState.Expired, "duration_elapsed"));
        }
        return new RuntimeSpellEffectAdvanceResult(ticks, expired);
    }

    public RuntimeSpellEffectInstance Remove(string effectId, long worldTick, string reason)
    {
        if (worldTick != _worldTick) throw new InvalidOperationException("SPELL_EFFECT_REMOVAL_TICK_MISMATCH");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("reason required", nameof(reason));
        return Terminate(effectId, RuntimeSpellEffectState.Removed, reason);
    }

    public IReadOnlyList<RuntimeSpellEffectInstance> HandleTargetDeath(string targetId, long worldTick)
    {
        if (worldTick != _worldTick) throw new InvalidOperationException("SPELL_EFFECT_DEATH_TICK_MISMATCH");
        var removed = new List<RuntimeSpellEffectInstance>();
        foreach (var effect in Active.Where(x => x.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase)).ToArray())
            if (_policies[effect.SpellId].RemoveOnDeath)
                removed.Add(Terminate(effect.EffectId, RuntimeSpellEffectState.Removed, "target_death"));
        return removed;
    }

    public RuntimeSpellEffectLifecycleSnapshot CaptureSnapshot() =>
        new(_worldTick, _effects.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));

    public void RestoreSnapshot(RuntimeSpellEffectLifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.WorldTick < 0) throw new InvalidOperationException("SPELL_EFFECT_SNAPSHOT_TICK_INVALID");
        _effects.Clear();
        foreach (var pair in snapshot.Effects) _effects.Add(pair.Key, pair.Value);
        _worldTick = snapshot.WorldTick;
    }

    public RuntimeSpellEffectInstance Get(string effectId) =>
        _effects.TryGetValue(effectId, out var value) ? value :
        throw new KeyNotFoundException("SPELL_EFFECT_NOT_FOUND");

    private RuntimeSpellEffectInstance Terminate(string id, RuntimeSpellEffectState state, string reason)
    {
        var current = Get(id);
        if (current.State != RuntimeSpellEffectState.Active)
            throw new InvalidOperationException("SPELL_EFFECT_ALREADY_TERMINAL");
        var terminal = current with { State = state, NextTick = null, TerminalReason = reason };
        _effects[id] = terminal;
        return terminal;
    }

    private static void ValidatePolicy(RuntimeSpellEffectPolicy policy)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(policy.SpellId, @"^spell\.\d+$"))
            throw new InvalidOperationException("SPELL_EFFECT_POLICY_SPELL_ID_INVALID");
        if (policy.DurationTicks <= 0) throw new InvalidOperationException("SPELL_EFFECT_DURATION_UNRESOLVED");
        if (policy.TickIntervalTicks < 0 || policy.TickIntervalTicks > policy.DurationTicks)
            throw new InvalidOperationException("SPELL_EFFECT_TICK_INTERVAL_INVALID");
        if (policy.MaximumStacks <= 0) throw new InvalidOperationException("SPELL_EFFECT_MAXIMUM_STACKS_INVALID");
        if (policy.EvidenceSources.Count == 0) throw new InvalidOperationException("SPELL_EFFECT_EVIDENCE_REQUIRED");
        if (!policy.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SPELL_EFFECT_SAFE_FALLBACK_REQUIRED");
    }

    private static void ValidateCanonical(RuntimeSpellEffectApplication value)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value.EffectId, @"^effect\.\d+$"))
            throw new InvalidOperationException("SPELL_EFFECT_ID_INVALID");
        if (!System.Text.RegularExpressions.Regex.IsMatch(value.SourceCastId, @"^cast\.[A-Za-z0-9._-]+$"))
            throw new InvalidOperationException("SPELL_EFFECT_CAST_ID_INVALID");
        if (!System.Text.RegularExpressions.Regex.IsMatch(value.SourceActorId, @"^(character|npc)\.\d+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(value.TargetId, @"^(character|npc)\.\d+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(value.SpellId, @"^spell\.\d+$"))
            throw new InvalidOperationException("SPELL_EFFECT_CANONICAL_REFERENCE_INVALID");
        if (value.Magnitude < 0) throw new InvalidOperationException("SPELL_EFFECT_MAGNITUDE_INVALID");
    }
}
