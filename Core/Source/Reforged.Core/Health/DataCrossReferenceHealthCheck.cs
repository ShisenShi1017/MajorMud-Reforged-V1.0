using System.Text.Json;
using Reforged.Core.Validation;

namespace Reforged.Core.Health;

/// <summary>
/// Validates relationships between Data JSON files without requiring importer, editor, server, or SDK-time services.
/// This keeps the live Data/ database honest as the project moves from imported records into editable server data.
/// </summary>
public sealed class DataCrossReferenceHealthCheck
{
    public ValidationResult ValidateDataFolder(string dataFolder)
    {
        var result = new ValidationResult();
        if (!Directory.Exists(dataFolder))
        {
            result.Add(ValidationSeverity.Fatal, "DATA_FOLDER_MISSING", "Data folder was not found.", dataFolder);
            return result;
        }

        var context = DataReferenceContext.Load(dataFolder, result);
        if (result.HasErrors)
        {
            return result;
        }

        ValidateWorldRooms(context, result);
        ValidateRaceClassReferences(context, result);
        ValidateShopReferences(context, result);
        ValidateQuestReferences(context, result);
        ValidateLootTableReferences(context, result);
        return result;
    }

    private static void ValidateWorldRooms(DataReferenceContext context, ValidationResult result)
    {
        foreach (var room in context.WorldRooms)
        {
            foreach (var exit in room.Exits)
            {
                if (!context.RoomIds.Contains(exit.TargetRoomId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_ROOM_EXIT_MISSING", $"Room '{room.Id}' exit '{exit.Direction}' targets missing room '{exit.TargetRoomId}'.", context.WorldPath, room.Id);
                }
            }

            foreach (var itemId in room.ItemIds)
            {
                if (!context.ItemIds.Contains(itemId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_ROOM_ITEM_MISSING", $"Room '{room.Id}' references missing item '{itemId}'.", context.WorldPath, room.Id);
                }
            }

            foreach (var monsterId in room.MonsterIds)
            {
                if (!context.MonsterIds.Contains(monsterId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_ROOM_MONSTER_MISSING", $"Room '{room.Id}' references missing monster '{monsterId}'.", context.WorldPath, room.Id);
                }
            }
        }
    }

    private static void ValidateRaceClassReferences(DataReferenceContext context, ValidationResult result)
    {
        foreach (var race in context.Races)
        {
            if (race.AvailableClasses.Count == 1 && race.AvailableClasses[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var classId in race.AvailableClasses)
            {
                if (!context.ClassIds.Contains(classId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_RACE_CLASS_MISSING", $"Race '{race.Id}' references missing class '{classId}'.", context.RacesPath, race.Id);
                }
            }
        }
    }

    private static void ValidateShopReferences(DataReferenceContext context, ValidationResult result)
    {
        foreach (var shop in context.Shops)
        {
            if (!string.IsNullOrWhiteSpace(shop.RoomId) && !context.RoomIds.Contains(shop.RoomId))
            {
                result.Add(ValidationSeverity.Error, "DATA_REF_SHOP_ROOM_MISSING", $"Shop '{shop.Id}' references missing room '{shop.RoomId}'.", context.ShopsPath, shop.Id);
            }

            foreach (var itemId in shop.ItemIds)
            {
                if (!context.ItemIds.Contains(itemId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_SHOP_ITEM_MISSING", $"Shop '{shop.Id}' references missing item '{itemId}'.", context.ShopsPath, shop.Id);
                }
            }
        }
    }

    private static void ValidateQuestReferences(DataReferenceContext context, ValidationResult result)
    {
        foreach (var quest in context.Quests)
        {
            foreach (var itemId in quest.ItemIds)
            {
                if (!context.ItemIds.Contains(itemId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_QUEST_ITEM_MISSING", $"Quest '{quest.Id}' references missing item '{itemId}'.", context.QuestsPath, quest.Id);
                }
            }

            foreach (var monsterId in quest.MonsterIds)
            {
                if (!context.MonsterIds.Contains(monsterId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_QUEST_MONSTER_MISSING", $"Quest '{quest.Id}' references missing monster '{monsterId}'.", context.QuestsPath, quest.Id);
                }
            }

            foreach (var roomId in quest.RoomIds)
            {
                if (!context.RoomIds.Contains(roomId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_QUEST_ROOM_MISSING", $"Quest '{quest.Id}' references missing room '{roomId}'.", context.QuestsPath, quest.Id);
                }
            }
        }
    }

    private static void ValidateLootTableReferences(DataReferenceContext context, ValidationResult result)
    {
        foreach (var table in context.LootTables)
        {
            foreach (var itemId in table.ItemIds)
            {
                if (!context.ItemIds.Contains(itemId))
                {
                    result.Add(ValidationSeverity.Error, "DATA_REF_LOOT_ITEM_MISSING", $"Loot table '{table.Id}' references missing item '{itemId}'.", context.LootTablesPath, table.Id);
                }
            }
        }
    }

    private sealed class DataReferenceContext
    {
        public string WorldPath { get; private set; } = string.Empty;
        public string RacesPath { get; private set; } = string.Empty;
        public string ShopsPath { get; private set; } = string.Empty;
        public string QuestsPath { get; private set; } = string.Empty;
        public string LootTablesPath { get; private set; } = string.Empty;
        public HashSet<string> RoomIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ItemIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> MonsterIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ClassIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<RoomReferenceRecord> WorldRooms { get; } = new();
        public List<RaceReferenceRecord> Races { get; } = new();
        public List<ShopReferenceRecord> Shops { get; } = new();
        public List<QuestReferenceRecord> Quests { get; } = new();
        public List<LootTableReferenceRecord> LootTables { get; } = new();

        public static DataReferenceContext Load(string dataFolder, ValidationResult result)
        {
            var context = new DataReferenceContext
            {
                WorldPath = Path.Combine(dataFolder, "world.json"),
                RacesPath = Path.Combine(dataFolder, "races.json"),
                ShopsPath = Path.Combine(dataFolder, "shops.json"),
                QuestsPath = Path.Combine(dataFolder, "quests.json"),
                LootTablesPath = Path.Combine(dataFolder, "loot_tables.json")
            };

            LoadIds(Path.Combine(dataFolder, "rooms.json"), context.RoomIds, result);
            LoadIds(Path.Combine(dataFolder, "items.json"), context.ItemIds, result);
            LoadIds(Path.Combine(dataFolder, "monsters.json"), context.MonsterIds, result);
            LoadIds(Path.Combine(dataFolder, "classes.json"), context.ClassIds, result);
            LoadWorldRooms(context.WorldPath, context, result);
            LoadRaces(context.RacesPath, context, result);
            LoadShops(context.ShopsPath, context, result);
            LoadQuests(context.QuestsPath, context, result);
            LoadLootTables(context.LootTablesPath, context, result);
            return context;
        }

        private static void LoadIds(string path, HashSet<string> target, ValidationResult result)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                foreach (var element in ReadRecordElements(path))
                {
                    var id = TryGetText(element, "id") ?? TryGetText(element, "legacyId") ?? TryGetText(element, "StableId") ?? TryGetText(element, "LegacyId");
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        target.Add(id);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                result.Add(ValidationSeverity.Fatal, "DATA_REF_INDEX_READ_ERROR", ex.Message, path);
            }
        }

        private static void LoadWorldRooms(string path, DataReferenceContext context, ValidationResult result)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var document = JsonDocument.Parse(stream);
                var rooms = FindObjectOrArray(document.RootElement, "rooms");
                if (rooms is null)
                {
                    return;
                }

                if (rooms.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in rooms.Value.EnumerateObject())
                    {
                        ReadRoom(property.Value, property.Name, context);
                    }
                }
                else if (rooms.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var room in rooms.Value.EnumerateArray())
                    {
                        ReadRoom(room, TryGetText(room, "id") ?? string.Empty, context);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                result.Add(ValidationSeverity.Fatal, "DATA_REF_WORLD_READ_ERROR", ex.Message, path);
            }
        }

        private static void ReadRoom(JsonElement element, string fallbackId, DataReferenceContext context)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var id = TryGetText(element, "id") ?? fallbackId;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            context.RoomIds.Add(id);
            var room = new RoomReferenceRecord(id);

            if (element.TryGetProperty("exits", out var exits) && exits.ValueKind == JsonValueKind.Object)
            {
                foreach (var exit in exits.EnumerateObject())
                {
                    var target = exit.Value.ValueKind == JsonValueKind.String ? exit.Value.GetString() : TryGetText(exit.Value, "targetRoomId") ?? TryGetText(exit.Value, "roomId") ?? TryGetText(exit.Value, "target");
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        room.Exits.Add(new ExitReference(exit.Name, target));
                    }
                }
            }

            AddStringArray(element, "items", room.ItemIds);
            AddStringArray(element, "itemIds", room.ItemIds);
            AddStringArray(element, "monsters", room.MonsterIds);
            AddStringArray(element, "monsterIds", room.MonsterIds);
            context.WorldRooms.Add(room);
        }

        private static void LoadRaces(string path, DataReferenceContext context, ValidationResult result)
        {
            foreach (var element in ReadArray(path, result, "DATA_REF_RACES_READ_ERROR"))
            {
                var id = TryGetText(element, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var race = new RaceReferenceRecord(id);
                if (element.TryGetProperty("availableClasses", out var classes))
                {
                    if (classes.ValueKind == JsonValueKind.String)
                    {
                        var value = classes.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            race.AvailableClasses.Add(value);
                        }
                    }
                    else
                    {
                        AddValues(classes, race.AvailableClasses);
                    }
                }

                context.Races.Add(race);
            }
        }

        private static void LoadShops(string path, DataReferenceContext context, ValidationResult result)
        {
            foreach (var element in ReadArray(path, result, "DATA_REF_SHOPS_READ_ERROR"))
            {
                var id = TryGetText(element, "id") ?? TryGetText(element, "name");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var shop = new ShopReferenceRecord(id) { RoomId = TryGetText(element, "roomId") ?? TryGetText(element, "locationRoomId") };
                AddStringArray(element, "items", shop.ItemIds);
                AddStringArray(element, "itemIds", shop.ItemIds);
                AddStringArray(element, "inventory", shop.ItemIds);
                AddStringArray(element, "sells", shop.ItemIds);
                context.Shops.Add(shop);
            }
        }

        private static void LoadQuests(string path, DataReferenceContext context, ValidationResult result)
        {
            foreach (var element in ReadArray(path, result, "DATA_REF_QUESTS_READ_ERROR"))
            {
                var id = TryGetText(element, "id") ?? TryGetText(element, "name");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var quest = new QuestReferenceRecord(id);
                AddStringArray(element, "requiredItems", quest.ItemIds);
                AddStringArray(element, "rewardItems", quest.ItemIds);
                AddStringArray(element, "itemIds", quest.ItemIds);
                AddStringArray(element, "requiredMonsters", quest.MonsterIds);
                AddStringArray(element, "monsterIds", quest.MonsterIds);
                AddStringArray(element, "roomIds", quest.RoomIds);
                AddStringArray(element, "requiredRooms", quest.RoomIds);
                context.Quests.Add(quest);
            }
        }

        private static void LoadLootTables(string path, DataReferenceContext context, ValidationResult result)
        {
            foreach (var element in ReadArray(path, result, "DATA_REF_LOOT_TABLES_READ_ERROR"))
            {
                var id = TryGetText(element, "id") ?? TryGetText(element, "name");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var table = new LootTableReferenceRecord(id);
                AddStringArray(element, "items", table.ItemIds);
                AddStringArray(element, "itemIds", table.ItemIds);
                AddStringArray(element, "drops", table.ItemIds);
                context.LootTables.Add(table);
            }
        }

        private static IReadOnlyList<JsonElement> ReadArray(string path, ValidationResult result, string code)
        {
            var elements = new List<JsonElement>();
            if (!File.Exists(path))
            {
                return elements;
            }

            try
            {
                elements.AddRange(ReadRecordElements(path));
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                result.Add(ValidationSeverity.Fatal, code, ex.Message, path);
            }

            return elements;
        }

        private static IReadOnlyList<JsonElement> ReadRecordElements(string path)
        {
            var elements = new List<JsonElement>();
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("record_parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.ValueKind != JsonValueKind.String) continue;
                    var relative = part.GetString();
                    if (string.IsNullOrWhiteSpace(relative)) continue;
                    var partPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, relative.Replace('/', Path.DirectorySeparatorChar)));
                    elements.AddRange(ReadRecordElements(partPath));
                }

                return elements;
            }

            var records = document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("records", out var nested) &&
                nested.ValueKind == JsonValueKind.Array
                    ? nested
                    : document.RootElement;
            if (records.ValueKind != JsonValueKind.Array)
            {
                return elements;
            }

            foreach (var element in records.EnumerateArray())
            {
                elements.Add(element.Clone());
            }

            return elements;
        }

        private static JsonElement? FindObjectOrArray(JsonElement root, string propertyName)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return property;
            }

            return root.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? root : null;
        }

        private static void AddStringArray(JsonElement element, string propertyName, List<string> target)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            {
                return;
            }

            AddValues(property, target);
        }

        private static void AddValues(JsonElement element, List<string> target)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var value in element.EnumerateArray())
                {
                    var text = value.ValueKind == JsonValueKind.Object ? TryGetText(value, "id") ?? TryGetText(value, "itemId") ?? TryGetText(value, "monsterId") ?? TryGetText(value, "roomId") : ValueToText(value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        target.Add(text);
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var value in element.EnumerateObject())
                {
                    if (!string.IsNullOrWhiteSpace(value.Name))
                    {
                        target.Add(value.Name);
                    }
                }
            }
            else
            {
                var text = ValueToText(element);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    target.Add(text);
                }
            }
        }

        private static string? TryGetText(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var property))
            {
                return null;
            }

            return ValueToText(property);
        }

        private static string? ValueToText(JsonElement property)
        {
            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.ToString(),
                _ => null
            };
        }
    }

    private sealed record ExitReference(string Direction, string TargetRoomId);
    private sealed record RoomReferenceRecord(string Id)
    {
        public List<ExitReference> Exits { get; } = new();
        public List<string> ItemIds { get; } = new();
        public List<string> MonsterIds { get; } = new();
    }

    private sealed record RaceReferenceRecord(string Id)
    {
        public List<string> AvailableClasses { get; } = new();
    }

    private sealed record ShopReferenceRecord(string Id)
    {
        public string? RoomId { get; init; }
        public List<string> ItemIds { get; } = new();
    }

    private sealed record QuestReferenceRecord(string Id)
    {
        public List<string> ItemIds { get; } = new();
        public List<string> MonsterIds { get; } = new();
        public List<string> RoomIds { get; } = new();
    }

    private sealed record LootTableReferenceRecord(string Id)
    {
        public List<string> ItemIds { get; } = new();
    }
}
