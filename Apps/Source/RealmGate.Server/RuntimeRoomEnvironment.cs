using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeRoomEnvironmentRecord(
    string Id, int MapId, int RoomNumber, int LightLevel, int RoomSpellId, int LairId, int Delay,
    int RoomType, int Attributes, int DeathRoomId, int GangHouseId, int RespawnMinutes,
    int MaxAreaCap, int LinkedSpawnRoom, string SourceClassification)
{
    public static RuntimeRoomEnvironmentRecord FromJson(JsonElement e) => new(
        JsonValueReader.ReadString(e, "id", string.Empty),
        JsonValueReader.ReadInt(e, "map_id", 0), JsonValueReader.ReadInt(e, "room_number", 0),
        JsonValueReader.ReadInt(e, "light_level", 0), JsonValueReader.ReadInt(e, "room_spell_id", 0),
        JsonValueReader.ReadInt(e, "room_lair_id", 0), JsonValueReader.ReadInt(e, "room_delay", 0),
        JsonValueReader.ReadInt(e, "room_type", 0), JsonValueReader.ReadInt(e, "attributes", 0),
        JsonValueReader.ReadInt(e, "death_room_id", 0), JsonValueReader.ReadInt(e, "gang_house", 0),
        JsonValueReader.ReadInt(e, "respawn_minutes", 0), JsonValueReader.ReadInt(e, "max_area_cap", 0),
        JsonValueReader.ReadInt(e, "linked_spawn_room", 0),
        JsonValueReader.ReadString(e, "source_classification", string.Empty));
}

public static class RuntimeRoomEnvironmentCatalog
{
    public static IReadOnlyDictionary<RuntimeRoomKey, RuntimeRoomEnvironmentRecord> Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Generated room environment catalog was not found.", path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Generated room environment catalog has no records array.");
        var result = new Dictionary<RuntimeRoomKey, RuntimeRoomEnvironmentRecord>();
        foreach (var e in records.EnumerateArray())
        {
            var r = RuntimeRoomEnvironmentRecord.FromJson(e);
            if (r.MapId <= 0 || r.RoomNumber <= 0 || string.IsNullOrWhiteSpace(r.Id)) continue;
            if (!result.TryAdd(new RuntimeRoomKey(r.MapId, r.RoomNumber), r))
                throw new InvalidDataException($"Duplicate environment room identity: {r.Id}");
        }
        return result;
    }
}
