namespace RealmGate.Server;

public sealed record RuntimeCharacterFlagState(
    string FlagId,
    string Scope,
    string Lifetime,
    bool Value,
    string EvidenceClassification,
    string? SourceRecordId = null,
    long? SourceByteOffset = null);

public sealed record RuntimeCharacterTimerState(
    string TimerId,
    long RemainingTicks,
    string TickDomain,
    string Lifetime,
    string EvidenceClassification,
    DateTimeOffset? ExpiresAtUtc = null,
    string? SourceRecordId = null);

public sealed record RuntimeCharacterStatusState(
    string EffectId,
    int Magnitude,
    long RemainingTicks,
    string StackPolicy,
    string Lifetime,
    string EvidenceClassification,
    string? SourceRecordId = null,
    string? SpellId = null,
    string? CasterId = null,
    DateTimeOffset? AppliedAtUtc = null,
    DateTimeOffset? ExpiresAtUtc = null,
    int? ProjectionOrdinal = null,
    string? ReplacementPolicy = null,
    string? TimerOwner = null);

public sealed record RuntimeCharacterProgressionState(
    IReadOnlyDictionary<string, int> ClassLevels,
    IReadOnlyDictionary<string, long> ClassExperience,
    IReadOnlyDictionary<string, int> PermanentStatAdjustments,
    IReadOnlyDictionary<string, int> PermanentAbilityAdjustments)
{
    public static RuntimeCharacterProgressionState Empty { get; } = new(
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
}

public sealed record RuntimeCharacterSemanticState(
    IReadOnlyList<RuntimeCharacterFlagState> Flags,
    IReadOnlyList<RuntimeCharacterTimerState> Timers,
    IReadOnlyList<RuntimeCharacterStatusState> StatusEffects,
    RuntimeCharacterProgressionState Progression,
    IReadOnlyDictionary<string, int> DerivedStats,
    IReadOnlyDictionary<string, int> Resistances,
    IReadOnlyDictionary<string, int> SavingThrows,
    IReadOnlyDictionary<string, string> RuntimeMarkers,
    RuntimeCharacterAlignmentState? Alignment = null)
{
    public static RuntimeCharacterSemanticState Empty { get; } = new(
        Array.Empty<RuntimeCharacterFlagState>(),
        Array.Empty<RuntimeCharacterTimerState>(),
        Array.Empty<RuntimeCharacterStatusState>(),
        RuntimeCharacterProgressionState.Empty,
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        RuntimeCharacterAlignmentResolver.CreateInitial());
}

public static class RuntimeCharacterRecordValidator
{
    public const int CurrentSaveVersion = 7;

    public static void Validate(RuntimeCharacterRecord character)
    {
        if (string.IsNullOrWhiteSpace(character.CharacterId)) throw new InvalidDataException("CharacterId is required.");
        if (string.IsNullOrWhiteSpace(character.AccountId)) throw new InvalidDataException("AccountId is required.");
        if (string.IsNullOrWhiteSpace(character.WorldId)) throw new InvalidDataException("WorldId is required.");
        if (string.IsNullOrWhiteSpace(character.RoomId)) throw new InvalidDataException("RoomId is required.");
        if (character.Level < 1) throw new InvalidDataException("Character level must be at least 1.");
        if (character.Experience < 0) throw new InvalidDataException("Character experience cannot be negative.");
        if (character.MaximumHitPoints < 1) throw new InvalidDataException("Maximum hit points must be at least 1.");
        if (character.HitPoints < 0 || character.HitPoints > character.MaximumHitPoints)
            throw new InvalidDataException("Hit points must be between zero and maximum hit points.");
        if (character.MaximumMana < 0 || character.Mana < 0 || character.Mana > character.MaximumMana)
            throw new InvalidDataException("Mana must be between zero and maximum mana.");
        if (character.SaveVersion < 1 || character.SaveVersion > CurrentSaveVersion)
            throw new InvalidDataException($"Unsupported character save version {character.SaveVersion}.");

        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var alignment = semantic.Alignment ?? RuntimeCharacterAlignmentResolver.CreateInitial();
        RuntimeCharacterCombatStatisticProjector.Validate(semantic, character.SaveVersion);
        var progression = semantic.Progression ?? RuntimeCharacterProgressionState.Empty;
        foreach (var pair in progression.ClassLevels)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1) throw new InvalidDataException("Character class progression contains an invalid level.");
        }
        foreach (var pair in progression.ClassExperience)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0) throw new InvalidDataException("Character class progression contains invalid experience.");
        }
        if (character.SaveVersion >= 7)
        {
            if (!semantic.RuntimeMarkers.TryGetValue(RuntimeCharacterProgressionResolver.ActiveClassMarker, out var activeClass) || string.IsNullOrWhiteSpace(activeClass))
                throw new InvalidDataException("Character progression active class marker is missing.");
            if (!progression.ClassLevels.TryGetValue(activeClass, out var activeLevel) || activeLevel != character.Level)
                throw new InvalidDataException("Character level diverges from active-class progression.");
            if (!progression.ClassExperience.TryGetValue(activeClass, out var activeExperience) || activeExperience > character.Experience)
                throw new InvalidDataException("Character active-class experience diverges from total experience.");
        }
        RequireEvidenceClassification(alignment.EvidenceClassification, "character alignment state");
        if (alignment.MinutesPlayedToday < 0) throw new InvalidDataException("Character alignment minutes played cannot be negative.");
        if (alignment.DailyReductionApplied < 0) throw new InvalidDataException("Character alignment daily reduction cannot be negative.");
        EnsureUnique(semantic.Flags.Select(value => value.FlagId), "character flag");
        EnsureUnique(semantic.Timers.Select(value => value.TimerId), "character timer");
        EnsureUnique(semantic.StatusEffects.Select(value => value.EffectId), "character status effect");

        foreach (var timer in semantic.Timers)
        {
            if (timer.RemainingTicks < 0) throw new InvalidDataException($"Timer '{timer.TimerId}' has a negative duration.");
            RequireEvidenceClassification(timer.EvidenceClassification, $"timer '{timer.TimerId}'");
        }
        foreach (var effect in semantic.StatusEffects)
        {
            if (effect.RemainingTicks < 0) throw new InvalidDataException($"Status effect '{effect.EffectId}' has a negative duration.");
            RequireEvidenceClassification(effect.EvidenceClassification, $"status effect '{effect.EffectId}'");
            if (effect.EvidenceClassification == RuntimeCharacterStatusEffectResolver.EvidenceProven)
            {
                if (!effect.StackPolicy.Equals(RuntimeCharacterStatusEffectResolver.StackPolicyUniqueEffectInstance, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Promoted status effect '{effect.EffectId}' has invalid stack policy '{effect.StackPolicy}'.");
                if (!effect.Lifetime.Equals(RuntimeCharacterStatusEffectResolver.LifetimeUntilExpirationOrDispel, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Promoted status effect '{effect.EffectId}' has invalid lifetime '{effect.Lifetime}'.");
                if (character.SaveVersion >= 6 && RuntimeCharacterStatusEffectResolver.IsSpellProjection(effect))
                {
                    if (string.IsNullOrWhiteSpace(effect.SpellId) || string.IsNullOrWhiteSpace(effect.CasterId))
                        throw new InvalidDataException($"Promoted spell status '{effect.EffectId}' is missing spell or caster ownership.");
                    if (effect.AppliedAtUtc is null) throw new InvalidDataException($"Promoted spell status '{effect.EffectId}' is missing application time.");
                    if (!string.Equals(effect.ReplacementPolicy, RuntimeCharacterStatusEffectResolver.ReplacementPolicyNone, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Promoted spell status '{effect.EffectId}' has invalid replacement policy '{effect.ReplacementPolicy}'.");
                    if (!string.Equals(effect.TimerOwner, RuntimeCharacterStatusEffectResolver.TimerOwnerWorldSpellLedger, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Promoted spell status '{effect.EffectId}' has invalid timer owner '{effect.TimerOwner}'.");
                }
            }
        }
        var orderedSpellEffects = character.SaveVersion >= 6
            ? semantic.StatusEffects.Where(RuntimeCharacterStatusEffectResolver.IsSpellProjection).ToArray()
            : Array.Empty<RuntimeCharacterStatusState>();
        for (var index = 0; index < orderedSpellEffects.Length; index++)
        {
            var effect = orderedSpellEffects[index];
            if (effect.ProjectionOrdinal != index) throw new InvalidDataException($"Spell status '{effect.EffectId}' has invalid projection ordinal.");
            if (index > 0)
            {
                var previous = orderedSpellEffects[index - 1];
                var order = Nullable.Compare(previous.AppliedAtUtc, effect.AppliedAtUtc);
                if (order > 0 || (order == 0 && StringComparer.OrdinalIgnoreCase.Compare(previous.EffectId, effect.EffectId) > 0))
                    throw new InvalidDataException("Spell status projection order is not deterministic.");
            }
        }
        foreach (var timer in semantic.Timers.Where(timer => timer.TimerId.StartsWith("spell.effect.", StringComparison.OrdinalIgnoreCase)))
        {
            if (!timer.TickDomain.Equals(RuntimeCharacterStatusEffectResolver.TickDomainUtcExpiration, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Promoted spell timer '{timer.TimerId}' has invalid tick domain '{timer.TickDomain}'.");
            var effectId = timer.TimerId["spell.effect.".Length..];
            if (!semantic.StatusEffects.Any(effect => effect.EffectId.Equals(effectId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Spell timer '{timer.TimerId}' has no matching status effect.");
        }
        foreach (var flag in semantic.Flags)
        {
            RequireEvidenceClassification(flag.EvidenceClassification, $"flag '{flag.FlagId}'");
        }

        ValidatePromotedFlagConsistency(character, semantic, RuntimeCharacterFlagIds.Dead);
        ValidatePromotedFlagConsistency(character, semantic, RuntimeCharacterFlagIds.Resting);
        RuntimeCharacterDeathResolver.Validate(character);
    }

    private static void ValidatePromotedFlagConsistency(RuntimeCharacterRecord character, RuntimeCharacterSemanticState semantic, string flagId)
    {
        var conditionValue = character.Conditions.TryGetValue(flagId, out var raw)
            && bool.TryParse(raw, out var parsed)
            && parsed;
        var semanticFlag = semantic.Flags.FirstOrDefault(flag => flag.FlagId.Equals(flagId, StringComparison.OrdinalIgnoreCase));
        if (semanticFlag is null) return; // Backward-compatible loading; Normalize promotes the ledger on the next mutation/save.
        if (semanticFlag.Value != conditionValue)
            throw new InvalidDataException($"Character flag '{flagId}' diverges between condition storage and semantic state.");
        if (!semanticFlag.Scope.Equals("character", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Character flag '{flagId}' has invalid scope '{semanticFlag.Scope}'.");
        if (!semanticFlag.Lifetime.Equals("persistent_until_explicit_clear", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Character flag '{flagId}' has invalid lifetime '{semanticFlag.Lifetime}'.");
    }

    private static void EnsureUnique(IEnumerable<string> ids, string kind)
    {
        var duplicate = ids.Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new InvalidDataException($"Duplicate {kind} id '{duplicate.Key}'.");
    }

    private static void RequireEvidenceClassification(string value, string owner)
    {
        if (value is not ("proven" or "strongly_correlated" or "weak_hypothesis" or "unknown"))
            throw new InvalidDataException($"{owner} has invalid evidence classification '{value}'.");
    }
}
