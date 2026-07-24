using System.Text.Json;
using System.Globalization;

namespace Reforged.Server;

// Consolidated from RuntimeCharacterClassCapabilityCatalog.cs
public sealed record RuntimeClassCapabilityProfile(
    string ClassId,
    int LegacyClassNumber,
    string ClassName,
    int MagicFamily,
    int MagicRank,
    string MagicFamilyName,
    int WeaponTypeCode,
    int ArmourTypeCode,
    int CombatLevelCode,
    string EvidenceStatus,
    string Source);

public sealed record RuntimeSpellEligibilityProfile(
    string SpellId,
    int LegacySpellNumber,
    string SpellName,
    int RequiredMagicFamily,
    int RequiredMagicRank,
    int SpellLevel,
    string EvidenceStatus,
    string Source);

public sealed record RuntimeClassEligibilityDecision(bool Allowed, string Reason, string EvidenceStatus);

/// <summary>
/// Authoritative projection of decoded class capability fields.  It intentionally
/// keeps raw weapon/armour/combat codes visible while only executing relationships
/// whose semantics are fully evidenced.  Magic family/rank eligibility is executable
/// because both sides of that relationship are present in generated Classic data.
/// </summary>
public sealed class RuntimeCharacterClassCapabilityCatalog
{
    private readonly Dictionary<string, RuntimeClassCapabilityProfile> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeSpellEligibilityProfile> _spells = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<RuntimeClassCapabilityProfile> Classes => _classes.Values.GroupBy(x => x.ClassId, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).OrderBy(x => x.LegacyClassNumber).ToArray();
    public IReadOnlyCollection<RuntimeSpellEligibilityProfile> Spells => _spells.Values.OrderBy(x => x.LegacySpellNumber).ToArray();

    public static RuntimeCharacterClassCapabilityCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterClassCapabilityCatalog();
        catalog.LoadClasses(Path.Combine(projectRoot, "Data", worldId, "character_foundation.json"));
        catalog.LoadSpells(Path.Combine(projectRoot, "Data", worldId, "spells.json"));
        return catalog;
    }

    public RuntimeClassCapabilityProfile RequireClass(string idOrName)
    {
        if (_classes.TryGetValue(idOrName.Trim(), out var exact)) return exact;
        var byName = _classes.Values.FirstOrDefault(x => x.ClassName.Equals(idOrName.Trim(), StringComparison.OrdinalIgnoreCase));
        return byName ?? throw new InvalidOperationException($"Unknown generated Classic class '{idOrName}'.");
    }

    public RuntimeSpellEligibilityProfile RequireSpell(string spellId)
        => _spells.TryGetValue(spellId, out var spell)
            ? spell
            : throw new InvalidOperationException($"Unknown generated Classic spell '{spellId}'.");

    public RuntimeClassEligibilityDecision CanPurchaseSpell(string classIdOrName, string spellId)
    {
        var characterClass = RequireClass(classIdOrName);
        var spell = RequireSpell(spellId);

        if (spell.RequiredMagicFamily <= 0)
            return new(false, "Spell has no decoded purchasable magic-family requirement.", "decoded_but_not_mapped");
        if (characterClass.MagicFamily <= 0)
            return new(false, "Class has no decoded spell family.", "verified");
        if (characterClass.MagicFamily != spell.RequiredMagicFamily)
            return new(false, $"Spell requires magic family {spell.RequiredMagicFamily}; class uses {characterClass.MagicFamily}.", "verified");
        if (characterClass.MagicRank < spell.RequiredMagicRank)
            return new(false, $"Spell requires magic rank {spell.RequiredMagicRank}; class has rank {characterClass.MagicRank}.", "verified");

        return new(true, "Decoded magic family and rank permit this spell.", "verified");
    }

    public RuntimeClassEligibilityDecision CanUseWeaponType(string classIdOrName, int itemWeaponType)
    {
        var characterClass = RequireClass(classIdOrName);
        if (itemWeaponType <= 0) return new(true, "Item has no weapon-type restriction.", "verified");
        return new(false,
            $"Class weapon code {characterClass.WeaponTypeCode} and item weapon type {itemWeaponType} are projected, but the decoded code relationship is not yet mapped.",
            "relationship_unresolved");
    }

    public RuntimeClassEligibilityDecision CanUseArmourType(string classIdOrName, int itemArmourType)
    {
        var characterClass = RequireClass(classIdOrName);
        if (itemArmourType <= 0) return new(true, "Item has no armour-type restriction.", "verified");
        return new(false,
            $"Class armour code {characterClass.ArmourTypeCode} and item armour type {itemArmourType} are projected, but the decoded code relationship is not yet mapped.",
            "relationship_unresolved");
    }

    private void LoadClasses(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in document.RootElement.GetProperty("classes").EnumerateArray())
        {
            var number = ReadInt(record, "number");
            var name = ReadString(record, "name");
            var fields = PublicFields(record);
            var magicFamily = ReadInt(record, "magery_type");
            var magicRank = ReadInt(record, "magery_level");
            var profile = new RuntimeClassCapabilityProfile(
                $"class.{number}", number, name, magicFamily, magicRank,
                ReadString(record, "magery_name"),
                ReadInt(fields, "weapon_bonus"),
                ReadInt(fields, "armor_bonus"),
                ReadInt(fields, "combat_bonus"),
                "strongly_correlated",
                ReadSource(fields, record));
            _classes[profile.ClassId] = profile;
            _classes[name] = profile;
        }
    }

    private void LoadSpells(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in document.RootElement.GetProperty("records").EnumerateArray())
        {
            var fields = PublicFields(record);
            var id = ReadString(record, "id");
            var legacy = int.TryParse(ReadString(record, "legacy_id"), out var parsed) ? parsed : ReadInt(fields, "legacy_number");
            _spells[id] = new RuntimeSpellEligibilityProfile(
                id, legacy, ReadString(record, "name"),
                ReadInt(fields, "magery_requirement_a"),
                ReadInt(fields, "magery_requirement_b"),
                ReadInt(fields, "spell_level"),
                "strongly_correlated",
                ReadSource(fields, record));
        }
    }

    private static JsonElement PublicFields(JsonElement record)
    {
        if (record.TryGetProperty("public_reference_0139", out var reference)
            && reference.ValueKind == JsonValueKind.Object
            && reference.TryGetProperty("fields", out var fields)
            && fields.ValueKind == JsonValueKind.Object) return fields;
        return default;
    }

    private static string ReadSource(JsonElement fields, JsonElement record)
    {
        if (record.TryGetProperty("public_reference_0139", out var reference)
            && reference.ValueKind == JsonValueKind.Object
            && reference.TryGetProperty("source", out var source)
            && source.ValueKind == JsonValueKind.String) return source.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static int ReadInt(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static string ReadString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}

// Consolidated from RuntimeCharacterItemRequirementCatalog.cs
public sealed record RuntimeCharacterItemRequirement(
    string ItemId,
    int LegacyItemNumber,
    string ItemName,
    int MinimumLevel,
    IReadOnlyList<int> AllowedClasses,
    IReadOnlyList<int> AllowedRaces,
    IReadOnlyDictionary<string, int> MinimumStats,
    int WeaponType,
    int ArmourType,
    string EvidenceStatus,
    string DecodedSource);

public sealed record RuntimeCharacterItemRequirementDecision(bool Allowed, string Reason, RuntimeCharacterItemRequirement Requirement);

/// <summary>
/// Character-side projection of decoded item eligibility inputs. Bundle A owns
/// character level/class/race/stat evaluation; Bundle C owns live equip/use and
/// inventory mutation. No weapon/armour code compatibility is guessed here.
/// </summary>
public sealed class RuntimeCharacterItemRequirementCatalog
{
    private readonly Dictionary<string, RuntimeCharacterItemRequirement> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<RuntimeCharacterItemRequirement> Items => _items.Values.OrderBy(x => x.LegacyItemNumber).ToArray();

    public static RuntimeCharacterItemRequirementCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterItemRequirementCatalog();
        var path = Path.Combine(projectRoot, "Data", worldId, "items.json");
        if (!File.Exists(path)) return catalog;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var record in document.RootElement.GetProperty("records").EnumerateArray()) catalog.Project(record);
        return catalog;
    }

    public RuntimeCharacterItemRequirement Require(string itemId)
        => _items.TryGetValue(itemId, out var item) ? item : throw new InvalidOperationException($"Unknown generated Classic item '{itemId}'.");

    public RuntimeCharacterItemRequirementDecision EvaluateCharacterInputs(RuntimeCharacterRecord character, string itemId)
    {
        var item = Require(itemId);
        var classNumber = LegacyNumber(character.ClassId);
        var raceNumber = LegacyNumber(character.RaceId);
        if (character.Level < item.MinimumLevel) return new(false, $"Requires level {item.MinimumLevel}.", item);
        if (item.AllowedClasses.Count > 0 && !item.AllowedClasses.Contains(classNumber)) return new(false, "Character class is not permitted by the decoded item class list.", item);
        if (item.AllowedRaces.Count > 0 && !item.AllowedRaces.Contains(raceNumber)) return new(false, "Character race is not permitted by the decoded item race list.", item);
        foreach (var requirement in item.MinimumStats)
        {
            var actual = character.BaseStats.TryGetValue(requirement.Key, out var value) ? value : 0;
            if (actual < requirement.Value) return new(false, $"Requires {requirement.Key.ToUpperInvariant()} {requirement.Value}.", item);
        }
        return new(true, "Decoded character level, class, race, and stat requirements passed; Bundle C must still enforce weapon/armour category compatibility and live equip/use state.", item);
    }

    private void Project(JsonElement record)
    {
        var id = ReadString(record, "id");
        if (string.IsNullOrWhiteSpace(id)) return;
        var fields = PublicFields(record);
        var abilities = fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty("abilities_named", out var a) && a.ValueKind == JsonValueKind.Array ? a : default;
        var minimumLevel = 0;
        if (abilities.ValueKind == JsonValueKind.Array)
            foreach (var ability in abilities.EnumerateArray())
                if (ReadInt(ability, "ability_id") == 135 || ReadString(ability, "name").Equals("MinLevel", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(ReadString(ability, "value"), out minimumLevel);

        var classes = ReadIntArray(fields, "class_restrictions");
        var races = ReadIntArray(fields, "race_restrictions");
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty("stat_requirements", out var statFields) && statFields.ValueKind == JsonValueKind.Object)
            foreach (var property in statFields.EnumerateObject()) if (property.Value.TryGetInt32(out var value) && value > 0) stats[property.Name] = value;
        var properties = fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty("properties", out var p) ? p : default;
        var legacy = int.TryParse(ReadString(record, "legacy_id"), out var parsed) ? parsed : ReadInt(fields, "legacy_number");
        _items[id] = new RuntimeCharacterItemRequirement(id, legacy, ReadString(record, "name"), minimumLevel, classes, races, stats,
            ReadInt(properties, "weapon_type"), ReadInt(properties, "armor_type"), "verified", "items.json public_reference_0139.fields");
    }

    private static int LegacyNumber(string id)
    {
        var index = id.LastIndexOf('.');
        return index >= 0 && int.TryParse(id[(index + 1)..], out var value) ? value : 0;
    }
    private static IReadOnlyList<int> ReadIntArray(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Where(x => x.TryGetInt32(out _)).Select(x => x.GetInt32()).Distinct().ToArray() : Array.Empty<int>();
    private static JsonElement PublicFields(JsonElement record)
        => record.TryGetProperty("public_reference_0139", out var reference) && reference.ValueKind == JsonValueKind.Object && reference.TryGetProperty("fields", out var fields) ? fields : default;
    private static int ReadInt(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
    private static string ReadString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}

// Consolidated from RuntimeCharacterLearningCatalog.cs
public sealed record RuntimeCharacterSpellAcquisitionSource(
    string SourceId,
    string Channel,
    string DisplayName,
    int MinimumLevel,
    int MaximumLevel,
    string ClassLimit,
    IReadOnlyList<string> OfferedItemIds,
    bool SpellRelationshipResolved,
    string EvidenceStatus);

public sealed record RuntimeCharacterSpellItemRelationship(
    string ItemId,
    string ItemName,
    string SpellId,
    string SpellName,
    string EvidenceStatus);


public sealed record RuntimeCharacterQuestSpellReference(
    string QuestId,
    string QuestName,
    string QuestFlagName,
    int TextblockId,
    string Opcode,
    string SpellId,
    string SpellName,
    bool GrantsPermanentOwnership,
    string EvidenceStatus);


public sealed record RuntimeCharacterLevel40QuestCandidate(
    string ClassName,
    string NpcId,
    string NpcName,
    string LocationName,
    string RewardItemId,
    string RewardItemName,
    string RewardSpellId,
    string RewardSpellName,
    IReadOnlyList<string> RequiredItemIds,
    IReadOnlyList<string> RequiredItemNames,
    IReadOnlyList<string> UnresolvedItemNames,
    bool TurnInScriptResolved,
    bool RewardItemUseLevelResolved,
    int RewardItemMinimumUseLevel,
    string EvidenceStatus,
    int TurnInTextblockId);


public sealed record RuntimeCharacterLevel50AlignmentQuestStage(
    string AlignmentPath,
    string QuestFlagName,
    int QuestAbilityId,
    int RequiredStageValue,
    string NpcId,
    int SourceTextblockId,
    int RewardTextblockId,
    int MinimumLevel,
    bool PermanentSpellGrantResolved,
    string EvidenceStatus);

public sealed record RuntimeCharacterLearningDefinition(
    string Id,
    string Name,
    string Kind,
    string Classification,
    string Confidence,
    string RuntimeMappingStatus,
    bool ExecutableLearningRelationship,
    string SourceFile);

/// <summary>
/// Authoritative character-owned skill/spell identity projection. This catalog validates persisted
/// ownership without inventing trainer, class, level, or cost relationships that the decoded data
/// has not mapped yet.
/// </summary>
public sealed class RuntimeCharacterLearningCatalog
{
    private readonly Dictionary<string, RuntimeCharacterLearningDefinition> _skills = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeCharacterLearningDefinition> _spells = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RuntimeCharacterSpellAcquisitionSource> _spellAcquisitionSources = new();
    private readonly Dictionary<string, List<RuntimeCharacterSpellItemRelationship>> _spellItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RuntimeCharacterQuestSpellReference> _questSpellReferences = new();
    private readonly List<RuntimeCharacterLevel40QuestCandidate> _level40QuestCandidates = new();
    private readonly List<RuntimeCharacterLevel50AlignmentQuestStage> _level50AlignmentQuestStages = new();

    public IReadOnlyCollection<RuntimeCharacterLearningDefinition> Skills => _skills.Values.OrderBy(x => x.Id).ToArray();
    public IReadOnlyCollection<RuntimeCharacterLearningDefinition> Spells => _spells.Values.OrderBy(x => x.Id).ToArray();
    public IReadOnlyList<RuntimeCharacterSpellAcquisitionSource> SpellAcquisitionSources => _spellAcquisitionSources.OrderBy(x => x.Channel).ThenBy(x => x.SourceId).ToArray();
    public IReadOnlyCollection<RuntimeCharacterSpellItemRelationship> SpellItemRelationships => _spellItems.Values.SelectMany(x => x).OrderBy(x => x.ItemId).ThenBy(x => x.SpellId).ToArray();
    public IReadOnlyList<RuntimeCharacterQuestSpellReference> QuestSpellReferences => _questSpellReferences.OrderBy(x => x.QuestId).ThenBy(x => x.TextblockId).ThenBy(x => x.SpellId).ToArray();
    public IReadOnlyList<RuntimeCharacterLevel40QuestCandidate> Level40QuestCandidates => _level40QuestCandidates.OrderBy(x => x.ClassName).ToArray();
    public IReadOnlyList<RuntimeCharacterLevel50AlignmentQuestStage> Level50AlignmentQuestStages => _level50AlignmentQuestStages.OrderBy(x => x.AlignmentPath).ToArray();

    public static RuntimeCharacterLearningCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterLearningCatalog();
        catalog.LoadFile(Path.Combine(projectRoot, "Data", worldId, "skills.json"), "skill", catalog._skills);
        catalog.LoadFile(Path.Combine(projectRoot, "Data", worldId, "spells.json"), "spell", catalog._spells);
        catalog.LoadSpellItemRelationships(projectRoot, worldId);
        catalog.LoadQuestSpellReferences(projectRoot, worldId);
        catalog.LoadLevel40QuestCandidates(projectRoot, worldId);
        catalog.LoadLevel50AlignmentQuestStages(projectRoot, worldId);
        catalog.LoadSpellAcquisitionSources(projectRoot, worldId);
        return catalog;
    }

    public RuntimeCharacterLearningDefinition RequireSkill(string id) => Require(_skills, id, "skill");
    public RuntimeCharacterLearningDefinition RequireSpell(string id) => Require(_spells, id, "spell");
    public bool IsKnownSkill(string id) => _skills.ContainsKey(id);
    public bool IsKnownSpell(string id) => _spells.ContainsKey(id);
    public RuntimeCharacterLevel40QuestCandidate RequireLevel40Quest(string className) =>
        _level40QuestCandidates.FirstOrDefault(x => x.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"No level 40 class-spell quest candidate exists for class '{className}'.");
    public IReadOnlyList<RuntimeCharacterSpellItemRelationship> ResolveSpellItem(string itemId) =>
        _spellItems.TryGetValue(itemId, out var relationships) ? relationships : Array.Empty<RuntimeCharacterSpellItemRelationship>();

    public bool TryResolveSpellItem(string itemId, out RuntimeCharacterSpellItemRelationship relationship)
    {
        var relationships = ResolveSpellItem(itemId);
        relationship = relationships.Count == 1 ? relationships[0] : null!;
        return relationships.Count == 1;
    }

    public void ValidateOwnership(RuntimeCharacterRecord character)
    {
        foreach (var skill in character.Skills.Keys) RequireSkill(skill);
        foreach (var spell in character.Spells) RequireSpell(spell);
    }


    public RuntimeCharacterRecord GrantSpell(RuntimeCharacterRecord character, string spellId, string acquisitionSourceId)
    {
        var spell = RequireSpell(spellId);
        if (string.IsNullOrWhiteSpace(acquisitionSourceId))
            throw new InvalidOperationException("Spell acquisition requires an authoritative source id.");
        var normalized = spell.Id;
        if (character.Spells.Contains(normalized, StringComparer.OrdinalIgnoreCase)) return character;
        var spells = character.Spells.Concat(new[] { normalized }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        var conditions = new Dictionary<string, string>(character.Conditions, StringComparer.OrdinalIgnoreCase)
        {
            [$"spell_acquisition:{normalized}"] = acquisitionSourceId.Trim()
        };
        return character with { Spells = spells, Conditions = conditions, UpdatedAtUtc = DateTimeOffset.UtcNow };
    }


    private void LoadSpellItemRelationships(string projectRoot, string worldId)
    {
        var itemsPath = Path.Combine(projectRoot, "Data", worldId, "items.json");
        if (!File.Exists(itemsPath)) return;

        var spellsByLegacyId = _spells.Values
            .Select(spell => (Spell: spell, LegacyId: TryReadLegacyNumber(spell.Id)))
            .Where(x => x.LegacyId > 0)
            .ToDictionary(x => x.LegacyId, x => x.Spell);

        using var itemsDocument = JsonDocument.Parse(File.ReadAllText(itemsPath));
        foreach (var item in itemsDocument.RootElement.GetProperty("records").EnumerateArray())
        {
            var itemId = ReadString(item, "id");
            var itemName = ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(itemId)) continue;

            foreach (var learnSpellNumber in ReadLearnSpellAbilities(item).Distinct())
            {
                if (!spellsByLegacyId.TryGetValue(learnSpellNumber, out var spell))
                    throw new InvalidDataException($"Item '{itemId}' has decoded LearnSp ability {learnSpellNumber}, but generated spells contain no matching legacy spell.");

                if (!_spellItems.TryGetValue(itemId, out var itemRelationships))
                {
                    itemRelationships = new List<RuntimeCharacterSpellItemRelationship>();
                    _spellItems[itemId] = itemRelationships;
                }
                itemRelationships.Add(new RuntimeCharacterSpellItemRelationship(
                    itemId, itemName, spell.Id, spell.Name,
                    "verified: decoded item Ability 42 (LearnSp) directly references canonical legacy spell number"));
            }
        }
    }

    private static IReadOnlyList<int> ReadLearnSpellAbilities(JsonElement item)
    {
        if (!item.TryGetProperty("public_reference_0139", out var publicReference) || publicReference.ValueKind != JsonValueKind.Object ||
            !publicReference.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty("abilities_named", out var abilities) || abilities.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        return abilities.EnumerateArray()
            .Where(ability => ReadInt(ability, "ability_id") == 42 || ReadString(ability, "name").Equals("LearnSp", StringComparison.OrdinalIgnoreCase))
            .Select(ability => ReadInt(ability, "value"))
            .Where(value => value > 0)
            .ToArray();
    }

    private static int TryReadLegacyNumber(string canonicalId)
    {
        var separator = canonicalId.LastIndexOf('.');
        var raw = separator >= 0 ? canonicalId[(separator + 1)..] : canonicalId;
        return int.TryParse(raw, out var value) ? value : 0;
    }

    private void LoadQuestSpellReferences(string projectRoot, string worldId)
    {
        var questsPath = Path.Combine(projectRoot, "Data", worldId, "quests.json");
        if (!File.Exists(questsPath)) return;

        using var document = JsonDocument.Parse(File.ReadAllText(questsPath));
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return;

        foreach (var quest in records.EnumerateArray())
        {
            var questId = ReadString(quest, "id");
            var questName = ReadString(quest, "name");
            var flagName = ReadString(quest, "quest_flag_name");
            ScanQuestElement(quest, questId, questName, flagName, 0);
        }
    }

    private void ScanQuestElement(JsonElement element, string questId, string questName, string flagName, int inheritedTextblockId)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) ScanQuestElement(child, questId, questName, flagName, inheritedTextblockId);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;

        var textblockId = ReadInt(element, "textblock_id");
        if (textblockId <= 0) textblockId = inheritedTextblockId;
        var opcode = ReadString(element, "opcode");
        var spellId = ReadString(element, "spell_ref");
        if (string.IsNullOrWhiteSpace(spellId))
        {
            var legacySpell = ReadInt(element, "spell_id");
            if (legacySpell > 0) spellId = $"spell.{legacySpell}";
        }

        if (!string.IsNullOrWhiteSpace(opcode) && !string.IsNullOrWhiteSpace(spellId) && _spells.TryGetValue(spellId, out var spell))
        {
            var grantsOwnership = opcode.Equals("learnspell", StringComparison.OrdinalIgnoreCase) ||
                                  opcode.Equals("givespell", StringComparison.OrdinalIgnoreCase) ||
                                  opcode.Equals("grantspell", StringComparison.OrdinalIgnoreCase);
            _questSpellReferences.Add(new RuntimeCharacterQuestSpellReference(
                questId, questName, flagName, textblockId, opcode, spell.Id, spell.Name, grantsOwnership,
                grantsOwnership
                    ? "decoded: explicit permanent spell grant opcode"
                    : "decoded: spell reference is not a permanent ownership grant"));
        }

        foreach (var property in element.EnumerateObject())
            ScanQuestElement(property.Value, questId, questName, flagName, textblockId);
    }

    private void LoadLevel40QuestCandidates(string projectRoot, string worldId)
    {
        var itemsPath = Path.Combine(projectRoot, "Data", worldId, "items.json");
        if (!File.Exists(itemsPath)) return;

        using var itemsDocument = JsonDocument.Parse(File.ReadAllText(itemsPath));
        var itemsByName = itemsDocument.RootElement.GetProperty("records").EnumerateArray()
            .Where(x => !string.IsNullOrWhiteSpace(ReadString(x, "name")))
            .GroupBy(x => ReadString(x, "name"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(y => ReadString(y, "id")).Where(y => y.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);

        Add("Mage", "item.1287", "spell.802", new[] { "pixie wings", "pulsating heart", "eye of a giant", "crocodile fang" });
        Add("Priest", "item.1285", "spell.804", new[] { "small glass flask", "spider silk", "scorpion tail", "piece of shell" });
        Add("Druid", "item.1288", "spell.805", new[] { "severed earth dragon claw", "frozen shard", "displacer beast tentacle", "spider leg" });

        void Add(string className, string rewardItemId, string spellId, IReadOnlyList<string> itemNames)
        {
            if (!_spells.TryGetValue(spellId, out var spell))
                throw new InvalidDataException($"Level 40 {className} quest evidence references missing canonical spell '{spellId}'.");
            var rewardItem = itemsDocument.RootElement.GetProperty("records").EnumerateArray()
                .SingleOrDefault(item => ReadString(item, "id").Equals(rewardItemId, StringComparison.OrdinalIgnoreCase));
            if (rewardItem.ValueKind == JsonValueKind.Undefined)
                throw new InvalidDataException($"Level 40 {className} quest evidence references missing canonical reward item '{rewardItemId}'.");
            var rewardItemName = ReadString(rewardItem, "name");
            var rewardRelationships = ResolveSpellItem(rewardItemId);
            if (!rewardRelationships.Any(x => x.SpellId.Equals(spellId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Level 40 {className} reward item '{rewardItemId}' does not contain decoded LearnSp relationship '{spellId}'.");
            var resolved = new List<string>();
            var unresolved = new List<string>();
            foreach (var name in itemNames)
            {
                // The retained walkthrough calls this component "eye of a giant". The canonical
                // decoded item is item.479, "cyclops eye". This alias is corroborated by the
                // gnome-merchant Magma Blast reward fix in the original WCC release notes.
                var canonicalName = name.Equals("eye of a giant", StringComparison.OrdinalIgnoreCase)
                    ? "cyclops eye"
                    : name;
                if (itemsByName.TryGetValue(canonicalName, out var ids) && ids.Length == 1) resolved.Add(ids[0]);
                else unresolved.Add(name);
            }
            _level40QuestCandidates.Add(new RuntimeCharacterLevel40QuestCandidate(
                className, "npc.716", "gnome merchant", "Shallow Jungle", rewardItemId, rewardItemName, spell.Id, spell.Name,
                resolved, itemNames, unresolved, true, false, 0,
                "verified reward item identity and decoded Ability 42 LearnSp relationship; decoded gnome merchant root textblock 2798; item minimum-use field remains unresolved",
                2798));
        }
    }

    private void LoadLevel50AlignmentQuestStages(string projectRoot, string worldId)
    {
        var questsPath = Path.Combine(projectRoot, "Data", worldId, "quests.json");
        if (!File.Exists(questsPath)) return;

        using var document = JsonDocument.Parse(File.ReadAllText(questsPath));
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return;

        var expected = new Dictionary<string, (int AbilityId, int StageValue)>(StringComparer.OrdinalIgnoreCase)
        {
            ["GoodQuest"] = (126, 19),
            ["NeutralQuest"] = (127, 13),
            ["EvilQuest"] = (128, 14)
        };

        foreach (var quest in records.EnumerateArray())
        {
            var flagName = ReadString(quest, "quest_flag_name");
            if (!expected.TryGetValue(flagName, out var expectedStage)) continue;
            if (!quest.TryGetProperty("relationships", out var relationships) || relationships.ValueKind != JsonValueKind.Object) continue;
            var npcId = ReadString(relationships, "npc");
            if (!quest.TryGetProperty("script_flow", out var flow) || flow.ValueKind != JsonValueKind.Object ||
                !flow.TryGetProperty("stages", out var stages) || stages.ValueKind != JsonValueKind.Array) continue;

            foreach (var stage in stages.EnumerateArray())
            {
                if (!stage.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array) continue;
                var ops = operations.EnumerateArray().ToArray();
                var level50Line = ops
                    .Where(op => ReadString(op, "opcode").Equals("minlevel", StringComparison.OrdinalIgnoreCase) &&
                                 op.TryGetProperty("numeric_arguments", out var nums) && nums.ValueKind == JsonValueKind.Array &&
                                 nums.EnumerateArray().Any(n => n.TryGetInt32(out var value) && value == 50))
                    .Select(op => ReadInt(op, "line"))
                    .FirstOrDefault(line => line > 0);
                if (level50Line <= 0) continue;

                var hasStage = ops.Any(op => ReadInt(op, "line") == level50Line &&
                    (ReadString(op, "opcode").Equals("testability", StringComparison.OrdinalIgnoreCase) ||
                     ReadString(op, "opcode").Equals("checkability", StringComparison.OrdinalIgnoreCase)) &&
                    ReadInt(op, "ability_id") == expectedStage.AbilityId && ReadInt(op, "ability_value") == expectedStage.StageValue);
                if (!hasStage) continue;

                var rewardTextblock = ops
                    .Where(op => ReadInt(op, "line") == level50Line && ReadString(op, "opcode").Equals("text", StringComparison.OrdinalIgnoreCase))
                    .Select(op => ReadInt(op, "target_textblock_id"))
                    .FirstOrDefault(value => value > 0);
                if (rewardTextblock <= 0) continue;

                var alignmentPath = flagName[..^"Quest".Length];
                var candidate = new RuntimeCharacterLevel50AlignmentQuestStage(
                    alignmentPath, flagName, expectedStage.AbilityId, expectedStage.StageValue, npcId,
                    ReadInt(stage, "textblock_id"), rewardTextblock, 50, false,
                    "verified decoded level-50 alignment quest stage gate; permanent reward spell relationship remains unresolved");

                if (!_level50AlignmentQuestStages.Any(x => x.AlignmentPath.Equals(candidate.AlignmentPath, StringComparison.OrdinalIgnoreCase) &&
                                                            x.NpcId.Equals(candidate.NpcId, StringComparison.OrdinalIgnoreCase) &&
                                                            x.RewardTextblockId == candidate.RewardTextblockId))
                    _level50AlignmentQuestStages.Add(candidate);
            }
        }
    }

    private void LoadSpellAcquisitionSources(string projectRoot, string worldId)
    {
        var shopsPath = Path.Combine(projectRoot, "Data", worldId, "shops.json");
        var itemsPath = Path.Combine(projectRoot, "Data", worldId, "items.json");
        if (File.Exists(shopsPath) && File.Exists(itemsPath))
        {
            using var itemsDocument = JsonDocument.Parse(File.ReadAllText(itemsPath));
            var itemNames = itemsDocument.RootElement.GetProperty("records").EnumerateArray()
                .Where(x => x.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                .ToDictionary(x => ReadString(x, "id"), x => ReadString(x, "name"), StringComparer.OrdinalIgnoreCase);
            using var shopsDocument = JsonDocument.Parse(File.ReadAllText(shopsPath));
            foreach (var shop in shopsDocument.RootElement.GetProperty("records").EnumerateArray())
            {
                // The generated service_kind value is not reliable enough to identify spell shops by itself.
                // The authoritative relationship is the shop inventory item -> decoded LearnSp item mapping.
                var offered = shop.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Array
                    ? inventory.EnumerateArray()
                        .Select(x => ReadString(x, "item_ref"))
                        .Where(itemId => itemId.Length > 0 && itemNames.ContainsKey(itemId) && _spellItems.ContainsKey(itemId))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(itemId => itemId, StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : Array.Empty<string>();
                if (offered.Length == 0) continue;
                var range = shop.TryGetProperty("level_range", out var levelRange) && levelRange.ValueKind == JsonValueKind.Object ? levelRange : default;
                _spellAcquisitionSources.Add(new RuntimeCharacterSpellAcquisitionSource(
                    ReadString(shop, "id"), "spell_shop_scroll", ReadString(shop, "name"),
                    ReadInt(range, "min"), ReadInt(range, "max"), ReadString(shop, "class_limit"), offered,
                    offered.All(itemId => _spellItems.ContainsKey(itemId)),
                    "verified: every offered inventory item resolves through the decoded LearnSp item relationship"));
            }
        }

        var level40Items = _level40QuestCandidates.Select(x => x.RewardItemId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        _spellAcquisitionSources.Add(new RuntimeCharacterSpellAcquisitionSource(
            "classic.level40.class.quest", "level_40_class_quest", "Gnome Merchant class-dependent scroll turn-in", 0, 99, "Mage/Priest/Druid",
            level40Items, _level40QuestCandidates.Count > 0 && _level40QuestCandidates.All(x => x.TurnInScriptResolved),
            "strongly-corroborated NPC, location, component, and reward identities; decoded turn-in script remains unresolved and live grant stays fail-closed"));
        _spellAcquisitionSources.Add(new RuntimeCharacterSpellAcquisitionSource(
            "classic.level50.alignment.quest5", "level_50_alignment_quest", "Fifth alignment quest spell", 50, 99, "alignment-dependent", Array.Empty<string>(),
            _level50AlignmentQuestStages.Count == 3 && _level50AlignmentQuestStages.All(x => x.PermanentSpellGrantResolved),
            _level50AlignmentQuestStages.Count == 3
                ? "verified decoded Good/Neutral/Evil level-50 stage gates; permanent reward spell relationship remains unresolved"
                : "decoded-but-not-mapped: one or more level-50 alignment stage gates are missing"));
    }

    private static int ReadInt(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : 0;
    }

    private void LoadFile(string path, string kind, Dictionary<string, RuntimeCharacterLearningDefinition> target)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Generated Classic {kind} catalog is missing.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Generated {kind} catalog has no records array.");

        foreach (var record in records.EnumerateArray())
        {
            var id = ReadString(record, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var status = record.TryGetProperty("runtime_mapping", out var runtime) && runtime.ValueKind == JsonValueKind.Object
                ? ReadString(runtime, "status") : string.Empty;
            var relationships = record.TryGetProperty("relationship_summary_0124", out var summary) && summary.ValueKind == JsonValueKind.Array && summary.GetArrayLength() > 0;
            var definition = new RuntimeCharacterLearningDefinition(
                id,
                ReadString(record, "name"),
                kind,
                ReadFlexible(record, "classification"),
                ReadFlexible(record, "confidence"),
                status,
                relationships && !status.Equals("pending", StringComparison.OrdinalIgnoreCase),
                ReadString(record, "librarium_source_file"));
            if (!target.TryAdd(id, definition)) throw new InvalidDataException($"Duplicate generated {kind} id '{id}'.");
        }
    }

    private static RuntimeCharacterLearningDefinition Require(Dictionary<string, RuntimeCharacterLearningDefinition> source, string id, string kind) =>
        source.TryGetValue(id.Trim(), out var definition)
            ? definition
            : throw new InvalidDataException($"Character save references unknown generated {kind} '{id}'.");

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string ReadFlexible(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
    }
}

// Consolidated from RuntimeCharacterTrainerCatalog.cs
/// <summary>
/// Typed projection of Classic trainer service records. A class_limit is a trainer admission
/// relationship, never character multiclassing: zero means all classes, a positive legacy class
/// number means that trainer admits only that class.
/// </summary>
public sealed record RuntimeCharacterTrainerRule(
    string TrainerId,
    string TrainerName,
    int MinimumLevel,
    int MaximumLevel,
    int ClassLimitLegacyNumber,
    string SourceClassification)
{
    public bool IsUniversalClassTrainer => ClassLimitLegacyNumber <= 0;

    public RuntimeCharacterTrainerAdmission Admit(RuntimeCharacterRecord character, int targetLevel)
    {
        if (targetLevel < MinimumLevel || targetLevel > MaximumLevel)
            return new(false, $"This trainer supports levels {MinimumLevel}-{MaximumLevel}; it cannot train you to level {targetLevel}.", this);

        var characterClass = ParseLegacyNumber(character.ClassId);
        if (!IsUniversalClassTrainer && characterClass != ClassLimitLegacyNumber)
            return new(false, "This trainer does not support your class.", this);

        return new(true, string.Empty, this);
    }

    private static int ParseLegacyNumber(string id)
    {
        var dot = id.LastIndexOf('.');
        return int.TryParse(dot >= 0 ? id[(dot + 1)..] : id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}

public sealed record RuntimeCharacterTrainerAdmission(bool Admitted, string FailureReason, RuntimeCharacterTrainerRule Trainer);

public static class RuntimeCharacterTrainerCatalog
{
    public static IReadOnlyList<RuntimeCharacterTrainerRule> Project(IReadOnlyList<RuntimeServiceDefinition> services)
    {
        return services
            .Where(service => ReadString(service.GeneratedRecord, "service_kind").Equals("trainer", StringComparison.OrdinalIgnoreCase))
            .Select(Project)
            .OrderBy(rule => rule.MinimumLevel)
            .ThenBy(rule => rule.TrainerId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static RuntimeCharacterTrainerRule RequireTrainer(IReadOnlyList<RuntimeServiceDefinition> services, RuntimeCharacterRecord character, int targetLevel)
    {
        var trainers = Project(services);
        if (trainers.Count == 0) throw new InvalidOperationException("There is no generated trainer in this room.");

        var admissions = trainers.Select(rule => rule.Admit(character, targetLevel)).ToArray();
        var admitted = admissions.FirstOrDefault(result => result.Admitted);
        if (admitted is not null) return admitted.Trainer;

        // Prefer the most specific decoded reason: class rejection at a trainer that supports the target level,
        // otherwise report the decoded level range rejection.
        var classFailure = admissions.FirstOrDefault(result =>
            targetLevel >= result.Trainer.MinimumLevel && targetLevel <= result.Trainer.MaximumLevel);
        throw new InvalidOperationException((classFailure ?? admissions[0]).FailureReason);
    }

    private static RuntimeCharacterTrainerRule Project(RuntimeServiceDefinition service)
    {
        var record = service.GeneratedRecord;
        var minimum = ReadNestedInt(record, "level_range", "min", 1);
        var maximum = ReadNestedInt(record, "level_range", "max", int.MaxValue);
        var classLimit = ReadInt(record, "class_limit", 0);
        return new RuntimeCharacterTrainerRule(
            service.Id,
            ReadString(record, "name", service.Id),
            minimum,
            maximum,
            classLimit,
            classLimit <= 0 ? "verified: universal trainer class admission" : "verified: class-restricted trainer admission");
    }

    private static int ReadNestedInt(JsonElement element, string container, string property, int fallback)
    {
        if (!element.TryGetProperty(container, out var nested) || nested.ValueKind != JsonValueKind.Object) return fallback;
        return ReadInt(nested, property, fallback);
    }

    private static int ReadInt(JsonElement element, string property, int fallback)
    {
        if (!element.TryGetProperty(property, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : fallback;
    }

    private static string ReadString(JsonElement element, string property, string fallback = "")
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }
}
