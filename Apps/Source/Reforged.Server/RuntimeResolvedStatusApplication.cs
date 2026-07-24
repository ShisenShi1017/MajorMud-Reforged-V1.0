namespace Reforged.Server;

public enum RuntimeStatusDurationRounding
{
    TowardZero,
    Floor,
    Ceiling
}

public sealed record RuntimeStatusDurationPolicy(
    string PolicyId,
    string SpellId,
    RuntimeStatusDurationRounding Rounding,
    int MinimumDurationTicks,
    RuntimeResolutionConfidence Confidence,
    IReadOnlyList<string> EvidenceReferences,
    string SafeFallback);

public sealed record RuntimeResolvedStatusApplicationTrace(
    string EffectId,
    string SpellId,
    int BaseDurationTicks,
    int DurationReductionBasisPoints,
    int AdjustedDurationTicks,
    RuntimeSpellEffectStackingMode StackingMode,
    bool RefreshedExisting,
    bool ReplacedExisting,
    RuntimeSpellEffectInstance Instance,
    IReadOnlyList<string> EvidenceReferences);

public sealed class RuntimeResolvedStatusApplication
{
    private readonly RuntimeSpellEffectLifecycle _lifecycle;
    private readonly IReadOnlyDictionary<string, RuntimeSpellEffectPolicy> _effectPolicies;
    private readonly IReadOnlyDictionary<string, RuntimeStatusDurationPolicy> _durationPolicies;

    public RuntimeResolvedStatusApplication(
        RuntimeSpellEffectLifecycle lifecycle,
        IEnumerable<RuntimeSpellEffectPolicy> effectPolicies,
        IEnumerable<RuntimeStatusDurationPolicy> durationPolicies)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        var effects = (effectPolicies ?? throw new ArgumentNullException(nameof(effectPolicies))).ToArray();
        var durations = (durationPolicies ?? throw new ArgumentNullException(nameof(durationPolicies))).ToArray();
        _effectPolicies = effects.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        _durationPolicies = durations.ToDictionary(x => x.SpellId, StringComparer.OrdinalIgnoreCase);
        if (_effectPolicies.Count != effects.Length) throw new InvalidOperationException("STATUS_EFFECT_DUPLICATE_POLICY");
        if (_durationPolicies.Count != durations.Length) throw new InvalidOperationException("STATUS_DURATION_DUPLICATE_POLICY");
        foreach (var policy in durations) Validate(policy);
    }

    public RuntimeResolvedStatusApplicationTrace Apply(
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
        if (!_effectPolicies.TryGetValue(application.SpellId, out var effectPolicy))
            throw new InvalidOperationException("STATUS_EFFECT_POLICY_REQUIRED");
        if (!_durationPolicies.TryGetValue(application.SpellId, out var durationPolicy))
            throw new InvalidOperationException("STATUS_DURATION_POLICY_REQUIRED");
        if (effectPolicy.Confidence == RuntimeResolutionConfidence.WeakHypothesis ||
            durationPolicy.Confidence == RuntimeResolutionConfidence.WeakHypothesis)
            throw new InvalidOperationException("STATUS_POLICY_NOT_PROMOTED");

        var magnitude = ScaleMagnitude(application.Magnitude, resolution.ResistanceMagnitudeBasisPoints,
            resolution.MagnitudeReductionBasisPoints);
        if (magnitude <= 0) throw new InvalidOperationException("STATUS_MAGNITUDE_SUPPRESSED");
        var adjustedDuration = ScaleDuration(effectPolicy.DurationTicks,
            resolution.DurationReductionBasisPoints, durationPolicy);
        var matchingBefore = _lifecycle.Active.Count(x =>
            x.TargetId.Equals(application.TargetId, StringComparison.OrdinalIgnoreCase) &&
            x.SpellId.Equals(application.SpellId, StringComparison.OrdinalIgnoreCase));
        var instance = _lifecycle.Apply(application with { Magnitude = magnitude }, adjustedDuration);
        return new RuntimeResolvedStatusApplicationTrace(
            application.EffectId, application.SpellId, effectPolicy.DurationTicks,
            resolution.DurationReductionBasisPoints, adjustedDuration, effectPolicy.StackingMode,
            matchingBefore > 0 && effectPolicy.StackingMode == RuntimeSpellEffectStackingMode.Refresh,
            matchingBefore > 0 && effectPolicy.StackingMode == RuntimeSpellEffectStackingMode.Replace,
            instance, effectPolicy.EvidenceSources.Concat(durationPolicy.EvidenceReferences)
                .Concat(resolution.EvidenceReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static int ScaleMagnitude(int value, int resistanceBasisPoints, int saveBasisPoints)
    {
        if (saveBasisPoints is < 0 or > 10000) throw new InvalidOperationException("STATUS_SAVE_REDUCTION_INVALID");
        var afterResistance = checked((int)(((long)value * (10000 - resistanceBasisPoints)) / 10000));
        return checked((int)(((long)afterResistance * (10000 - saveBasisPoints)) / 10000));
    }

    private static int ScaleDuration(int baseTicks, int reductionBasisPoints, RuntimeStatusDurationPolicy policy)
    {
        if (reductionBasisPoints is < 0 or > 10000)
            throw new InvalidOperationException("STATUS_DURATION_REDUCTION_INVALID");
        var numerator = checked((long)baseTicks * (10000 - reductionBasisPoints));
        var raw = policy.Rounding switch
        {
            RuntimeStatusDurationRounding.Floor => (int)Math.Floor((decimal)numerator / 10000),
            RuntimeStatusDurationRounding.Ceiling => (int)Math.Ceiling((decimal)numerator / 10000),
            _ => (int)(numerator / 10000)
        };
        return Math.Max(policy.MinimumDurationTicks, raw);
    }

    private static void Validate(RuntimeStatusDurationPolicy policy)
    {
        if (!policy.PolicyId.StartsWith("statusduration.", StringComparison.OrdinalIgnoreCase) ||
            !System.Text.RegularExpressions.Regex.IsMatch(policy.SpellId, @"^spell\.\d+$"))
            throw new InvalidOperationException("STATUS_DURATION_CANONICAL_ID_INVALID");
        if (policy.MinimumDurationTicks <= 0) throw new InvalidOperationException("STATUS_DURATION_MINIMUM_INVALID");
        if (policy.EvidenceReferences.Count == 0) throw new InvalidOperationException("STATUS_DURATION_EVIDENCE_REQUIRED");
        if (!policy.SafeFallback.Equals("reject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("STATUS_DURATION_SAFE_FALLBACK_REQUIRED");
    }
}
