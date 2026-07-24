using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Reforged.Server;

// Consolidated from RuntimeCharacterProgression.cs
public static class RuntimeCharacterProgressionResolver
{
    public const string EvidenceProven = "proven";
    public const string ActiveClassMarker = "progression.active_class";
    public const string LastAwardMarker = "progression.last_xp_award";
    public const string LastPromotionMarker = "progression.last_promotion";

    public static RuntimeCharacterRecord Normalize(RuntimeCharacterRecord character)
    {
        var semantic = character.SemanticState ?? RuntimeCharacterSemanticState.Empty;
        var source = semantic.Progression ?? RuntimeCharacterProgressionState.Empty;
        var classLevels = new Dictionary<string, int>(source.ClassLevels, StringComparer.OrdinalIgnoreCase);
        var classExperience = new Dictionary<string, long>(source.ClassExperience, StringComparer.OrdinalIgnoreCase);
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase);

        var activeClass = string.IsNullOrWhiteSpace(character.ClassId) ? character.ClassName : character.ClassId;
        if (string.IsNullOrWhiteSpace(activeClass)) throw new InvalidDataException("Character progression requires an active class.");
        if (!classLevels.ContainsKey(activeClass)) classLevels[activeClass] = character.Level;
        if (!classExperience.ContainsKey(activeClass)) classExperience[activeClass] = character.Experience;
        markers[ActiveClassMarker] = activeClass;

        var normalized = source with
        {
            ClassLevels = classLevels.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            ClassExperience = classExperience.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };
        return character with
        {
            SaveVersion = RuntimeCharacterRecordValidator.CurrentSaveVersion,
            SemanticState = semantic with { Progression = normalized, RuntimeMarkers = markers }
        };
    }

    public static long ApplyDecodedExperienceModifier(long baseAmount, RuntimeCharacterDefinition race, RuntimeCharacterDefinition characterClass)
    {
        if (baseAmount < 0) throw new ArgumentOutOfRangeException(nameof(baseAmount));
        var classPercent = characterClass.ExperiencePercent > 0
            ? characterClass.ExperiencePercent
            : checked(characterClass.ExperienceStored + 100);
        var combinedPercent = checked(classPercent + race.ExperienceAddPercent);
        if (combinedPercent < 0) throw new InvalidDataException("Decoded race/class experience modifier produced a negative percentage.");
        return checked(baseAmount * combinedPercent / 100L);
    }

    public static RuntimeCharacterRecord AwardExperience(RuntimeCharacterRecord character, long baseAmount, RuntimeCharacterDefinition race, RuntimeCharacterDefinition characterClass, string sourceRecordId)
    {
        var adjusted = ApplyDecodedExperienceModifier(baseAmount, race, characterClass);
        return AwardExperience(character, adjusted, $"{sourceRecordId}|base={baseAmount}|percent={characterClass.ExperiencePercent + race.ExperienceAddPercent}");
    }

    public static RuntimeCharacterRecord AwardExperience(RuntimeCharacterRecord character, long amount, string sourceRecordId)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Experience awards cannot be negative.");
        var normalized = Normalize(character);
        var semantic = normalized.SemanticState!;
        var progression = semantic.Progression;
        var activeClass = semantic.RuntimeMarkers[ActiveClassMarker];
        var total = checked(normalized.Experience + amount);
        var classExperience = new Dictionary<string, long>(progression.ClassExperience, StringComparer.OrdinalIgnoreCase)
        {
            [activeClass] = checked(progression.ClassExperience.GetValueOrDefault(activeClass) + amount)
        };
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [LastAwardMarker] = string.Join("|", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), amount.ToString(CultureInfo.InvariantCulture), sourceRecordId)
        };
        return normalized with
        {
            Experience = total,
            SemanticState = semantic with { Progression = progression with { ClassExperience = classExperience }, RuntimeMarkers = markers },
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public static RuntimeCharacterRecord Promote(
        RuntimeCharacterRecord character,
        int targetLevel,
        int characterPointAward,
        RuntimeCharacterDefinition race,
        RuntimeCharacterDefinition characterClass,
        string sourceRecordId,
        int? forcedHitPointRoll = null)
    {
        var normalized = Normalize(character);
        if (targetLevel != checked(normalized.Level + 1)) throw new InvalidOperationException("Promotions must advance exactly one level.");
        if (characterPointAward < 0) throw new ArgumentOutOfRangeException(nameof(characterPointAward));
        var semantic = normalized.SemanticState!;
        var progression = semantic.Progression;
        var activeClass = semantic.RuntimeMarkers[ActiveClassMarker];
        if (!string.Equals(activeClass, characterClass.Id, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(activeClass, characterClass.Name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Active progression class does not match the resolved class definition.");
        var cap = characterClass.HitPoints?.MaximumBonusPerLevel
            ?? throw new InvalidDataException($"Class '{characterClass.Id}' has no hit-point definition.");
        var hpRoll = forcedHitPointRoll ?? RandomNumberGenerator.GetInt32(cap + 1);
        var growth = RuntimeCharacterDerivedStatisticCalculator.CalculatePromotionGrowth(race, characterClass, normalized.Level, normalized.BaseStats, hpRoll);
        var levels = new Dictionary<string, int>(progression.ClassLevels, StringComparer.OrdinalIgnoreCase) { [activeClass] = targetLevel };
        var conditions = new Dictionary<string, string>(normalized.Conditions, StringComparer.OrdinalIgnoreCase);
        var currentCp = conditions.TryGetValue("character_points", out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        conditions["character_points"] = checked(currentCp + characterPointAward).ToString(CultureInfo.InvariantCulture);
        var markers = new Dictionary<string, string>(semantic.RuntimeMarkers, StringComparer.OrdinalIgnoreCase)
        {
            [LastPromotionMarker] = string.Join("|", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), activeClass, targetLevel.ToString(CultureInfo.InvariantCulture), hpRoll.ToString(CultureInfo.InvariantCulture), growth.MaximumHitPointDelta.ToString(CultureInfo.InvariantCulture), growth.MaximumManaDelta.ToString(CultureInfo.InvariantCulture), sourceRecordId)
        };
        var nextMaximumHitPoints = Math.Max(1, checked(normalized.MaximumHitPoints + growth.MaximumHitPointDelta));
        var nextMaximumMana = Math.Max(0, checked(normalized.MaximumMana + growth.MaximumManaDelta));
        return normalized with
        {
            Level = targetLevel,
            HitPoints = Math.Min(nextMaximumHitPoints, checked(normalized.HitPoints + Math.Max(0, growth.MaximumHitPointDelta))),
            MaximumHitPoints = nextMaximumHitPoints,
            Mana = Math.Min(nextMaximumMana, checked(normalized.Mana + Math.Max(0, growth.MaximumManaDelta))),
            MaximumMana = nextMaximumMana,
            Conditions = conditions,
            SemanticState = semantic with { Progression = progression with { ClassLevels = levels }, RuntimeMarkers = markers },
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}

// Consolidated from RuntimeCharacterProgressionSchedule.cs
public sealed record RuntimeCharacterProgressionScheduleRow(
    string RowId,
    int TargetLevel,
    long RequiredExperience,
    int CharacterPointAward,
    string? ClassRef,
    string? RaceRef,
    int? MaximumLevel,
    string Classification,
    string SourceId,
    string SourcePath);

public sealed class RuntimeCharacterProgressionSchedule
{
    private static readonly HashSet<string> ExecutableClassifications = new(StringComparer.OrdinalIgnoreCase)
    {
        "proven", "verified", "strongly_correlated", "strongly_corroborated"
    };

    private readonly IReadOnlyList<RuntimeCharacterProgressionScheduleRow> _rows;
    private RuntimeCharacterProgressionSchedule(IReadOnlyList<RuntimeCharacterProgressionScheduleRow> rows) => _rows = rows;

    public static RuntimeCharacterProgressionSchedule Load(string dataRoot)
    {
        var rows = new List<RuntimeCharacterProgressionScheduleRow>();
        foreach (var path in Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Collect(document.RootElement, path, rows);
        }
        return new RuntimeCharacterProgressionSchedule(rows.OrderBy(x => x.TargetLevel).ThenBy(x => x.RowId, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public RuntimeCharacterProgressionScheduleRow Require(RuntimeCharacterRecord character, int targetLevel)
    {
        var matches = _rows.Where(row => row.TargetLevel == targetLevel && Matches(row.ClassRef, character.ClassId, character.ClassName) && Matches(row.RaceRef, character.RaceId, character.RaceName)).ToArray();
        if (matches.Length == 0) throw new InvalidOperationException($"Generated progression data contains no evidence-promoted row for level {targetLevel}, race {character.RaceId}, class {character.ClassId}.");

        // Relationship resolution is specificity-first: a class+race row overrides class-only,
        // race-only, and global rows. Conflicts are errors only among equally specific rows.
        var highestSpecificity = matches.Max(Specificity);
        var authoritative = matches.Where(row => Specificity(row) == highestSpecificity).ToArray();
        var distinct = authoritative.Select(x => (x.RequiredExperience, x.CharacterPointAward, x.MaximumLevel)).Distinct().ToArray();
        if (distinct.Length != 1) throw new InvalidDataException($"Conflicting executable progression rows exist at specificity {highestSpecificity} for level {targetLevel}, race {character.RaceId}, class {character.ClassId}.");
        var selected = authoritative.OrderBy(x => x.RowId, StringComparer.OrdinalIgnoreCase).First();
        if (selected.MaximumLevel is int cap && targetLevel > cap) throw new InvalidOperationException($"Target level {targetLevel} exceeds the evidence-backed cap {cap}.");
        var prior = _rows.Where(row => row.TargetLevel < targetLevel && Matches(row.ClassRef, character.ClassId, character.ClassName) && Matches(row.RaceRef, character.RaceId, character.RaceName)).OrderByDescending(row => row.TargetLevel).FirstOrDefault();
        if (prior is not null && selected.RequiredExperience <= prior.RequiredExperience) throw new InvalidDataException($"Progression XP threshold {selected.RequiredExperience} is not greater than prior threshold {prior.RequiredExperience}.");
        return selected;
    }

    private static void Collect(JsonElement node, string path, List<RuntimeCharacterProgressionScheduleRow> rows)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var level = ReadInt(node, "target_level", "level");
            var xp = ReadLong(node, "required_experience", "experience_required", "total_experience");
            var cp = ReadInt(node, "character_point_award", "cp_award", "character_points");
            var classification = ReadText(node, "classification", "evidence_classification");
            var source = ReadText(node, "source_id", "source_record_id", "provenance");
            if (level > 0 && xp > 0 && cp >= 0 && !string.IsNullOrWhiteSpace(classification) && ExecutableClassifications.Contains(classification) && !string.IsNullOrWhiteSpace(source))
            {
                var rowId = ReadText(node, "row_id", "id") ?? $"{Path.GetFileName(path)}:{level}";
                rows.Add(new RuntimeCharacterProgressionScheduleRow(rowId, level, xp, cp,
                    ReadText(node, "class_ref", "class_id", "class"), ReadText(node, "race_ref", "race_id", "race"),
                    ReadNullableInt(node, "maximum_level", "level_cap"), classification, source, path));
            }
            foreach (var property in node.EnumerateObject()) Collect(property.Value, path, rows);
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var child in node.EnumerateArray()) Collect(child, path, rows);
    }


    private static int Specificity(RuntimeCharacterProgressionScheduleRow row) =>
        (string.IsNullOrWhiteSpace(row.ClassRef) ? 0 : 1) +
        (string.IsNullOrWhiteSpace(row.RaceRef) ? 0 : 1);

    private static bool Matches(string? expected, string id, string name)
    {
        if (string.IsNullOrWhiteSpace(expected)) return true;
        if (expected.Equals(id, StringComparison.OrdinalIgnoreCase) || expected.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        var dot = id.LastIndexOf('.');
        return int.TryParse(dot >= 0 ? id[(dot + 1)..] : id, out var legacy) && expected == legacy.ToString(CultureInfo.InvariantCulture);
    }
    private static string? ReadText(JsonElement root, params string[] names) { foreach (var name in names) if (root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null) { var text = value.ToString(); if (!string.IsNullOrWhiteSpace(text)) return text; } return null; }
    private static long ReadLong(JsonElement root, params string[] names) { foreach (var name in names) if (root.TryGetProperty(name, out var value) && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed; return 0; }
    private static int ReadInt(JsonElement root, params string[] names) => checked((int)Math.Clamp(ReadLong(root, names), int.MinValue, int.MaxValue));
    private static int? ReadNullableInt(JsonElement root, params string[] names) { foreach (var name in names) if (root.TryGetProperty(name, out var value) && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed; return null; }
}
