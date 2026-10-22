using System.Globalization;
using System.Text.Json.Nodes;

namespace RealmGate.Compiler;

public static class NavigationGraphProjectionWriter
{
    public const string FileName = "navigation_graph.json";

    public static int Write(
        string worldId,
        string outputRoot,
        IReadOnlyList<LibrariumFamily> families,
        Action<string, JsonNode> writeJson)
    {
        var rooms = Family(families, "rooms");
        var exits = Family(families, "exits");
        if (rooms.Count == 0 || exits.Count == 0)
            return 0;

        var npcLevelsById = new Dictionary<int, int>();
        var npcLevelsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var npc in Family(families, "npcs"))
        {
            var level = ReadInt(npc["combat"] as JsonObject, "level");
            var id = ReadInt(npc, "legacy_id");
            var name = ReadString(npc, "name");
            if (id > 0) npcLevelsById[id] = level;
            if (name.Length > 0) npcLevelsByName[name] = level;
        }

        var maximumNpcLevelByRoom = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var spawn in Family(families, "spawns"))
        {
            var map = ReadInt(spawn, "map_id");
            var number = ReadInt(spawn, "room_number");
            var key = map > 0 && number > 0
                ? RoomKey(map, number)
                : NormalizeRoomKey(ReadString(spawn, "qualified_room_ref"));
            if (key.Length == 0) continue;

            var npcId = ReadInt(spawn, "npc_legacy_id");
            var npcName = ReadString(spawn, "npc_name");
            var level = npcId > 0 && npcLevelsById.TryGetValue(npcId, out var byId)
                ? byId
                : npcLevelsByName.TryGetValue(npcName, out var byName) ? byName : 0;
            if (level <= 0) continue;
            maximumNpcLevelByRoom[key] = Math.Max(
                maximumNpcLevelByRoom.GetValueOrDefault(key),
                level);
        }

        var roomKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roomArray = new JsonArray();
        foreach (var room in rooms
                     .OrderBy(record => ReadInt(record, "map_number"))
                     .ThenBy(record => ReadInt(record, "room_number")))
        {
            var map = ReadInt(room, "map_number");
            var number = ReadInt(room, "room_number");
            if (map <= 0 || number <= 0) continue;
            var key = RoomKey(map, number);
            if (!roomKeys.Add(key)) continue;
            var environment = room["environment"] as JsonObject;
            roomArray.Add(new JsonObject
            {
                ["key"] = key,
                ["name"] = FirstNonBlank(ReadString(room, "name"), $"Room {key}"),
                ["map"] = map,
                ["room"] = number,
                ["roomSpellId"] = ReadInt(environment, "room_spell_id"),
                ["maximumNpcLevel"] = maximumNpcLevelByRoom.GetValueOrDefault(key)
            });
        }

        var edgeArray = new JsonArray();
        var seenEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var exit in exits
                     .OrderBy(record => ReadInt(record, "from_map_number"))
                     .ThenBy(record => ReadInt(record, "from_room_number"))
                     .ThenBy(record => ReadString(record, "direction"), StringComparer.OrdinalIgnoreCase))
        {
            var from = RoomKey(
                ReadInt(exit, "from_map_number"),
                ReadInt(exit, "from_room_number"));
            var to = RoomKey(
                ReadInt(exit, "to_map_number"),
                ReadInt(exit, "to_room_number"));
            if (!roomKeys.Contains(from) || !roomKeys.Contains(to)) continue;

            var gateKind = ReadString(exit, "gate_kind").ToLowerInvariant();
            var command = ResolveMovementCommand(
                ReadString(exit, "direction"),
                gateKind,
                ReadStringArray(exit["commands"]));
            if (command.Length == 0 || !seenEdges.Add($"{from}|{to}|{command}")) continue;

            edgeArray.Add(new JsonObject
            {
                ["from"] = from,
                ["to"] = to,
                ["command"] = command,
                ["gateKind"] = FirstNonBlank(gateKind, "restricted"),
                ["qualifier"] = ReadString(exit, "qualifier"),
                ["hasRestrictions"] = HasMeaningfulRestrictions(exit["prerequisites"] as JsonObject),
                ["source"] = worldId
            });
        }

        var graph = new JsonObject
        {
            ["schema"] = "realmgate_navigation_graph_v2",
            ["worldId"] = worldId,
            ["generatedBy"] = "RealmGate.Compiler",
            ["rooms"] = roomArray,
            ["edges"] = edgeArray
        };
        writeJson(Path.Combine(outputRoot, FileName), graph);
        return 1;
    }

    private static IReadOnlyList<JsonObject> Family(
        IReadOnlyList<LibrariumFamily> families,
        string name) =>
        families.FirstOrDefault(family =>
            family.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Records ?? [];

    private static string ResolveMovementCommand(
        string direction,
        string gateKind,
        IReadOnlyList<string> commands)
    {
        if (gateKind.Equals("text_command", StringComparison.OrdinalIgnoreCase) && commands.Count > 0)
            return commands[0];
        return direction.Trim().ToLowerInvariant() switch
        {
            "n" or "ne" or "e" or "se" or "s" or "sw" or "w" or "nw" or "u" or "d" =>
                direction.Trim().ToLowerInvariant(),
            _ => string.Empty
        };
    }

    private static bool HasMeaningfulRestrictions(JsonObject? prerequisites)
    {
        if (prerequisites is null || prerequisites.Count == 0) return false;
        foreach (var pair in prerequisites)
        {
            if (pair.Value is null) continue;
            if (pair.Value is JsonValue value)
            {
                if (value.TryGetValue<bool>(out var flag) && flag) return true;
                if (value.TryGetValue<int>(out var number) && number != 0) return true;
                if (value.TryGetValue<long>(out var longNumber) && longNumber != 0) return true;
                if (value.TryGetValue<string>(out var text) &&
                    !string.IsNullOrWhiteSpace(text) && text != "0") return true;
                continue;
            }

            var json = pair.Value.ToJsonString();
            if (json.Length > 2 && json is not "[]" and not "{}") return true;
        }
        return false;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node) =>
        node is JsonArray array
            ? array.Select(item => item?.GetValue<string>()?.Trim() ?? string.Empty)
                .Where(value => value.Length > 0)
                .ToArray()
            : [];

    private static int ReadInt(JsonObject? record, string name)
    {
        var node = Property(record, name);
        if (node is not JsonValue value) return 0;
        if (value.TryGetValue<int>(out var number)) return number;
        if (value.TryGetValue<long>(out var longNumber) &&
            longNumber is >= int.MinValue and <= int.MaxValue) return (int)longNumber;
        return value.TryGetValue<string>(out var text) &&
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : 0;
    }

    private static string ReadString(JsonObject? record, string name)
    {
        var node = Property(record, name);
        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static JsonNode? Property(JsonObject? record, string name)
    {
        if (record is null) return null;
        if (record.TryGetPropertyValue(name, out var node)) return node;
        return record.FirstOrDefault(pair =>
            pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static string NormalizeRoomKey(string value)
    {
        var text = value.Trim();
        if (text.StartsWith("room.map", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text[8..].Split('.', 2);
            return parts.Length == 2 &&
                   int.TryParse(parts[0], out var map) &&
                   int.TryParse(parts[1], out var room)
                ? RoomKey(map, room)
                : string.Empty;
        }
        return string.Empty;
    }

    private static string RoomKey(int map, int room) =>
        map > 0 && room > 0 ? $"{map}/{room}" : string.Empty;

    private static string FirstNonBlank(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
