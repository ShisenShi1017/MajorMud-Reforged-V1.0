using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicDescriptionRecord(string Id, string EntityType, string EntityId, string Name, string Description, string Source, string EvidenceClassification);

public sealed class RuntimeClassicDescriptionAuthority
{
    private readonly IReadOnlyDictionary<string, RuntimeClassicDescriptionRecord> _raceByName;
    private readonly IReadOnlyDictionary<string, string> _npcById;
    private readonly IReadOnlyDictionary<string, string> _itemById;
    private readonly IReadOnlyDictionary<string, string> _roomById;

    private RuntimeClassicDescriptionAuthority(
        IReadOnlyDictionary<string, RuntimeClassicDescriptionRecord> raceByName,
        IReadOnlyDictionary<string, string> npcById,
        IReadOnlyDictionary<string, string> itemById,
        IReadOnlyDictionary<string, string> roomById)
    {
        _raceByName = raceByName;
        _npcById = npcById;
        _itemById = itemById;
        _roomById = roomById;
    }

    public static RuntimeClassicDescriptionAuthority Load(string projectRoot, string worldId)
    {
        var root = Path.Combine(projectRoot, "Data", worldId);
        var races = new Dictionary<string, RuntimeClassicDescriptionRecord>(StringComparer.OrdinalIgnoreCase);
        using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "race_descriptions.json"))))
        {
            foreach (var row in document.RootElement.GetProperty("records").EnumerateArray())
            {
                var name = Text(row, "race_name");
                var description = Text(row, "description");
                if (name.Length == 0 || description.Length == 0) throw new InvalidOperationException("Race description record is incomplete.");
                races[name] = new(Text(row,"id"), "race", Text(row,"race_number"), name, description, Text(row,"source_file"), Text(row,"evidence_classification"));
            }
        }
        return new(races, LoadDirectDescriptions(Path.Combine(root, "npcs.json")), LoadDirectDescriptions(Path.Combine(root, "items.json")), LoadDirectDescriptions(Path.Combine(root, "rooms.json")));
    }

    public string RequireRaceDescription(string race) => _raceByName.TryGetValue(NormalizeRace(race), out var record)
        ? record.Description
        : throw new KeyNotFoundException($"No authoritative decoded race description exists for '{race}'.");

    public bool TryNpcDescription(string npcId, out string description) => _npcById.TryGetValue(npcId, out description!);
    public bool TryItemDescription(string itemId, out string description) => _itemById.TryGetValue(itemId, out description!);
    public bool TryRoomDescription(string roomId, out string description) => _roomById.TryGetValue(roomId, out description!);
    public int RaceDescriptionCount => _raceByName.Count;
    public int NpcDirectDescriptionCount => _npcById.Count;
    public int ItemDirectDescriptionCount => _itemById.Count;
    public int RoomDirectDescriptionCount => _roomById.Count;

    private static Dictionary<string,string> LoadDirectDescriptions(string path)
    {
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return result;
        foreach (var row in records.EnumerateArray())
        {
            var id = Text(row,"id");
            var description = FirstDescription(row);
            if (id.Length > 0 && description.Length > 0) result[id] = description;
            var legacy = NestedNumber(row,"sourceNumbers","number");
            if (legacy > 0 && description.Length > 0) result[legacy.ToString()] = description;
        }
        return result;
    }

    private static string FirstDescription(JsonElement row)
    {
        var direct = FirstDescriptionValue(row);
        if (direct.Length > 0) return direct;

        foreach (var nested in PreferredGameDataObjects(row))
        {
            direct = FirstDescriptionValue(nested);
            if (direct.Length > 0) return direct;
        }

        return string.Empty;
    }

    private static IEnumerable<JsonElement> PreferredGameDataObjects(JsonElement row)
    {
        foreach (var key in new[] { "public_reference_0139", "mmexplorer_sql_reference_0139", "public_reference", "mmexplorer_sql_reference" })
        {
            if (!row.TryGetProperty(key, out var container) || container.ValueKind != JsonValueKind.Object) continue;
            yield return container;
            if (container.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object) yield return fields;
        }
    }

    private static string FirstDescriptionValue(JsonElement row)
    {
        foreach (var key in new[] { "description", "long_description", "look_description" })
        {
            var value = Text(row, key);
            if (value.Length > 0) return value;
        }

        if (row.TryGetProperty("descriptions", out var descriptions) && descriptions.ValueKind == JsonValueKind.Array)
        {
            var description = string.Join(" ", descriptions.EnumerateArray().Select(DescriptionLine).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (description.Length > 0) return description;
        }

        if (!row.TryGetProperty("printable_segments", out var segments) || segments.ValueKind != JsonValueKind.Array) return string.Empty;
        return string.Join(" ", segments.EnumerateArray().Select(PrintableSegment).Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string DescriptionLine(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString()?.Trim() ?? string.Empty;
        if (value.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var key in new[] { "description", "text", "line", "value" })
        {
            var line = Text(value, key);
            if (line.Length > 0) return line;
        }

        return string.Empty;
    }

    private static string PrintableSegment(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) return string.Empty;
        var text = value.GetString()?.Trim() ?? string.Empty;
        if (text.StartsWith("Room ", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        if (text.StartsWith("Textblock #", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return text;
    }

    private static int NestedNumber(JsonElement row, string parent, string child) => row.TryGetProperty(parent,out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty(child,out var c) && c.TryGetInt32(out var n) ? n : 0;
    private static string Text(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key,out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() ?? "" : v.ToString() : "";
    private static string NormalizeRace(string race) => race.Trim().Replace(" ", "-", StringComparison.Ordinal);
}

public sealed record RuntimeBundleFPhaseF2AStaticDescriptionAuditReport(bool Passed, int RaceDescriptions, int NpcDirectDescriptions, int ItemDirectDescriptions, int RoomDirectDescriptions, int VerifiedDisconnectCount, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public static class RuntimeBundleFPhaseF2AStaticDescriptionAudit
{
    public static RuntimeBundleFPhaseF2AStaticDescriptionAuditReport Run(string projectRoot, string worldId)
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok,string name){if(ok)checks.Add(name);else failures.Add(name);}
        var authority = RuntimeClassicDescriptionAuthority.Load(projectRoot,worldId);
        Check(authority.RaceDescriptionCount == 13, "all-13-classic-race-descriptions-promoted");
        foreach(var race in new[]{"Human","Dwarf","Gnome","Halfling","Elf","Half-Elf","Dark-Elf","Half-Orc","Goblin","Minotaur","Kang","Nekojin","Gaunt"})
            Check(authority.RequireRaceDescription(race).Length > 80, "race-description-live:"+race);
        var source = File.ReadAllText(Path.Combine(projectRoot,"Apps","Source","Reforged.Server","RuntimeLiveWorld.cs"));
        Check(source.Contains("RequireRaceDescription",StringComparison.Ordinal), "player-examine-consumes-race-authority");
        Check(source.Contains("TryNpcDescription",StringComparison.Ordinal), "npc-examine-consumes-direct-description");
        Check(source.Contains("TryItemDescription",StringComparison.Ordinal), "item-examine-consumes-direct-description");
        Check(source.Contains("session.CurrentRoom.Description",StringComparison.Ordinal), "room-look-consumes-authoritative-direct-description");
        Check(source.Contains("target.Description",StringComparison.Ordinal), "adjacent-room-look-consumes-authoritative-direct-description");
        Check(authority.RoomDirectDescriptionCount > 26000, "decoded-room-descriptions-loaded");
        Check(authority.NpcDirectDescriptionCount > 1000, "decoded-npc-descriptions-loaded");
        Check(authority.ItemDirectDescriptionCount > 1900, "decoded-item-descriptions-loaded");
        Check(!source.Contains("is a {character.Race} {character.CharacterClass}",StringComparison.Ordinal), "generic-race-summary-removed");
        return new(failures.Count==0,authority.RaceDescriptionCount,authority.NpcDirectDescriptionCount,authority.ItemDirectDescriptionCount,authority.RoomDirectDescriptionCount,failures.Count,checks,failures);
    }
}
