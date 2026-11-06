using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealmGate.App.Services;

public sealed record ClassicLibrariumCategory(string Key, string DisplayName);

public sealed record ClassicLibrariumRecordSummary(
    string Category,
    string Id,
    string Name,
    string Subheading);

public sealed record ClassicLibrariumField(string Name, string Value);

public sealed record ClassicLibrariumRecordDetails(
    string Category,
    string Id,
    string Name,
    string Subheading,
    IReadOnlyList<ClassicLibrariumField> Fields);

/// <summary>
/// Read-only, cross-platform browser over the generated Classic projection. Generated data is
/// consumed exactly as the runtime sees it; this service never edits Data or Librarium source.
/// </summary>
public sealed class ClassicLibrariumBrowserService
{
    private static readonly HashSet<string> HiddenSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "authority", "generated_by", "compiler_code_update", "source", "source_file", "source_files",
        "evidence", "evidence_refs", "evidence_ref", "provenance", "confidence", "promotion_note",
        "promotion_code_update", "public_reference_0139", "raw", "raw_hex", "unknown_fields"
    };

    private static readonly HashSet<string> GameplaySegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "description", "short_description", "long_description", "room_description", "type", "item_type",
        "weapon_type", "armor_type", "armor_type_name", "damage", "minimum_damage", "maximum_damage",
        "min_damage", "max_damage", "accuracy", "speed", "delay", "armor_class", "armor_class_bonus",
        "level", "hit_points", "hp", "mana", "experience", "experience_value", "gold", "cost", "cost_type",
        "weight", "slot", "equip_slot", "equipment_slot", "required_level", "required_class", "required_race",
        "alignment", "disposition", "hostility", "light", "light_level", "light_duration", "charges", "uses",
        "range", "target", "duration", "effect", "effects", "flags", "requirements", "prerequisites",
        "abilities", "ability_slots", "skills", "spells", "minimum_stats", "maximum_stats", "base_cp",
        "experience_add_percent", "hp_per_level_adjustment", "mana_per_level", "relationships", "exits",
        "destination", "destination_room", "from_room", "to_room", "map_number", "room_number", "room_type",
        "quest_flag_name", "quest_ability_id", "script_flow", "stages", "keywords", "responses", "actions",
        "inventory", "loot", "spawn", "spawn_room", "shop", "trainer", "bank", "movement", "combat",
        "classification", "economy", "attributes", "statistics", "restrictions", "casting", "progression",
        "service", "environment", "location", "attack", "defense"
    };

    private static readonly ClassicLibrariumCategory[] Categories =
    [
        new("weapons", "Weapons"),
        new("armor", "Armor"),
        new("items", "Items"),
        new("monsters", "Monsters"),
        new("rooms", "Rooms"),
        new("classes", "Classes"),
        new("races", "Races"),
        new("spells", "Spells"),
        new("abilities", "Abilities"),
        new("quests", "Quests"),
        new("exits", "Exits / Paths")
    ];

    private readonly string _dataRoot;

    public ClassicLibrariumBrowserService(string projectRoot)
    {
        _dataRoot = Path.Combine(Path.GetFullPath(projectRoot), "Data", "Classic");
    }

    public IReadOnlyList<ClassicLibrariumCategory> GetCategories() => Categories;

    public IReadOnlyList<ClassicLibrariumRecordSummary> Browse(
        string category,
        string? search,
        int limit = 2000)
    {
        category = NormalizeCategory(category);
        var query = search?.Trim() ?? string.Empty;
        if (category == "quests") return BrowseQuests(query, limit);
        var results = new List<ClassicLibrariumRecordSummary>();

        foreach (var record in EnumerateCategory(category))
        {
            if (!MatchesCategory(category, record)) continue;
            var id = ReadId(category, record);
            if (id.Length == 0) continue;
            var name = ReadName(category, record, id);
            if (query.Length > 0 &&
                !id.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !ContainsVisibleText(record, query))
            {
                continue;
            }

            results.Add(new ClassicLibrariumRecordSummary(
                category,
                id,
                name,
                BuildSubheading(category, record)));
            if (results.Count >= Math.Clamp(limit, 1, 5000)) break;
        }

        return results
            .OrderBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ClassicLibrariumRecordDetails Read(string category, string id)
    {
        category = NormalizeCategory(category);
        id = id?.Trim() ?? string.Empty;
        if (id.Length == 0) throw new InvalidOperationException("Select a Librarium record.");
        if (category == "quests") return ReadQuest(id);

        foreach (var record in EnumerateCategory(category))
        {
            if (!MatchesCategory(category, record) ||
                !ReadId(category, record).Equals(id, StringComparison.OrdinalIgnoreCase)) continue;

            var fields = BuildFields(record);
            return new ClassicLibrariumRecordDetails(
                category,
                id,
                ReadName(category, record, id),
                BuildSubheading(category, record),
                fields);
        }

        throw new KeyNotFoundException($"Classic Librarium record '{id}' was not found in {category}.");
    }

    private IReadOnlyList<ClassicLibrariumRecordSummary> BrowseQuests(string query, int limit)
    {
        return LoadQuestGroups()
            .Where(group => query.Length == 0 ||
                            group.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            group.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            group.Records.Any(record => ContainsVisibleText(record, query)))
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Id, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 5000))
            .Select(group => new ClassicLibrariumRecordSummary(
                "quests",
                group.Id,
                group.Name,
                $"{group.Records.Count:N0} NPC dialogue branch(es)"))
            .ToArray();
    }

    private ClassicLibrariumRecordDetails ReadQuest(string id)
    {
        var group = LoadQuestGroups().FirstOrDefault(candidate =>
            candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Classic quest '{id}' was not found.");
        var fields = new List<ClassicLibrariumField>
        {
            new("Quest Flag", group.FlagName),
            new("Dialogue Branches", group.Records.Count.ToString("N0"))
        };

        var flagIds = group.Records
            .Select(record => FirstValue(record, ["quest_ability_id"]))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (flagIds.Length > 0) fields.Add(new("Native Flag ID", string.Join(", ", flagIds)));

        var requiredItems = group.Records
            .SelectMany(ReadQuestItemReferences)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requiredItems.Length > 0) fields.Add(new("Required / Used Items", string.Join(", ", requiredItems)));

        var step = 1;
        foreach (var record in group.Records
                     .OrderBy(value => FirstValue(value, ["npc_name"], ["npc_ref"]), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(value => FirstValue(value, ["root_textblock_id"]), StringComparer.OrdinalIgnoreCase))
        {
            var npcRef = FirstValue(record, ["npc_ref"], ["relationships", "npc"]);
            var npcName = FirstValue(record, ["npc_name"]);
            var npc = npcName.Length == 0 ? npcRef : npcRef.Length == 0 ? npcName : $"{npcName} ({npcRef})";
            var root = FirstValue(record, ["relationships", "root_textblock"], ["root_textblock_id"]);
            fields.Add(new($"Branch {step} NPC", npc.Length == 0 ? "Unknown NPC" : npc));
            if (root.Length > 0) fields.Add(new($"Branch {step} Dialogue Root", root.StartsWith("textblock.", StringComparison.OrdinalIgnoreCase) ? root : "textblock." + root));

            if (TryPath(record, ["script_flow", "stages"], out var stages) && stages.ValueKind == JsonValueKind.Array)
            {
                foreach (var stage in stages.EnumerateArray())
                {
                    var stageRef = FirstValue(stage, ["textblock_ref"], ["textblock_id"]);
                    var actions = DescribeQuestStage(stage, group.FlagName);
                    fields.Add(new(
                        $"Step {step++}",
                        (stageRef.Length == 0 ? string.Empty : stageRef + ": ") +
                        (actions.Length == 0 ? "Continue the NPC dialogue." : actions)));
                }
            }
            else
            {
                fields.Add(new($"Step {step++}", "Continue the NPC dialogue."));
            }
        }

        return new ClassicLibrariumRecordDetails(
            "quests",
            group.Id,
            group.Name,
            $"{group.Records.Count:N0} NPC dialogue branch(es)",
            fields);
    }

    private IReadOnlyList<QuestGroup> LoadQuestGroups()
    {
        var records = EnumerateGenerated(Path.Combine(_dataRoot, "quests.json"))
            .Where(IsQuest)
            .ToArray();
        return records
            .GroupBy(QuestGroupKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0)
            .Select(group =>
            {
                var first = group.First();
                var flag = FirstValue(first, ["quest_flag_name"]);
                var root = FirstValue(first, ["relationships", "root_textblock"], ["root_textblock_id"]);
                var alignment = IsAlignmentQuest(flag) && root.Length > 0 ? " " + FriendlyName(root) : string.Empty;
                return new QuestGroup(
                    "quest.summary." + Slug(group.Key),
                    FriendlyName(flag) + alignment,
                    flag,
                    group.ToArray());
            })
            .ToArray();
    }

    private static string QuestGroupKey(JsonElement record)
    {
        var flag = FirstValue(record, ["quest_flag_name"]);
        if (flag.Length == 0) return string.Empty;
        if (!IsAlignmentQuest(flag)) return flag;
        var root = FirstValue(record, ["relationships", "root_textblock"], ["root_textblock_id"]);
        return root.Length == 0 ? flag : flag + "." + root;
    }

    private static bool IsAlignmentQuest(string flag) =>
        flag.Equals("GoodQuest", StringComparison.OrdinalIgnoreCase) ||
        flag.Equals("EvilQuest", StringComparison.OrdinalIgnoreCase) ||
        flag.Equals("NeutralQuest", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ReadQuestItemReferences(JsonElement record)
    {
        if (!TryPath(record, ["item_actions"], out var actions) || actions.ValueKind != JsonValueKind.Array) yield break;
        foreach (var action in actions.EnumerateArray())
        {
            var item = FirstValue(action, ["item_ref"], ["item_id"]);
            if (item.Length == 0) continue;
            yield return item.StartsWith("item.", StringComparison.OrdinalIgnoreCase) ? item : "item." + item;
        }
    }

    private static string DescribeQuestStage(JsonElement stage, string flagName)
    {
        if (!TryPath(stage, ["operations"], out var operations) || operations.ValueKind != JsonValueKind.Array)
            return Collapse(FirstValue(stage, ["script"]), 900);
        var actions = new List<string>();
        foreach (var operation in operations.EnumerateArray())
        {
            var opcode = FirstValue(operation, ["opcode"]).ToLowerInvariant();
            var flagValue = FirstValue(operation, ["ability_value"]);
            var item = FirstValue(operation, ["item_ref"], ["item_id"]);
            if (item.Length > 0 && !item.StartsWith("item.", StringComparison.OrdinalIgnoreCase)) item = "item." + item;
            var textblock = FirstValue(operation, ["target_textblock_id"]);
            actions.Add(opcode switch
            {
                "checkability" or "testability" => flagValue.Length == 0
                    ? $"check {flagName}"
                    : $"require {flagName} flag step {flagValue}",
                "failability" => $"handle the failed {flagName} state",
                "giveability" => flagValue.Length == 0
                    ? $"advance {flagName}"
                    : $"set {flagName} flag step {flagValue}",
                "takeitem" => item.Length == 0 ? "use the required item" : $"use {item}",
                "giveitem" => item.Length == 0 ? "receive an item" : $"receive {item}",
                "text" or "message" => textblock.Length == 0 ? "show the native response" : $"continue at textblock.{textblock}",
                _ => FriendlyName(opcode)
            });
        }
        return string.Join("; ", actions.Where(value => value.Length > 0));
    }

    private static string Slug(string value)
    {
        var normalized = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", ".").Trim('.');
        return normalized.Length == 0 ? "unknown" : normalized;
    }

    private IEnumerable<JsonElement> EnumerateCategory(string category)
    {
        EnsureDataRoot();
        return category switch
        {
            "classes" => EnumerateNested(Path.Combine(_dataRoot, "character_foundation.json"), "classes"),
            "races" => EnumerateNested(Path.Combine(_dataRoot, "character_foundation.json"), "races"),
            "weapons" or "armor" or "items" => EnumerateGenerated(Path.Combine(_dataRoot, "items.json")),
            "monsters" => EnumerateGenerated(Path.Combine(_dataRoot, "npcs.json")),
            "rooms" => EnumerateGenerated(Path.Combine(_dataRoot, "rooms.json")),
            "spells" => EnumerateGenerated(Path.Combine(_dataRoot, "spells.json")),
            "abilities" => EnumerateGenerated(Path.Combine(_dataRoot, "abilities.json")),
            "quests" => EnumerateGenerated(Path.Combine(_dataRoot, "quests.json")),
            "exits" => EnumerateGenerated(Path.Combine(_dataRoot, "exits.json")),
            _ => []
        };
    }

    private void EnsureDataRoot()
    {
        if (!Directory.Exists(_dataRoot))
            throw new DirectoryNotFoundException($"Generated Classic data was not found at '{_dataRoot}'.");
    }

    private static IEnumerable<JsonElement> EnumerateGenerated(string path)
    {
        if (!File.Exists(path)) yield break;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("record_parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            var expected = root.TryGetProperty("record_count", out var count) && count.TryGetInt32(out var value)
                ? value
                : (int?)null;
            var loaded = 0;
            foreach (var part in parts.EnumerateArray())
            {
                var relative = part.GetString();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var partPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(path) ?? string.Empty,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(partPath))
                    throw new FileNotFoundException($"Generated chunk listed by '{Path.GetFileName(path)}' was not found.", partPath);
                foreach (var record in EnumerateGenerated(partPath))
                {
                    loaded++;
                    yield return record;
                }
            }
            if (expected.HasValue && loaded != expected.Value)
                throw new InvalidDataException($"{Path.GetFileName(path)} declares {expected.Value:N0} records but loaded {loaded:N0}.");
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray()) yield return item.Clone();
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("records", out var records) &&
            records.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in records.EnumerateArray()) yield return item.Clone();
        }
    }

    private static IEnumerable<JsonElement> EnumerateNested(string path, string propertyName)
    {
        if (!File.Exists(path)) yield break;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty(propertyName, out var records) ||
            records.ValueKind != JsonValueKind.Array) yield break;
        foreach (var record in records.EnumerateArray()) yield return record.Clone();
    }

    private static bool MatchesCategory(string category, JsonElement record) => category switch
    {
        "weapons" => IsWeapon(record),
        "armor" => IsArmor(record),
        "items" => !IsWeapon(record) && !IsArmor(record),
        "quests" => IsQuest(record),
        _ => true
    };

    private static bool IsWeapon(JsonElement record)
    {
        if (ReadBool(record, "classification", "is_armor") || ReadBool(record, "combat", "is_armor")) return false;
        var type = FirstValue(record, ["item_type"], ["classification", "type"]);
        return type.Contains("weapon", StringComparison.OrdinalIgnoreCase) ||
               ReadInt(record, "classification", "weapon_type") > 0 ||
               ReadInt(record, "combat", "weapon_type") > 0 ||
               ReadInt(record, "combat", "maximum_damage") > 0;
    }

    private static bool IsArmor(JsonElement record)
    {
        var type = FirstValue(record, ["item_type"], ["classification", "type"]);
        return ReadBool(record, "classification", "is_armor") ||
               ReadBool(record, "combat", "is_armor") ||
               ReadInt(record, "combat", "armor_type") > 0 ||
               type.Contains("armor", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("shield", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQuest(JsonElement record)
    {
        var classification = FirstValue(record, ["classification"]);
        if (classification.Equals("npc_textblock_quest_flow", StringComparison.OrdinalIgnoreCase)) return true;
        return TryPath(record, ["script_flow", "stages"], out var stages) &&
               stages.ValueKind == JsonValueKind.Array && stages.GetArrayLength() > 0;
    }

    private static string ReadId(string category, JsonElement record)
    {
        var value = FirstValue(record, ["id"], ["mechanic_id"], ["object_id"]);
        if (value.Length > 0) return value;
        var number = FirstValue(record, ["number"], ["legacy_id"]);
        return number.Length == 0 ? string.Empty : $"{category.TrimEnd('s')}.{number}";
    }

    private static string ReadName(string category, JsonElement record, string id)
    {
        var name = FirstValue(record, ["name"], ["title"], ["display_name"], ["race_name"], ["class_name"]);
        if (category == "quests")
        {
            var flag = FirstValue(record, ["quest_flag_name"]);
            if (flag.Length > 0) name = FriendlyName(flag);
        }
        return name.Length == 0 ? FriendlyName(id) : name;
    }

    private static string BuildSubheading(string category, JsonElement record)
    {
        var level = FirstValue(record, ["level"], ["combat", "level"]);
        var type = FirstValue(record, ["classification", "type"], ["item_type"], ["room_type"]);
        var description = FirstValue(record, ["short_description"], ["description"], ["room_description"]);
        var parts = new[]
        {
            level.Length > 0 ? $"Level {level}" : string.Empty,
            type.Length > 0 ? FriendlyName(type) : string.Empty,
            description.Length > 0 ? Collapse(description, 140) : string.Empty
        }.Where(value => value.Length > 0).ToArray();
        return parts.Length > 0 ? string.Join(" | ", parts) : FriendlyName(category.TrimEnd('s'));
    }

    private static IReadOnlyList<ClassicLibrariumField> BuildFields(JsonElement record)
    {
        var fields = new List<ClassicLibrariumField>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Flatten(record, string.Empty, fields, seen, 0);
        return fields.Count > 0
            ? fields
            : [new ClassicLibrariumField("Details", "No additional gameplay fields are available for this record.")];
    }

    private static void Flatten(
        JsonElement element,
        string path,
        ICollection<ClassicLibrariumField> fields,
        ISet<string> seen,
        int depth)
    {
        if (depth > 5 || fields.Count >= 120) return;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (IsHiddenSegment(property.Name) || property.Name is "id" or "name" or "title" or "object_type" or "world_scope") continue;
                    var childPath = path.Length == 0 ? property.Name : path + "." + property.Name;
                    var important = GameplaySegments.Contains(property.Name) ||
                                    childPath.Split('.').Any(GameplaySegments.Contains);
                    if (!important && property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) continue;
                    Flatten(property.Value, childPath, fields, seen, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                var primitives = element.EnumerateArray().Where(item => item.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array).ToArray();
                if (primitives.Length == element.GetArrayLength())
                {
                    AddField(path, string.Join(", ", primitives.Select(ValueText)), fields, seen);
                    break;
                }
                var index = 1;
                foreach (var item in element.EnumerateArray().Take(20))
                    Flatten(item, $"{path}.{Singular(FriendlyName(path.Split('.').LastOrDefault() ?? "Entry"))} {index++}", fields, seen, depth + 1);
                break;
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                AddField(path, ValueText(element), fields, seen);
                break;
        }
    }

    private static void AddField(string path, string value, ICollection<ClassicLibrariumField> fields, ISet<string> seen)
    {
        value = value.Trim();
        if (path.Length == 0 || value.Length == 0 || value.Equals("null", StringComparison.OrdinalIgnoreCase)) return;
        var label = string.Join(" - ", path.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(FriendlyName));
        if (!seen.Add(label)) return;
        fields.Add(new ClassicLibrariumField(label, Collapse(value, 1200)));
    }

    private static bool ContainsVisibleText(JsonElement record, string query)
    {
        foreach (var property in record.EnumerateObject())
        {
            if (IsHiddenSegment(property.Name)) continue;
            if (property.Value.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string NormalizeCategory(string category)
    {
        category = category?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Categories.Any(item => item.Key.Equals(category, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Unknown Librarium category '{category}'.");
        return category;
    }

    private static bool IsHiddenSegment(string name) =>
        HiddenSegments.Contains(name) ||
        name.Contains("evidence", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("provenance", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("source_path", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("source_kind", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("source_reference", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_confidence", StringComparison.OrdinalIgnoreCase);

    private static string FirstValue(JsonElement root, params string[][] paths)
    {
        foreach (var path in paths)
            if (TryPath(root, path, out var value)) return ValueText(value);
        return string.Empty;
    }

    private static bool ReadBool(JsonElement root, params string[] path)
    {
        if (!TryPath(root, path, out var value)) return false;
        return value.ValueKind is JsonValueKind.True ||
               value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed;
    }

    private static int ReadInt(JsonElement root, params string[] path)
    {
        if (!TryPath(root, path, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(ValueText(value), out number) ? number : 0;
    }

    private static bool TryPath(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                value = default;
                return false;
            }
        }
        return true;
    }

    private static string ValueText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        JsonValueKind.Number => value.ToString(),
        _ => value.ToString()
    };

    private static string FriendlyName(string value)
    {
        value = value.Replace('_', ' ').Replace('-', ' ').Replace('.', ' ');
        value = Regex.Replace(value, "(?<=[a-z0-9])(?=[A-Z])", " ");
        value = Regex.Replace(value, "\\s+", " ").Trim();
        return value.Length == 0 ? string.Empty : char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string Singular(string value) => value.EndsWith("s", StringComparison.OrdinalIgnoreCase) && value.Length > 1
        ? value[..^1]
        : value;

    private static string Collapse(string value, int limit)
    {
        var collapsed = Regex.Replace(value, "\\s+", " ").Trim();
        return collapsed.Length <= limit ? collapsed : collapsed[..Math.Max(0, limit - 3)] + "...";
    }

    private sealed record QuestGroup(
        string Id,
        string Name,
        string FlagName,
        IReadOnlyList<JsonElement> Records);
}
