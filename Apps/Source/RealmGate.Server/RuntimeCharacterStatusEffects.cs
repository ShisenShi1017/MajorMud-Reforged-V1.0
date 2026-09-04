namespace RealmGate.Server;

public static class RuntimeCharacterStatusEffectResolver
{
    public const string TickDomainUtcExpiration = "utc_expiration";
    public const string LifetimeUntilExpirationOrDispel = "until_expiration_or_explicit_dispel";
    public const string StackPolicyUniqueEffectInstance = "unique_effect_instance";
    public const string ReplacementPolicyNone = "none_independent_instances";
    public const string TimerOwnerWorldSpellLedger = "runtime_spell_state";
    public const string EvidenceProven = "proven";
    public const string SpellTimerPrefix = "spell.effect.";
    public const string SpellSourcePrefix = "spell:";

    public static RuntimeCharacterRecord Synchronize(
        RuntimeCharacterRecord character,
        IEnumerable<RuntimeActiveSpellEffect> activeEffects,
        DateTimeOffset nowUtc,
        string sourceRecordId)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(activeEffects);
        if (string.IsNullOrWhiteSpace(sourceRecordId)) throw new ArgumentException("Status source record id is required.", nameof(sourceRecordId));

        var effects = activeEffects
            .Where(effect => effect.Target.Kind.Equals("character", StringComparison.OrdinalIgnoreCase)
                && effect.Target.Id.Equals(character.CharacterId, StringComparison.OrdinalIgnoreCase)
                && (effect.ExpiresAtUtc is null || effect.ExpiresAtUtc > nowUtc))
            .OrderBy(effect => effect.AppliedAtUtc)
            .ThenBy(effect => effect.EffectId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var duplicate = effects.GroupBy(effect => effect.EffectId, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Duplicate active spell effect id '{duplicate.Key}'.");

        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var preservedStatuses = semantic.StatusEffects.Where(effect => !IsSpellProjection(effect)).ToArray();
        var preservedTimers = semantic.Timers.Where(timer => !timer.TimerId.StartsWith(SpellTimerPrefix, StringComparison.OrdinalIgnoreCase)).ToArray();

        var statuses = effects.Select((effect, ordinal) => new RuntimeCharacterStatusState(
            effect.EffectId,
            ResolveMagnitude(effect),
            RemainingTicks(effect, nowUtc),
            StackPolicyUniqueEffectInstance,
            LifetimeUntilExpirationOrDispel,
            EvidenceProven,
            $"{SpellSourcePrefix}{effect.SpellId};{sourceRecordId}",
            effect.SpellId,
            effect.CasterId,
            effect.AppliedAtUtc,
            effect.ExpiresAtUtc,
            ordinal,
            ReplacementPolicyNone,
            TimerOwnerWorldSpellLedger))
            .ToArray();

        var timers = effects.Where(effect => effect.ExpiresAtUtc is not null).Select(effect => new RuntimeCharacterTimerState(
            SpellTimerPrefix + effect.EffectId,
            RemainingTicks(effect, nowUtc),
            TickDomainUtcExpiration,
            LifetimeUntilExpirationOrDispel,
            EvidenceProven,
            effect.ExpiresAtUtc,
            $"{SpellSourcePrefix}{effect.SpellId};{sourceRecordId}"))
            .ToArray();

        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            ["character.status.authority"] = TimerOwnerWorldSpellLedger,
            ["character.status.order"] = "applied_at_utc_then_effect_id",
            ["character.status.replacement"] = ReplacementPolicyNone,
            ["character.status.last_sync_source"] = sourceRecordId,
            ["character.status.active_spell_count"] = effects.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        return character with
        {
            SemanticState = semantic with
            {
                StatusEffects = preservedStatuses.Concat(statuses).ToArray(),
                Timers = preservedTimers.Concat(timers).ToArray(),
                RuntimeMarkers = markers
            },
            SaveVersion = Math.Max(character.SaveVersion, 6),
            UpdatedAtUtc = nowUtc
        };
    }

    public static bool IsSpellProjection(RuntimeCharacterStatusState effect) =>
        effect.SourceRecordId?.StartsWith(SpellSourcePrefix, StringComparison.OrdinalIgnoreCase) == true
        || !string.IsNullOrWhiteSpace(effect.SpellId);

    private static int ResolveMagnitude(RuntimeActiveSpellEffect effect) =>
        checked((effect.MinimumEffect + effect.MaximumEffect) / 2);

    private static long RemainingTicks(RuntimeActiveSpellEffect effect, DateTimeOffset nowUtc)
    {
        if (effect.ExpiresAtUtc is null) return Math.Max(0, effect.Duration);
        return Math.Max(0, checked((long)Math.Ceiling((effect.ExpiresAtUtc.Value - nowUtc).TotalSeconds)));
    }
}
