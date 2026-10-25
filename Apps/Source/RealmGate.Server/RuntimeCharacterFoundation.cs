using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeAccountRecord(
    string AccountId,
    string DisplayName,
    IReadOnlyList<string> CharacterIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RuntimeCharacterRecord(
    string CharacterId,
    string AccountId,
    string DisplayName,
    string WorldId,
    string RaceId,
    string RaceName,
    string ClassId,
    string ClassName,
    int Level,
    long Experience,
    int HitPoints,
    int MaximumHitPoints,
    int Mana,
    int MaximumMana,
    string RoomId,
    int RoomNumber,
    IReadOnlyDictionary<string, int> BaseStats,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyList<string> Spells,
    IReadOnlyList<string> Inventory,
    IReadOnlyDictionary<string, string?> Equipment,
    IReadOnlyDictionary<string, string> Conditions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int SaveVersion = 1,
    RuntimeItemState? ItemState = null,
    RuntimeCharacterSemanticState? SemanticState = null,
    string Sex = "Unspecified");

public sealed record RuntimeCharacterDefinition(
    string Id,
    string Name,
    string Confidence,
    string Classification,
    IReadOnlyDictionary<string, int> MinimumStats,
    IReadOnlyDictionary<string, int> MaximumStats,
    int HitPointsPerLevelAdjustment,
    int BaseCreationPoints,
    int ExperienceAddPercent,
    IReadOnlyList<RuntimeAbilityGrant> AbilitySlots,
    RuntimeClassHitPointDefinition? HitPoints,
    RuntimeMagicDefinition? Magic,
    int LegacyNumber,
    int ExperienceStored,
    int ExperiencePercent,
    int WeaponType,
    int ArmourType,
    int CombatLevel,
    string MageryName,
    string SourceFile,
    long SourceRecordOffset,
    string SourceClassification);


public sealed record RuntimeCharacterExperienceRule(
    string ClassPercentFormula,
    string RaceAddPercentFormula,
    string CombinedPercentFormula,
    IReadOnlyList<string> Evidence);

public sealed record RuntimeCharacterDerivedFormulaContract(
    string MaximumHitPointsFormula,
    string ConstitutionAdjustmentFormula,
    string CreationHitPointRollRule,
    string LevelUpHitPointRollRule,
    string MaximumManaTypeZeroFormula,
    string MaximumManaCasterFormula,
    string MaximumManaKaiFormula,
    int CreationAbilityAndEquipmentCache,
    IReadOnlyDictionary<int, string> SpellcastingTypes);

public sealed record RuntimeCharacterAlignmentRuleContract(
    int InitialEvilPoints,
    string InitialReputation,
    IReadOnlyDictionary<string, int> AttackReputationPenalties,
    IReadOnlyDictionary<string, int> ReputationMinimumEvilPoints,
    string AttackPenaltyRule,
    int LawfulAttackPenaltyMultiplier,
    bool CreationBlocking,
    int EvilActionReduction,
    int NoEvilActionBaseReduction,
    int CycleMinutes,
    int CycleAmount,
    int DefaultEpCycleMinutes,
    int DefaultEpAmount,
    int MaximumDailyReduction,
    string NegativeEpException,
    int FiendAttackLockoutThreshold);

public sealed record RuntimeAbilityGrant(int AbilityId, int Value);

public sealed record RuntimeClassHitPointDefinition(
    int MinimumPerLevel,
    int MaximumBonusPerLevel,
    int MaximumPerLevel);

public sealed record RuntimeMagicDefinition(
    int Type,
    int Level);

public sealed record RuntimeCharacterCreationContract(
    bool AllRaceClassCombinationsAllowed,
    int AllowedPairCount,
    int StartingGold,
    string StartingAlignment,
    int StartingAlignmentValue,
    string StartingAlignmentClassification,
    string StartingRoomId,
    int StartingRoomNumber,
    IReadOnlyList<string> StartingInventory,
    IReadOnlyDictionary<string, string> StartingEquipment,
    IReadOnlyList<string> StartingSpells,
    string StartingAbilitiesRule,
    string StartingStatsRule)
{
    public static RuntimeCharacterCreationContract Blocked { get; } = new(false, 0, 0, "Neutral", 0, string.Empty, string.Empty, 0, Array.Empty<string>(), new Dictionary<string, string>(), Array.Empty<string>(), string.Empty, string.Empty);

    public static RuntimeCharacterCreationContract FromFoundation(JsonElement root)
    {
        if (!root.TryGetProperty("character_creation_rules", out var rules) || rules.ValueKind != JsonValueKind.Object)
        {
            return Blocked;
        }

        var allPairs = rules.TryGetProperty("all_race_class_combinations_allowed", out var allowed) && allowed.ValueKind == JsonValueKind.True;
        var pairCount = rules.TryGetProperty("allowed_pair_count", out var count) && count.TryGetInt32(out var parsedCount) ? parsedCount : 0;
        var gold = rules.TryGetProperty("starting_gold", out var goldValue) && goldValue.TryGetInt32(out var parsedGold) ? parsedGold : 0;
        var alignmentValue = rules.TryGetProperty("starting_alignment", out var alignment) && alignment.TryGetInt32(out var parsedAlignment) ? parsedAlignment : 0;
        var alignmentName = rules.TryGetProperty("starting_alignment_name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() ?? "Neutral" : "Neutral";
        var alignmentClassification = rules.TryGetProperty("starting_alignment_classification", out var classification) && classification.ValueKind == JsonValueKind.String ? classification.GetString() ?? string.Empty : string.Empty;
        var inventory = ReadStrings(rules, "starting_inventory");
        var spells = ReadStrings(rules, "starting_spells");
        var equipment = ReadStringMap(rules, "starting_equipment");
        var abilitiesRule = ReadString(rules, "starting_abilities_rule");
        var statsRule = ReadString(rules, "starting_stats_rule");
        var roomId = string.Empty;
        var roomNumber = 0;
        if (rules.TryGetProperty("new_character_start", out var start) && start.ValueKind == JsonValueKind.Object)
        {
            roomId = start.TryGetProperty("room_id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() ?? string.Empty : string.Empty;
            roomNumber = start.TryGetProperty("room_number", out var number) && number.TryGetInt32(out var parsedRoom) ? parsedRoom : 0;
        }

        return new RuntimeCharacterCreationContract(allPairs, pairCount, gold, alignmentName, alignmentValue, alignmentClassification, roomId, roomNumber, inventory, equipment, spells, abilitiesRule, statsRule);
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Object) return new Dictionary<string, string>();
        return values.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String).ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray();
    }
}

public sealed class RuntimeCharacterCatalog
{
    private readonly Dictionary<string, RuntimeCharacterDefinition> _races = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeCharacterDefinition> _classes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<RuntimeCharacterDefinition> Races => _races.Values.OrderBy(value => value.Name).ToArray();
    public IReadOnlyCollection<RuntimeCharacterDefinition> Classes => _classes.Values.OrderBy(value => value.Name).ToArray();
    public bool CreationGateReady { get; private set; }
    public IReadOnlyList<string> CreationGateBlockers { get; private set; } = Array.Empty<string>();
    public RuntimeCharacterCreationContract CreationContract { get; private set; } = RuntimeCharacterCreationContract.Blocked;
    public RuntimeCharacterExperienceRule ExperienceRule { get; private set; } = new(string.Empty, string.Empty, string.Empty, Array.Empty<string>());
    public RuntimeCharacterDerivedFormulaContract DerivedFormulaContract { get; private set; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, new Dictionary<int, string>());
    public RuntimeCharacterAlignmentRuleContract AlignmentRuleContract { get; private set; } = new(0, "Neutral", new Dictionary<string, int>(), new Dictionary<string, int>(), string.Empty, RuntimeClassicDeathLawPvpAuthority.LawfulAttackPenaltyMultiplier, false, 2, 3, 60, 1, 60, 1, 18, string.Empty, 300);

    public static RuntimeCharacterCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterCatalog();
        var path = Path.Combine(projectRoot, "Data", worldId, "character_foundation.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Generated Librarium character foundation is missing.", path);
        }

        catalog.LoadFoundation(path);
        return catalog;
    }

    public RuntimeCharacterDefinition RequireRace(string idOrName) => Require(_races, idOrName, "race");
    public RuntimeCharacterDefinition RequireClass(string idOrName) => Require(_classes, idOrName, "class");

    private static RuntimeCharacterDefinition Require(Dictionary<string, RuntimeCharacterDefinition> values, string idOrName, string kind)
    {
        var requested = idOrName.Trim();
        if (values.TryGetValue(requested, out var exact))
        {
            return exact;
        }

        var byName = values.Values.FirstOrDefault(value => string.Equals(value.Name, requested, StringComparison.OrdinalIgnoreCase));
        return byName ?? throw new InvalidOperationException($"Unknown generated Classic {kind} '{idOrName}'.");
    }

    private void LoadFoundation(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("authority", out var authority) || !string.Equals(authority.GetString(), "Librarium", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Character foundation is not a Librarium-authoritative projection.");
        }

        LoadArray(root, "races", "race", _races);
        LoadArray(root, "classes", "class", _classes);
        CreationContract = RuntimeCharacterCreationContract.FromFoundation(root);
        ExperienceRule = ReadExperienceRule(root);
        DerivedFormulaContract = ReadDerivedFormulaContract(root);
        AlignmentRuleContract = ReadAlignmentRuleContract(root);

        if (root.TryGetProperty("creation_gate", out var gate) && gate.ValueKind == JsonValueKind.Object)
        {
            CreationGateReady = gate.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True;
            CreationGateBlockers = ReadStringArray(gate, "blockers");
        }
        else if (root.TryGetProperty("character_creation_rules", out var rules) && rules.ValueKind == JsonValueKind.Object)
        {
            CreationGateReady = rules.TryGetProperty("all_race_class_combinations_allowed", out var allowed) && allowed.ValueKind == JsonValueKind.True;
            CreationGateBlockers = CreationGateReady
                ? Array.Empty<string>()
                : new[] { "generated character creation rules are not ready" };
        }
    }

    private static RuntimeCharacterExperienceRule ReadExperienceRule(JsonElement root)
    {
        if (!root.TryGetProperty("xp_rule", out var rule) || rule.ValueKind != JsonValueKind.Object)
            return new(string.Empty, string.Empty, string.Empty, Array.Empty<string>());
        return new(ReadString(rule, "class_percent_formula"), ReadString(rule, "race_add_percent_formula"), ReadString(rule, "combined_percent_formula"), ReadStringArray(rule, "evidence"));
    }

    private static RuntimeCharacterDerivedFormulaContract ReadDerivedFormulaContract(JsonElement root)
    {
        if (!root.TryGetProperty("derived_stat_formulas", out var formulas) || formulas.ValueKind != JsonValueKind.Object)
            return new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, new Dictionary<int, string>());
        formulas.TryGetProperty("maximum_hit_points", out var hp);
        formulas.TryGetProperty("maximum_mana", out var mana);
        var types = new Dictionary<int, string>();
        if (formulas.TryGetProperty("spellcasting", out var spellcasting) && spellcasting.TryGetProperty("types", out var typeValues) && typeValues.ValueKind == JsonValueKind.Object)
            foreach (var property in typeValues.EnumerateObject()) if (int.TryParse(property.Name, out var id) && property.Value.ValueKind == JsonValueKind.String) types[id] = property.Value.GetString() ?? string.Empty;
        return new(ReadString(hp, "formula"), ReadString(hp, "con_adj"), ReadString(hp, "creation_accumulated_hp_rolls"), ReadString(hp, "level_up_roll"), ReadString(mana, "magic_type_0"), ReadString(mana, "magic_type_1_to_4"), ReadString(mana, "magic_type_5"), ReadInt(mana, "creation_ability_and_equipment_cache"), types);
    }

    private static RuntimeCharacterAlignmentRuleContract ReadAlignmentRuleContract(JsonElement root)
    {
        if (!root.TryGetProperty("character_creation_rules", out var creation) || !creation.TryGetProperty("alignment_evil_point_rules", out var rules))
            return new(0, "Neutral", new Dictionary<string, int>(), new Dictionary<string, int>(), string.Empty, RuntimeClassicDeathLawPvpAuthority.LawfulAttackPenaltyMultiplier, false, 2, 3, 60, 1, 60, 1, 18, string.Empty, 300);
        var penalties = ReadIntDictionary(rules, "attack_reputation_penalties");
        var reputationMinimums = ReadIntDictionary(rules, "reputation_minimum_evil_points");
        rules.TryGetProperty("daily_cleanup", out var cleanup);
        cleanup.TryGetProperty("evil_action_that_day", out var evilDay);
        cleanup.TryGetProperty("no_evil_action_that_day", out var cleanDay);
        rules.TryGetProperty("fiend_attack_lockout", out var lockout);
        var maximumDailyReduction = ReadInt(cleanDay, "maximum_daily_reduction");
        if (maximumDailyReduction <= 0) maximumDailyReduction = 18;
        var lawfulMultiplier = RuntimeClassicDeathLawPvpAuthority.LawfulAttackPenaltyMultiplier;
        if (rules.TryGetProperty("lawful_target_attack_penalty", out var lawfulPenalty) && lawfulPenalty.ValueKind == JsonValueKind.Object)
        {
            var decoded = ReadInt(lawfulPenalty, "multiplier");
            if (decoded > 0) lawfulMultiplier = decoded;
        }
        return new(
            ReadInt(rules, "initial_evil_points"),
            ReadString(rules, "initial_reputation"),
            penalties,
            reputationMinimums,
            ReadString(rules, "attack_penalty_rule"),
            lawfulMultiplier,
            rules.TryGetProperty("creation_blocking", out var creationBlocking) && creationBlocking.ValueKind == JsonValueKind.True,
            ReadInt(evilDay, "evil_points_reduced_by"),
            ReadInt(cleanDay, "base_reduction"),
            ReadInt(cleanDay, "cycle_minutes"),
            ReadInt(cleanDay, "cycle_amount"),
            ReadInt(cleanDay, "default_epcycle_minutes"),
            ReadInt(cleanDay, "default_epamount"),
            maximumDailyReduction,
            ReadString(cleanup, "negative_ep_exception"),
            ReadInt(lockout, "threshold"));
    }

    private static void LoadArray(JsonElement root, string property, string kind, Dictionary<string, RuntimeCharacterDefinition> destination)
    {
        if (!root.TryGetProperty(property, out var records) || records.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Generated character foundation does not contain '{property}'.");
        }

        foreach (var record in records.EnumerateArray())
        {
            var id = ReadString(record, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                var number = ReadInt(record, "number");
                if (number > 0) id = $"{kind}.{number}";
            }
            var name = ReadString(record, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            destination[id] = new RuntimeCharacterDefinition(
                id,
                name,
                ReadString(record, "confidence") is { Length: > 0 } confidence ? confidence : ReadNestedString(record, "source", "classification"),
                ReadString(record, "classification") is { Length: > 0 } classification ? classification : ReadNestedString(record, "source", "classification"),
                ReadIntDictionary(record, "minimum_stats"),
                ReadIntDictionary(record, "maximum_stats"),
                ReadInt(record, "hp_per_level_adjustment"),
                ReadInt(record, "base_cp"),
                ReadInt(record, "experience_add_percent"),
                ReadAbilitySlots(record),
                ReadHitPoints(record),
                ReadMagic(record),
                ReadInt(record, "number"),
                ReadInt(record, "experience_stored"),
                ReadInt(record, "experience_percent"),
                ReadInt(record, "weapon_type"),
                ReadInt(record, "armour_type"),
                ReadInt(record, "combat_level"),
                ReadString(record, "magery_name"),
                ReadNestedString(record, "source", "file"),
                ReadNestedLong(record, "source", "record_offset"),
                ReadNestedString(record, "source", "classification"));
        }
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static IReadOnlyDictionary<string, int> ReadIntDictionary(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, int>();
        }

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in values.EnumerateObject())
        {
            if (TryReadInt(property.Value, out var number)) result[property.Name] = number;
        }
        return result;
    }


    private static bool TryReadInt(JsonElement value, out int number)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number)) return true;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return true;
        number = 0;
        return false;
    }

    private static IReadOnlyList<RuntimeAbilityGrant> ReadAbilitySlots(JsonElement element)
    {
        if (!element.TryGetProperty("ability_slots", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RuntimeAbilityGrant>();
        }

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object)
            .Select(value => new RuntimeAbilityGrant(ReadInt(value, "ability_id"), ReadInt(value, "value")))
            .Where(value => value.AbilityId > 0)
            .ToArray();
    }
    private static RuntimeClassHitPointDefinition? ReadHitPoints(JsonElement element)
    {
        if (element.TryGetProperty("hit_points", out var hitPoints) && hitPoints.ValueKind == JsonValueKind.Object)
        {
            return new RuntimeClassHitPointDefinition(
                ReadInt(hitPoints, "minimum_per_level"),
                ReadInt(hitPoints, "maximum_bonus_per_level"),
                ReadInt(hitPoints, "maximum_per_level"));
        }

        var minimum = ReadInt(element, "min_hits");
        var bonus = ReadInt(element, "max_hits_bonus");
        return minimum > 0 || bonus > 0
            ? new RuntimeClassHitPointDefinition(minimum, bonus, minimum + bonus)
            : null;
    }

    private static RuntimeMagicDefinition? ReadMagic(JsonElement element)
    {
        if (element.TryGetProperty("magic", out var magic) && magic.ValueKind == JsonValueKind.Object)
        {
            return new RuntimeMagicDefinition(ReadInt(magic, "type"), ReadInt(magic, "level"));
        }

        var type = ReadInt(element, "magery_type");
        var level = ReadInt(element, "magery_level");
        return type > 0 || level > 0 ? new RuntimeMagicDefinition(type, level) : null;
    }

    private static string ReadNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (!element.TryGetProperty(objectName, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return ReadString(nested, propertyName);
    }

    private static long ReadNestedLong(JsonElement element, string objectName, string propertyName)
    {
        if (!element.TryGetProperty(objectName, out var nested) || nested.ValueKind != JsonValueKind.Object ||
            !nested.TryGetProperty(propertyName, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number) ? number : 0;
    }

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}

public sealed class RuntimeCharacterRepository
{
    public const int MaximumCharactersPerAccountPerWorld = 3;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _hostRoot;
    private readonly string _projectRoot;
    private readonly string _persistenceRoot;
    private readonly object _gate = new();

    public RuntimeCharacterRepository(string projectRoot, string? persistenceRoot = null)
    {
        _projectRoot = projectRoot;
        _persistenceRoot = string.IsNullOrWhiteSpace(persistenceRoot) ? projectRoot : Path.GetFullPath(persistenceRoot);
        _hostRoot = RuntimeSavePaths.HostRoot(_persistenceRoot);
        Directory.CreateDirectory(Path.Combine(_hostRoot, "accounts"));
        Directory.CreateDirectory(Path.Combine(RuntimeSavePaths.RealmRoot(_persistenceRoot, RuntimeSavePaths.ClassicWorldId), "characters"));
        StartupRecoverySweep();
    }

    public IReadOnlyList<string> StartupRecoverySweep()
    {
        var recovered = new List<string>();
        lock (_gate)
        {
            foreach (var path in CharacterDirectories()
                         .SelectMany(directory => Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var result = RuntimeCharacterPersistencePolicy.Load(path, JsonOptions);
                if (result.Source == "missing") continue;
                var migrated = RuntimeClassicRoomCurrencyStore.MigrateLegacyCurrency(_projectRoot, result.Character);
                if (result.Recovered || !ReferenceEquals(migrated, result.Character))
                {
                    var reason = result.Recovered ? "startup-recovery" : "legacy-currency-migration";
                    var repaired = RuntimeCharacterPersistencePolicy.PrepareForSave(migrated, reason, DateTimeOffset.UtcNow);
                    WriteAtomic(path, repaired, retainBackup: true);
                    recovered.Add(repaired.CharacterId);
                }
            }
        }
        return recovered;
    }

    public RuntimeAccountRecord GetOrCreateAccount(string accountName)
    {
        var accountId = RuntimeIdentity.Normalize(accountName, "account");
        lock (_gate)
        {
            var existing = TryLoadAccount(accountId);
            if (existing is not null)
            {
                return existing;
            }

            var now = DateTimeOffset.UtcNow;
            var created = new RuntimeAccountRecord(accountId, accountName.Trim(), Array.Empty<string>(), now, now);
            WriteAtomic(AccountPath(accountId), created);
            return created;
        }
    }

    public RuntimeAccountRecord? TryLoadAccount(string accountId) => Read<RuntimeAccountRecord>(AccountPath(RuntimeIdentity.Normalize(accountId, "account")));
    public bool AccountExists(string accountName) => TryLoadAccount(accountName) is not null;
    public RuntimeCharacterRecord? TryLoadCharacter(string characterId)
    {
        var path = CharacterPath(CanonicalCharacterId(characterId));
        var result = RuntimeCharacterPersistencePolicy.Load(path, JsonOptions);
        if (result.Source == "missing") return null;
        var migrated = RuntimeClassicRoomCurrencyStore.MigrateLegacyCurrency(_projectRoot, result.Character);
        if (result.Recovered || !ReferenceEquals(migrated, result.Character))
        {
            var reason = result.Recovered ? "crash-recovery" : "legacy-currency-migration";
            var recovered = RuntimeCharacterPersistencePolicy.PrepareForSave(migrated, reason, DateTimeOffset.UtcNow);
            WriteAtomic(path, recovered, retainBackup: true);
            return recovered;
        }
        return migrated;
    }

    public IReadOnlyList<RuntimeCharacterRecord> ListCharacters(string accountId)
    {
        var account = TryLoadAccount(accountId);
        if (account is null)
        {
            return Array.Empty<RuntimeCharacterRecord>();
        }

        return account.CharacterIds
            .Select(TryLoadCharacter)
            .Where(value => value is not null)
            .Cast<RuntimeCharacterRecord>()
            .OrderBy(value => value.DisplayName)
            .ToArray();
    }


    public IReadOnlyList<RuntimeCharacterRecord> ListAllCharacters()
    {
        lock (_gate)
        {
            var result = new List<RuntimeCharacterRecord>();
            foreach (var path in CharacterDirectories()
                         .SelectMany(directory => Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                var loaded = RuntimeCharacterPersistencePolicy.Load(path, JsonOptions);
                if (loaded.Source == "missing") continue;
                result.Add(RuntimeClassicRoomCurrencyStore.MigrateLegacyCurrency(_projectRoot, loaded.Character));
            }
            return result.OrderBy(character => character.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public RuntimeCharacterRecord CreateCharacter(RuntimeAccountRecord account, RuntimeCharacterRecord character)
    {
        lock (_gate)
        {
            if (TryLoadCharacter(character.CharacterId) is not null)
            {
                throw new InvalidOperationException($"Character '{character.DisplayName}' already exists.");
            }

            var existingInWorld = account.CharacterIds
                .Select(TryLoadCharacter)
                .Where(value => value is not null)
                .Cast<RuntimeCharacterRecord>()
                .Count(value => value.WorldId.Equals(character.WorldId, StringComparison.OrdinalIgnoreCase)
                    && RuntimeClassicCharacterProgressionFormula.ReadLives(value) > 0);
            if (existingInWorld >= MaximumCharactersPerAccountPerWorld)
            {
                throw new InvalidOperationException($"Account '{account.DisplayName}' already has {MaximumCharactersPerAccountPerWorld} characters in realm '{character.WorldId}'.");
            }

            character = RuntimeCharacterPersistencePolicy.PrepareForSave(character, "create", DateTimeOffset.UtcNow);
            WriteAtomic(CharacterPath(character.CharacterId), character, retainBackup: true);
            var ids = account.CharacterIds.Concat(new[] { character.CharacterId }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            WriteAtomic(AccountPath(account.AccountId), account with { CharacterIds = ids, UpdatedAtUtc = DateTimeOffset.UtcNow });
            return character;
        }
    }

    public RuntimeCharacterRecord ReplaceCharacter(RuntimeAccountRecord account, RuntimeCharacterRecord previous, RuntimeCharacterRecord replacement)
    {
        lock (_gate)
        {
            var current = TryLoadCharacter(previous.CharacterId)
                ?? throw new InvalidOperationException($"Character '{previous.DisplayName}' was not found for reroll.");
            if (!string.Equals(current.AccountId, account.AccountId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Reroll character/account ownership mismatch.");
            if (!string.Equals(current.WorldId, replacement.WorldId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Reroll cannot move a character between realms.");

            var sameId = current.CharacterId.Equals(replacement.CharacterId, StringComparison.OrdinalIgnoreCase);
            if (!sameId && TryLoadCharacter(replacement.CharacterId) is not null)
                throw new InvalidOperationException($"Character '{replacement.DisplayName}' already exists.");

            var accountIds = account.CharacterIds.ToArray();
            var slot = Array.FindIndex(accountIds, id => id.Equals(current.CharacterId, StringComparison.OrdinalIgnoreCase));
            if (slot < 0) throw new InvalidOperationException("Reroll source character is not present in the account slot list.");

            var oldPath = CharacterPath(current.CharacterId);
            var newPath = CharacterPath(replacement.CharacterId);
            var prepared = RuntimeCharacterPersistencePolicy.PrepareForSave(replacement, "reroll-replacement", DateTimeOffset.UtcNow);
            var updatedIds = accountIds.ToArray();
            updatedIds[slot] = prepared.CharacterId;
            if (updatedIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != updatedIds.Length)
                throw new InvalidOperationException("Reroll replacement would duplicate an existing character slot.");

            // Write the replacement and account slot first. If the logical ID changed, only
            // then remove the departed character. No old-character backup is retained by this
            // destructive replacement path: REROLL intentionally discards the prior character.
            WriteAtomic(newPath, prepared, retainBackup: false);
            try
            {
                WriteAtomic(AccountPath(account.AccountId), account with
                {
                    CharacterIds = updatedIds,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
            catch
            {
                if (sameId) WriteAtomic(oldPath, current, retainBackup: false);
                else TryDelete(newPath);
                throw;
            }

            if (!sameId)
            {
                TryDelete(oldPath);
                TryDelete(oldPath + ".bak");
            }
            TryDelete(newPath + ".bak");
            return prepared;
        }
    }

    public RuntimeCharacterRecord DeleteCharacter(string accountId, string characterId, string worldId)
    {
        lock (_gate)
        {
            var normalizedAccountId = RuntimeIdentity.Normalize(accountId, "account");
            var normalizedCharacterId = CanonicalCharacterId(characterId);
            var account = TryLoadAccount(normalizedAccountId)
                ?? throw new InvalidOperationException($"Account '{accountId}' was not found.");
            var character = TryLoadCharacter(normalizedCharacterId)
                ?? throw new InvalidOperationException($"Character '{characterId}' was not found.");

            if (!character.AccountId.Equals(account.AccountId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Character/account ownership mismatch.");
            if (!character.WorldId.Equals(worldId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Character '{character.DisplayName}' belongs to realm '{character.WorldId}', not '{worldId}'.");
            if (!account.CharacterIds.Any(id => CanonicalCharacterId(id).Equals(normalizedCharacterId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Character is not present in the account slot list.");

            var characterPath = CharacterPath(normalizedCharacterId);
            var updatedAccount = account with
            {
                CharacterIds = account.CharacterIds
                    .Where(id => !CanonicalCharacterId(id).Equals(normalizedCharacterId, StringComparison.OrdinalIgnoreCase))
                    .ToArray(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            WriteAtomic(AccountPath(account.AccountId), updatedAccount);
            try
            {
                File.Delete(characterPath);
                File.Delete(characterPath + ".bak");
            }
            catch
            {
                WriteAtomic(characterPath, character, retainBackup: false);
                WriteAtomic(AccountPath(account.AccountId), account);
                throw;
            }

            return character;
        }
    }

    public void SaveCharacter(RuntimeCharacterRecord character, string reason = "runtime-mutation")
    {
        lock (_gate)
        {
            var updated = RuntimeCharacterPersistencePolicy.PrepareForSave(character, reason, DateTimeOffset.UtcNow);
            WriteAtomic(CharacterPath(character.CharacterId), updated, retainBackup: true);
        }
    }

    public T ExecuteCharacterTransaction<T>(string firstCharacterId, string secondCharacterId, Func<RuntimeCharacterRecord, RuntimeCharacterRecord, (RuntimeCharacterRecord First, RuntimeCharacterRecord Second, T Result)> action)
    {
        lock (_gate)
        {
            var first = TryLoadCharacter(firstCharacterId) ?? throw new InvalidOperationException($"Character '{firstCharacterId}' was not found.");
            var second = TryLoadCharacter(secondCharacterId) ?? throw new InvalidOperationException($"Character '{secondCharacterId}' was not found.");
            var outcome = action(first, second);
            var firstPath = CharacterPath(first.CharacterId);
            var secondPath = CharacterPath(second.CharacterId);
            try
            {
                WriteAtomic(firstPath, RuntimeCharacterPersistencePolicy.PrepareForSave(outcome.First, "character-transaction", DateTimeOffset.UtcNow), retainBackup: true);
                WriteAtomic(secondPath, RuntimeCharacterPersistencePolicy.PrepareForSave(outcome.Second, "character-transaction", DateTimeOffset.UtcNow), retainBackup: true);
                return outcome.Result;
            }
            catch
            {
                WriteAtomic(firstPath, first, retainBackup: true);
                WriteAtomic(secondPath, second, retainBackup: true);
                throw;
            }
        }
    }

    public T ExecuteCharacterBatchTransaction<T>(
        IReadOnlyCollection<string> characterIds,
        Func<IReadOnlyDictionary<string, RuntimeCharacterRecord>, (IReadOnlyDictionary<string, RuntimeCharacterRecord> Characters, T Result)> action)
    {
        ArgumentNullException.ThrowIfNull(characterIds);
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            var requested = characterIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(CanonicalCharacterId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (requested.Length == 0) throw new InvalidOperationException("character-transaction-empty");

            var originals = new Dictionary<string, RuntimeCharacterRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in requested)
            {
                var character = TryLoadCharacter(id) ?? throw new InvalidOperationException($"Character '{id}' was not found.");
                originals[character.CharacterId] = character;
            }

            var outcome = action(new Dictionary<string, RuntimeCharacterRecord>(originals, StringComparer.OrdinalIgnoreCase));
            if (outcome.Characters.Count != originals.Count
                || originals.Keys.Any(id => !outcome.Characters.ContainsKey(id)))
                throw new InvalidOperationException("character-transaction-set-mismatch");

            var now = DateTimeOffset.UtcNow;
            var prepared = outcome.Characters.ToDictionary(
                pair => pair.Key,
                pair => RuntimeCharacterPersistencePolicy.PrepareForSave(pair.Value, "character-batch-transaction", now),
                StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var id in requested)
                    WriteAtomic(CharacterPath(id), prepared[id], retainBackup: true);
                return outcome.Result;
            }
            catch
            {
                foreach (var id in requested)
                    WriteAtomic(CharacterPath(id), originals[id], retainBackup: true);
                throw;
            }
        }
    }

    private string AccountPath(string id) => Path.Combine(_hostRoot, "accounts", id + ".json");
    private string CharacterPath(string id)
    {
        id = CanonicalCharacterId(id);
        return Path.Combine(RuntimeSavePaths.RealmRoot(_persistenceRoot, RuntimeSavePaths.WorldIdFromCharacterId(id)), "characters", PersistenceFileKey(id) + ".json");
    }

    private IEnumerable<string> CharacterDirectories()
    {
        var classic = Path.Combine(RuntimeSavePaths.RealmRoot(_persistenceRoot, RuntimeSavePaths.ClassicWorldId), "characters");
        if (Directory.Exists(classic)) yield return classic;

        var customRoot = Path.Combine(_persistenceRoot, "Saves", "Custom");
        if (!Directory.Exists(customRoot)) yield break;
        foreach (var directory in Directory.EnumerateDirectories(customRoot, "characters", SearchOption.AllDirectories))
            yield return directory;
    }

    private static string CanonicalCharacterId(string characterId)
    {
        var raw = (characterId ?? string.Empty).Trim();
        var separator = raw.IndexOf(':');
        if (separator < 0)
        {
            return RuntimeIdentity.Normalize(raw, "character");
        }

        var world = RuntimeIdentity.Normalize(raw[..separator], "world");
        var name = RuntimeIdentity.Normalize(raw[(separator + 1)..], "character");
        return $"{world}:{name}";
    }

    private static string PersistenceFileKey(string stableId)
    {
        // Character IDs are logical identifiers and may contain ':' (for example
        // "classic:hero"). A colon is not a legal normal filename character on
        // Windows and can be interpreted as an alternate data stream, causing
        // File.Move/File.Replace to fail with ERROR_INVALID_PARAMETER. Keep the
        // logical ID inside the JSON record and use a deterministic SHA-256 key
        // only for the on-disk filename.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(stableId));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static T? Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }

    private static void WriteAtomic<T>(string path, T value, bool retainBackup = false)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Persistence path '{path}' has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporary = Path.Combine(directory, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var backup = path + ".bak";
        var payload = JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine;

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(payload);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }

            try
            {
                File.Replace(temporary, path, backup, ignoreMetadataErrors: true);
                if (!retainBackup) TryDelete(backup);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithCopyFallback(temporary, path, backup, retainBackup);
            }
            catch (IOException)
            {
                // Some Windows filesystems and mounted/synchronized folders reject
                // File.Replace/File.Move overwrite semantics with ERROR_INVALID_PARAMETER.
                ReplaceWithCopyFallback(temporary, path, backup, retainBackup);
            }
            catch (UnauthorizedAccessException)
            {
                // Antivirus, indexing, and synchronized folders can briefly deny
                // File.Replace even though overwrite-copy access is available.
                ReplaceWithCopyFallback(temporary, path, backup, retainBackup);
            }
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static void ReplaceWithCopyFallback(string temporary, string path, string backup, bool retainBackup)
    {
        File.Copy(path, backup, overwrite: true);
        try
        {
            File.Copy(temporary, path, overwrite: true);
            if (!retainBackup) TryDelete(backup);
        }
        catch
        {
            if (File.Exists(backup))
            {
                File.Copy(backup, path, overwrite: true);
            }

            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup failure must not hide the persistence result. A later save can
            // safely replace stale .tmp/.bak files because names are unique/versioned.
        }
    }
}

public sealed class RuntimeCharacterService
{
    private readonly RuntimeCharacterRepository _repository;
    private readonly string _projectRoot;
    private readonly RuntimeCharacterLifecycleCoordinator _lifecycle = new();

    public RuntimeCharacterService(string projectRoot, string? persistenceRoot = null)
    {
        _projectRoot = projectRoot;
        _repository = new RuntimeCharacterRepository(projectRoot, persistenceRoot);
    }

    public string ProjectRoot => _projectRoot;
    public RuntimeCharacterLifecycleCoordinator Lifecycle => _lifecycle;
    public IReadOnlyList<RuntimeCharacterNativeDependencyDecision> CharacterNativeDependencies => _lifecycle.NativeDependencies;
    public RuntimeCharacterNativeDependencyDecision GetCharacterNativeDependencyDecision(RuntimeCharacterNativeDependency dependency) => _lifecycle.GetNativeDependencyDecision(dependency);
    public void RequireExecutableCharacterNativeDependency(RuntimeCharacterNativeDependency dependency) => _lifecycle.RequireExecutableNativeDependency(dependency);
    public RuntimeAccountRecord GetOrCreateAccount(string accountName) => _repository.GetOrCreateAccount(accountName);
    public RuntimeAccountRecord? TryLoadAccount(string accountName) => _repository.TryLoadAccount(accountName);
    public bool AccountExists(string accountName) => _repository.AccountExists(accountName);
    public RuntimeCharacterCatalog GetCatalog(string worldId) => RuntimeCharacterCatalog.Load(_projectRoot, worldId);
    public IReadOnlyList<RuntimeCharacterRecord> ListCharacters(string accountId) => _repository.ListCharacters(accountId);
    public int MaximumCharactersPerAccountPerWorld => RuntimeCharacterRepository.MaximumCharactersPerAccountPerWorld;
    public IReadOnlyList<RuntimeCharacterRecord> ListAllCharacters() => _repository.ListAllCharacters();
    public RuntimeCharacterRecord? LoadCharacter(string id) => _repository.TryLoadCharacter(id);
    public RuntimeCharacterRecord DeleteCharacter(string accountId, string characterId, string worldId) =>
        _repository.DeleteCharacter(accountId, characterId, worldId);
    public RuntimeCharacterRecord LoginCharacter(string characterId, string sessionId, RuntimeCharacterLifecycleHooks hooks, bool reconnect = false) =>
        _lifecycle.Login(characterId, sessionId, _repository.TryLoadCharacter, hooks, DateTimeOffset.UtcNow, reconnect);
    public RuntimeCharacterRecord LogoutCharacter(RuntimeCharacterRecord character, string sessionId, RuntimeCharacterLifecycleHooks hooks, bool graceful = true) =>
        _lifecycle.Logout(character, sessionId, hooks, _repository.SaveCharacter, DateTimeOffset.UtcNow, graceful);
    public RuntimeCharacterRecord RecoverStaleCharacterSession(RuntimeCharacterRecord character, string staleSessionId, RuntimeCharacterLifecycleHooks hooks) =>
        _lifecycle.RecoverStaleSession(character, staleSessionId, hooks, _repository.SaveCharacter, DateTimeOffset.UtcNow);
    public void SaveCharacter(RuntimeCharacterRecord character, string reason = "runtime-mutation") => _repository.SaveCharacter(character, reason);
    public IReadOnlyList<string> StartupRecoverySweep() => _repository.StartupRecoverySweep();

    public int AutosaveSessions(IEnumerable<RuntimeSession> sessions, DateTimeOffset now, TimeSpan interval)
    {
        var saved = 0;
        foreach (var session in sessions.OrderBy(x => x.Actor.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (session.Actor is not RuntimeCharacterActor actor) continue;
            var character = _repository.TryLoadCharacter(actor.Id);
            if (character is null || !RuntimeCharacterPersistencePolicy.IsAutosaveDue(character, now, interval)) continue;
            var snapshot = RuntimeCharacterPersistencePolicy.MarkAutosave(character with
            {
                WorldId = session.World.Definition.Id, RoomId = session.CurrentRoom.Id, RoomNumber = session.CurrentRoom.RoomNumber,
                Level = actor.Level, UpdatedAtUtc = now
            }, now);
            _repository.SaveCharacter(snapshot, "autosave");
            saved++;
        }
        return saved;
    }
    public T ExecuteCharacterTransaction<T>(string firstCharacterId, string secondCharacterId, Func<RuntimeCharacterRecord, RuntimeCharacterRecord, (RuntimeCharacterRecord First, RuntimeCharacterRecord Second, T Result)> action) => _repository.ExecuteCharacterTransaction(firstCharacterId, secondCharacterId, action);
    public T ExecuteCharacterBatchTransaction<T>(IReadOnlyCollection<string> characterIds, Func<IReadOnlyDictionary<string, RuntimeCharacterRecord>, (IReadOnlyDictionary<string, RuntimeCharacterRecord> Characters, T Result)> action) => _repository.ExecuteCharacterBatchTransaction(characterIds, action);

    public RuntimeCharacterRecord CreateCharacter(RuntimeAccountRecord account, RuntimeWorld world, string name, string sex, string race, string characterClass) =>
        CreateCharacter(account, world, name, sex, race, characterClass, null, name, string.Empty, "short", "brown", "brown", 100);

    public RuntimeCharacterRecord CreateCharacter(
        RuntimeAccountRecord account,
        RuntimeWorld world,
        string name,
        string sex,
        string race,
        string characterClass,
        IReadOnlyDictionary<string, int>? selectedStats,
        string givenName,
        string familyName,
        string hairLength,
        string hairColor,
        string eyeColor,
        int creationPointsRemaining)
    {
        ValidateClassicGivenNameAvailability(world, givenName);
        var character = BuildCharacterRecord(account, world, name, sex, race, characterClass, selectedStats, givenName, familyName, hairLength, hairColor, eyeColor, creationPointsRemaining);
        return _repository.CreateCharacter(account, character);
    }

    public RuntimeCharacterRecord RerollCharacter(
        RuntimeAccountRecord account,
        RuntimeWorld world,
        RuntimeCharacterRecord previous,
        string name,
        string sex,
        string race,
        string characterClass,
        IReadOnlyDictionary<string, int>? selectedStats,
        string givenName,
        string familyName,
        string hairLength,
        string hairColor,
        string eyeColor,
        int creationPointsRemaining)
    {
        if (!previous.AccountId.Equals(account.AccountId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reroll character/account ownership mismatch.");
        if (!previous.WorldId.Equals(world.Definition.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reroll must create the replacement character in the same realm.");

        var oldCatalog = RuntimeCharacterCatalog.Load(_projectRoot, previous.WorldId);
        var oldRace = oldCatalog.RequireRace(previous.RaceId);
        var oldClass = oldCatalog.RequireClass(previous.ClassId);
        var chart = RuntimeClassicCharacterProgressionFormula.ExperienceChartPercent(oldRace, oldClass);
        var keepEnabled = previous.Conditions.TryGetValue("ui.classic.keep.enabled", out var keepRaw)
            && keepRaw.Equals("1", StringComparison.OrdinalIgnoreCase);
        var retainedExperience = RuntimeClassicDeathExperienceAuthority.CalculateFinalRerollRetainedExperience(
            previous.Experience, previous.Level, chart, keepEnabled, RuntimeClassicConfigurationAuthority.KeepExperienceAfterFinalDeath);

        ValidateClassicGivenNameAvailability(world, givenName, previous.CharacterId);
        var replacement = BuildCharacterRecord(account, world, name, sex, race, characterClass, selectedStats, givenName, familyName, hairLength, hairColor, eyeColor, creationPointsRemaining) with
        {
            Experience = retainedExperience,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        return _repository.ReplaceCharacter(account, previous, replacement);
    }

    private void ValidateClassicGivenNameAvailability(RuntimeWorld world, string givenName, string? excludedCharacterId = null)
    {
        var requested = givenName.Trim();
        var playerCollision = _repository.ListAllCharacters().Any(character =>
            (excludedCharacterId is null || !character.CharacterId.Equals(excludedCharacterId, StringComparison.OrdinalIgnoreCase)) &&
            RuntimeIdentity.ClassicGivenNameFromDisplayName(character.DisplayName).Equals(requested, StringComparison.OrdinalIgnoreCase));
        var monsterCollision = world.Npcs.Values.Any(npc => npc.Name.Trim().Equals(requested, StringComparison.OrdinalIgnoreCase));
        var commandCollision = RuntimeClassicNativePlayerCommandRegistry.Load(_projectRoot, world.Definition.Id).Records
            .Select(record => RuntimeCommand.Parse(record.DocumentedInput).Name)
            .Any(command => command.Equals(requested, StringComparison.OrdinalIgnoreCase));

        if (playerCollision || monsterCollision || commandCollision)
            throw new InvalidOperationException(RuntimeIdentity.ClassicNameAlreadyInUseMessage);
    }

    private RuntimeCharacterRecord BuildCharacterRecord(
        RuntimeAccountRecord account,
        RuntimeWorld world,
        string name,
        string sex,
        string race,
        string characterClass,
        IReadOnlyDictionary<string, int>? selectedStats,
        string givenName,
        string familyName,
        string hairLength,
        string hairColor,
        string eyeColor,
        int creationPointsRemaining)
    {
        // Classic character identity is composed of separately validated given and family names.
        // The display name may contain the separator space and must not be rejected as a single token.
        RuntimeIdentity.ValidateClassicGivenName(givenName);
        if (!string.IsNullOrWhiteSpace(familyName))
            RuntimeIdentity.ValidateClassicFamilyName(familyName);
        var normalizedSex = NormalizeSex(sex);
        var catalog = RuntimeCharacterCatalog.Load(_projectRoot, world.Definition.Id);
        var raceDefinition = catalog.RequireRace(race);
        var classDefinition = catalog.RequireClass(characterClass);
        var creation = catalog.CreationContract;
        if (catalog.AlignmentRuleContract.CreationBlocking)
        {
            throw new InvalidOperationException("Decoded Classic alignment rules block character creation.");
        }

        if (!catalog.CreationGateReady)
        {
            var blockers = catalog.CreationGateBlockers.Count == 0 ? "unknown blockers" : string.Join(", ", catalog.CreationGateBlockers);
            throw new InvalidOperationException($"Character Foundation Phase003 creation gate is blocked: {blockers}.");
        }

        if (!creation.AllRaceClassCombinationsAllowed || creation.AllowedPairCount != catalog.Races.Count * catalog.Classes.Count)
        {
            throw new InvalidOperationException("Generated character creation race/class matrix is incomplete or inconsistent.");
        }

        var start = world.ResolveGeneratedCharacterStartRoom()
            ?? throw new InvalidOperationException($"World '{world.Definition.Id}' has no generated Classic character-start room. Character creation is blocked rather than guessing a start.");
        if (!string.Equals(start.Id, creation.StartingRoomId, StringComparison.OrdinalIgnoreCase) || start.RoomNumber != creation.StartingRoomNumber)
        {
            throw new InvalidOperationException($"Generated character start room '{start.Id}' does not match promoted creation contract '{creation.StartingRoomId}'.");
        }
        var characterId = RuntimeIdentity.CharacterId(world.Definition.Id, name);
        var now = DateTimeOffset.UtcNow;
        const int startingLevel = 1;
        var baseStats = selectedStats is null
            ? new Dictionary<string, int>(raceDefinition.MinimumStats, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(selectedStats, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in raceDefinition.MinimumStats)
        {
            var value = baseStats.TryGetValue(pair.Key, out var selected) ? selected : pair.Value;
            var maximum = raceDefinition.MaximumStats.TryGetValue(pair.Key, out var max) ? max : pair.Value;
            if (value < pair.Value || value > maximum)
                throw new InvalidOperationException($"Stat '{pair.Key}' must be between {pair.Value} and {maximum}.");
            baseStats[pair.Key] = value;
        }
        var derivedStatistics = RuntimeCharacterDerivedStatisticCalculator.CalculateCreation(
            raceDefinition,
            classDefinition,
            startingLevel,
            baseStats,
            equipmentManaCache: catalog.DerivedFormulaContract.CreationAbilityAndEquipmentCache);
        var maximumHitPoints = derivedStatistics.MaximumHitPoints;
        var maximumMana = derivedStatistics.MaximumMana;

        // Native _create_player consumes the permanent-info retained-XP field once after
        // core creation inputs are validated. Delay the destructive read until this point
        // so failed character-creation attempts cannot consume the pending reroll value.
        var retainedRerollExperience = RuntimeClassicPermanentDeathInfoStore.Consume(
            _projectRoot, account.AccountId, world.Definition.Id);

        var startingAbilities = raceDefinition.AbilitySlots
            .Concat(classDefinition.AbilitySlots)
            .GroupBy(value => value.AbilityId)
            .ToDictionary(
                group => $"ability.{group.Key}",
                group => group.Last().Value,
                StringComparer.OrdinalIgnoreCase);
        var character = new RuntimeCharacterRecord(
            characterId,
            account.AccountId,
            name.Trim(),
            world.Definition.Id,
            raceDefinition.Id,
            raceDefinition.Name,
            classDefinition.Id,
            classDefinition.Name,
            startingLevel,
            retainedRerollExperience,
            maximumHitPoints,
            maximumHitPoints,
            maximumMana,
            maximumMana,
            start.Id,
            start.RoomNumber,
            baseStats,
            startingAbilities,
            creation.StartingSpells,
            creation.StartingInventory,
            new Dictionary<string, string?>(),
            new Dictionary<string, string>
            {
                ["alignment"] = creation.StartingAlignment,
                ["alignment_value"] = creation.StartingAlignmentValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["alignment_classification"] = creation.StartingAlignmentClassification,
                ["evil_points"] = catalog.AlignmentRuleContract.InitialEvilPoints.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["online_alignment_minutes"] = "0",
                ["starting_gold"] = creation.StartingGold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["race_experience_add_percent"] = raceDefinition.ExperienceAddPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["race_base_creation_points"] = raceDefinition.BaseCreationPoints.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["creation_points"] = Math.Max(0, creationPointsRemaining).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["lives"] = RuntimeClassicCharacterProgressionFormula.MaximumLives.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [RuntimeClassicDeathExperienceAuthority.KeepExperienceConditionKey] = "1",
                ["classic.reroll_retained_experience_consumed"] = retainedRerollExperience.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["given_name"] = givenName ?? string.Empty,
                ["family_name"] = familyName ?? string.Empty,
                ["hair_length"] = hairLength ?? string.Empty,
                ["hair_color"] = hairColor ?? string.Empty,
                ["eye_color"] = eyeColor ?? string.Empty,
                ["combat.attack_value"] = Math.Max(1, (ReadStat(baseStats, "strength", "str") + ReadStat(baseStats, "agility", "agi", "agl")) / 2 + startingLevel).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["combat.defense_value"] = Math.Max(1, (ReadStat(baseStats, "agility", "agi", "agl") + ReadStat(baseStats, "health", "hea")) / 2 + startingLevel).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["combat.minimum_damage"] = "1",
                ["combat.maximum_damage"] = Math.Max(2, 2 + ReadStat(baseStats, "strength", "str") / 25).ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            now,
            now,
            RuntimeCharacterRecordValidator.CurrentSaveVersion,
            null,
            RuntimeCharacterSemanticState.Empty with { Alignment = RuntimeCharacterAlignmentResolver.CreateInitial(catalog.AlignmentRuleContract) },
            normalizedSex);
        return character;
    }

    private static int ReadStat(IReadOnlyDictionary<string, int> values, params string[] keys)
    {
        foreach (var key in keys)
            if (values.TryGetValue(key, out var value)) return value;
        return 0;
    }

    private static string NormalizeSex(string sex)
    {
        var value = (sex ?? string.Empty).Trim();
        if (string.Equals(value, "male", StringComparison.OrdinalIgnoreCase)) return "Male";
        if (string.Equals(value, "female", StringComparison.OrdinalIgnoreCase)) return "Female";
        throw new InvalidOperationException("Character sex must be Male or Female.");
    }

    public void SaveSession(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
        {
            return;
        }

        var existing = _repository.TryLoadCharacter(actor.Id);
        if (existing is null)
        {
            return;
        }

        _repository.SaveCharacter(existing with
        {
            WorldId = session.World.Definition.Id,
            RoomId = session.CurrentRoom.Id,
            RoomNumber = session.CurrentRoom.RoomNumber,
            Level = actor.Level,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        }, "disconnect");
    }
}

public static class RuntimeIdentity
{
    // Native UserRecType / AddUserFieldMap plus WCCMMUD.DLL creation validation.
    public const int ClassicGivenNameMinimumLength = 1;
    public const int ClassicGivenNameMaximumLength = 10;
    public const int ClassicFamilyNameMinimumLength = 1;
    public const int ClassicFamilyNameMaximumLength = 18;
    // User-supplied WCCMMUD.INI capture for the target installation has BLANK_LASTNAME_ALLOWED=1.
    public const bool ClassicBlankLastNameAllowed = true;
    public const string ClassicInvalidGivenNameMessage = "You have entered an invalid name!";
    public const string ClassicLastNameRequiredMessage = "You MUST have a last name!";
    public const string ClassicInvalidFamilyNameMessage = "Your last name contains invalid characters!";
    public const string ClassicNameAlreadyInUseMessage = "The name you have chosen is already in use!";

    private static bool IsClassicAlphabeticToken(string value) =>
        value.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    public static void ValidateCharacterName(string name) => ValidateClassicGivenName(name);

    public static void ValidateClassicGivenName(string name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length < ClassicGivenNameMinimumLength || value.Length > ClassicGivenNameMaximumLength || !IsClassicAlphabeticToken(value))
            throw new InvalidOperationException(ClassicInvalidGivenNameMessage);
    }

    public static void ValidateClassicFamilyName(string name, bool allowBlank = ClassicBlankLastNameAllowed)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            if (allowBlank) return;
            throw new InvalidOperationException(ClassicLastNameRequiredMessage);
        }
        if (value.Length < ClassicFamilyNameMinimumLength || value.Length > ClassicFamilyNameMaximumLength || !IsClassicAlphabeticToken(value))
            throw new InvalidOperationException(ClassicInvalidFamilyNameMessage);
    }

    public static string ClassicGivenNameFromDisplayName(string displayName)
    {
        var value = (displayName ?? string.Empty).Trim();
        var separator = value.IndexOf(' ');
        return separator < 0 ? value : value[..separator];
    }

    public static string CharacterId(string worldId, string name) => $"{Normalize(worldId, "world")}:{Normalize(name, "character")}";

    public static string Normalize(string value, string fallback)
    {
        var safe = new string((value ?? string.Empty).Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (safe.Contains("--", StringComparison.Ordinal))
        {
            safe = safe.Replace("--", "-", StringComparison.Ordinal);
        }

        return safe.Length == 0 ? fallback : safe;
    }
}
