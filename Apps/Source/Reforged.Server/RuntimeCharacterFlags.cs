namespace Reforged.Server;

public static class RuntimeCharacterFlagIds
{
    public const string Dead = "dead";
    public const string Resting = "resting";
    public const string ToptenDisabled = "sysop.topten.disabled";
}

public static class RuntimeCharacterFlagResolver
{
    private const string ScopeCharacter = "character";
    private const string LifetimePersistent = "persistent_until_explicit_clear";
    private const string EvidenceProven = "proven";

    public static bool IsSet(RuntimeCharacterRecord character, string flagId)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (string.IsNullOrWhiteSpace(flagId)) throw new ArgumentException("Character flag id is required.", nameof(flagId));
        return character.Conditions.TryGetValue(flagId, out var raw)
            && bool.TryParse(raw, out var value)
            && value;
    }

    public static RuntimeCharacterRecord Set(RuntimeCharacterRecord character, string flagId, string sourceRecordId)
    {
        return Mutate(character, flagId, true, sourceRecordId);
    }

    public static RuntimeCharacterRecord Clear(RuntimeCharacterRecord character, string flagId, string sourceRecordId)
    {
        return Mutate(character, flagId, false, sourceRecordId);
    }

    public static RuntimeCharacterRecord Normalize(RuntimeCharacterRecord character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var result = character;
        foreach (var flagId in new[] { RuntimeCharacterFlagIds.Dead, RuntimeCharacterFlagIds.Resting, RuntimeCharacterFlagIds.ToptenDisabled })
        {
            var value = IsSet(result, flagId);
            result = Mutate(result, flagId, value, "runtime.character.flag.normalize", preserveConditionFalse: false);
        }
        return result;
    }

    private static RuntimeCharacterRecord Mutate(
        RuntimeCharacterRecord character,
        string flagId,
        bool value,
        string sourceRecordId,
        bool preserveConditionFalse = false)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (!IsSupported(flagId)) throw new InvalidOperationException($"Unsupported promoted character flag '{flagId}'.");
        if (string.IsNullOrWhiteSpace(sourceRecordId)) throw new ArgumentException("Character flag source record id is required.", nameof(sourceRecordId));

        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase);
        if (value || preserveConditionFalse) conditions[flagId] = value ? "true" : "false";
        else conditions.Remove(flagId);

        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var flags = semantic.Flags
            .Where(flag => !flag.FlagId.Equals(flagId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        flags.Add(new RuntimeCharacterFlagState(
            flagId,
            ScopeCharacter,
            LifetimePersistent,
            value,
            EvidenceProven,
            sourceRecordId));

        return character with
        {
            Conditions = conditions,
            SemanticState = semantic with { Flags = flags.OrderBy(flag => flag.FlagId, StringComparer.OrdinalIgnoreCase).ToArray() },
            SaveVersion = Math.Max(character.SaveVersion, 4),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static bool IsSupported(string flagId) =>
        flagId.Equals(RuntimeCharacterFlagIds.Dead, StringComparison.OrdinalIgnoreCase)
        || flagId.Equals(RuntimeCharacterFlagIds.Resting, StringComparison.OrdinalIgnoreCase)
        || flagId.Equals(RuntimeCharacterFlagIds.ToptenDisabled, StringComparison.OrdinalIgnoreCase);
}
