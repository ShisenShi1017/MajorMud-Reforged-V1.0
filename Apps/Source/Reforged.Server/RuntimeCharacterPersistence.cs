using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeCharacterLoadResult(
    RuntimeCharacterRecord Character,
    string Source,
    bool Recovered,
    string? Failure);

public static class RuntimeCharacterPersistencePolicy
{
    public const string ContractMarker = "character.persistence.contract";
    public const string SaveReasonMarker = "character.persistence.last_save_reason";
    public const string SaveSequenceMarker = "character.persistence.save_sequence";
    public const string RecoveryMarker = "character.persistence.recovered_from_backup";
    public const string ContractVersion = "0163";
    public const string AutosaveMarker = "character.persistence.last_autosave_utc";
    public const string DisconnectFailureMarker = "character.persistence.disconnect_failure";
    public const string MigrationBoundaryMarker = "character.persistence.compatibility_boundary";


    public static bool IsAutosaveDue(RuntimeCharacterRecord character, DateTimeOffset now, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        var markers = (character.SemanticState ?? RuntimeCharacterSemanticState.Empty).RuntimeMarkers;
        if (!markers.TryGetValue(AutosaveMarker, out var raw) || !DateTimeOffset.TryParse(raw, out var last)) return true;
        return now >= last.ToUniversalTime().Add(interval);
    }

    public static RuntimeCharacterRecord MarkAutosave(RuntimeCharacterRecord character, DateTimeOffset now)
    {
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [AutosaveMarker] = now.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            [MigrationBoundaryMarker] = $"save-version-{RuntimeCharacterRecordValidator.CurrentSaveVersion}"
        };
        return character with { SemanticState = semantic with { RuntimeMarkers = markers } };
    }

    public static RuntimeCharacterRecord MarkDisconnectFailure(RuntimeCharacterRecord character, Exception failure, DateTimeOffset now)
    {
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [DisconnectFailureMarker] = $"{now.ToUniversalTime():O}|{failure.GetType().Name}"
        };
        return character with { SemanticState = semantic with { RuntimeMarkers = markers } };
    }

    public static RuntimeCharacterRecord PrepareForSave(RuntimeCharacterRecord character, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidDataException("Character save reason is required.");
        var normalized = RuntimeCharacterProgressionResolver.Normalize(character);
        var semantic = normalized.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase);
        var sequence = 0L;
        if (markers.TryGetValue(SaveSequenceMarker, out var raw) && !long.TryParse(raw, out sequence))
            throw new InvalidDataException("Character persistence save sequence is invalid.");
        if (sequence == long.MaxValue) throw new OverflowException("Character persistence save sequence overflowed.");
        markers[ContractMarker] = ContractVersion;
        markers[SaveReasonMarker] = reason.Trim().ToLowerInvariant();
        markers[SaveSequenceMarker] = checked(sequence + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var prepared = normalized with
        {
            BaseStats = Sort(normalized.BaseStats),
            Skills = Sort(normalized.Skills),
            Spells = normalized.Spells.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            Inventory = normalized.Inventory.ToArray(),
            Equipment = SortNullable(normalized.Equipment),
            Conditions = Sort(normalized.Conditions),
            SemanticState = semantic with
            {
                Flags = semantic.Flags.OrderBy(x => x.FlagId, StringComparer.OrdinalIgnoreCase).ToArray(),
                Timers = semantic.Timers.OrderBy(x => x.TimerId, StringComparer.OrdinalIgnoreCase).ToArray(),
                StatusEffects = semantic.StatusEffects
                    .OrderBy(x => x.ProjectionOrdinal ?? int.MaxValue)
                    .ThenBy(x => x.AppliedAtUtc)
                    .ThenBy(x => x.EffectId, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Progression = semantic.Progression with
                {
                    ClassLevels = Sort(semantic.Progression.ClassLevels),
                    ClassExperience = Sort(semantic.Progression.ClassExperience),
                    PermanentStatAdjustments = Sort(semantic.Progression.PermanentStatAdjustments),
                    PermanentAbilityAdjustments = Sort(semantic.Progression.PermanentAbilityAdjustments)
                },
                DerivedStats = Sort(semantic.DerivedStats),
                Resistances = Sort(semantic.Resistances),
                SavingThrows = Sort(semantic.SavingThrows),
                RuntimeMarkers = Sort(markers)
            },
            UpdatedAtUtc = now,
            SaveVersion = RuntimeCharacterRecordValidator.CurrentSaveVersion
        };
        RuntimeCharacterRecordValidator.Validate(prepared);
        ValidateRoundTrip(prepared);
        return prepared;
    }

    public static RuntimeCharacterLoadResult Load(string primaryPath, JsonSerializerOptions options)
    {
        Exception? primaryFailure = null;
        try
        {
            var primary = ReadAndValidate(primaryPath, options);
            if (primary is not null) return new RuntimeCharacterLoadResult(primary, "primary", false, null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException)
        {
            primaryFailure = ex;
        }

        var backupPath = primaryPath + ".bak";
        try
        {
            var backup = ReadAndValidate(backupPath, options);
            if (backup is null)
            {
                if (primaryFailure is not null) throw new InvalidDataException("Character primary save is invalid and no valid backup exists.", primaryFailure);
                return new RuntimeCharacterLoadResult(null!, "missing", false, null);
            }
            var semantic = backup.SemanticState ?? RuntimeCharacterSemanticState.Empty;
            var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
            {
                [RecoveryMarker] = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            };
            return new RuntimeCharacterLoadResult(backup with { SemanticState = semantic with { RuntimeMarkers = markers } }, "backup", true, primaryFailure?.Message);
        }
        catch (Exception backupFailure) when (backupFailure is JsonException or InvalidDataException or IOException)
        {
            throw new InvalidDataException("Character save and backup are both invalid.", new AggregateException(primaryFailure ?? new InvalidDataException("Primary save missing."), backupFailure));
        }
    }

    private static RuntimeCharacterRecord? ReadAndValidate(string path, JsonSerializerOptions options)
    {
        if (!File.Exists(path)) return null;
        var payload = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(payload)) throw new InvalidDataException($"Character save '{path}' is empty.");
        var character = JsonSerializer.Deserialize<RuntimeCharacterRecord>(payload, options)
            ?? throw new InvalidDataException($"Character save '{path}' deserialized to null.");
        character = RuntimeCharacterProgressionResolver.Normalize(character);
        RuntimeCharacterRecordValidator.Validate(character);
        return character;
    }

    private static void ValidateRoundTrip(RuntimeCharacterRecord character)
    {
        var payload = JsonSerializer.Serialize(character);
        var restored = JsonSerializer.Deserialize<RuntimeCharacterRecord>(payload)
            ?? throw new InvalidDataException("Character persistence round-trip produced null.");
        RuntimeCharacterRecordValidator.Validate(restored);
        if (!string.Equals(restored.CharacterId, character.CharacterId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Character persistence round-trip changed CharacterId.");
    }

    private static IReadOnlyDictionary<string, int> Sort(IReadOnlyDictionary<string, int> source) =>
        source.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyDictionary<string, long> Sort(IReadOnlyDictionary<string, long> source) =>
        source.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyDictionary<string, string> Sort(IReadOnlyDictionary<string, string> source) =>
        source.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyDictionary<string, string?> SortNullable(IReadOnlyDictionary<string, string?> source) =>
        source.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
}
