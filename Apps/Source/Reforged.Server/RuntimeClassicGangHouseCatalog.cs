using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicGangHouseRecord(
    string Id,
    string HouseId,
    string Color,
    string Name,
    string DeedItemId,
    IReadOnlyList<RuntimeClassicGangHouseLocationRecord> Locations);

public sealed record RuntimeClassicGangHouseLocationRecord(
    string LocationId,
    string RoomId,
    int MapId,
    int RoomNumber,
    string RoomName,
    string LocationKind,
    string ShopId,
    int LegacyShopNumber,
    bool IsGangHouse,
    bool IsVault,
    bool IsGangHouseShop,
    string Evidence);

public sealed class RuntimeClassicGangHouseCatalog
{
    public RuntimeClassicGangHouseCatalog(IReadOnlyList<RuntimeClassicGangHouseRecord> records)
    {
        Records = records;
        Locations = records.SelectMany(x => x.Locations).ToArray();
    }

    public IReadOnlyList<RuntimeClassicGangHouseRecord> Records { get; }
    public IReadOnlyList<RuntimeClassicGangHouseLocationRecord> Locations { get; }

    public IReadOnlyList<RuntimeClassicGangHouseLocation> ToProtectionLocations() =>
        Locations.Select(x => new RuntimeClassicGangHouseLocation(
            x.LocationId,
            HouseIdForLocation(x.LocationId),
            x.LocationKind,
            x.IsGangHouse,
            x.IsVault,
            x.IsGangHouseShop)).ToArray();

    private string HouseIdForLocation(string locationId)
    {
        var owner = Records.FirstOrDefault(record => record.Locations.Any(location =>
            location.LocationId.Equals(locationId, StringComparison.OrdinalIgnoreCase)));
        return owner?.HouseId ?? string.Empty;
    }

    public static RuntimeClassicGangHouseCatalog Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Generated gang-house catalog was not found.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("records", out var recordsElement) || recordsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Generated gang-house catalog has no records array.");

        var records = new List<RuntimeClassicGangHouseRecord>();
        foreach (var recordElement in recordsElement.EnumerateArray())
        {
            var locations = new List<RuntimeClassicGangHouseLocationRecord>();
            if (recordElement.TryGetProperty("locations", out var locationsElement) && locationsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var locationElement in locationsElement.EnumerateArray())
                {
                    locations.Add(new RuntimeClassicGangHouseLocationRecord(
                        JsonValueReader.ReadString(locationElement, "location_id", string.Empty),
                        JsonValueReader.ReadString(locationElement, "room_id", string.Empty),
                        JsonValueReader.ReadInt(locationElement, "map_id", 0),
                        JsonValueReader.ReadInt(locationElement, "room_number", 0),
                        JsonValueReader.ReadString(locationElement, "room_name", string.Empty),
                        JsonValueReader.ReadString(locationElement, "location_kind", string.Empty),
                        JsonValueReader.ReadString(locationElement, "shop_id", string.Empty),
                        JsonValueReader.ReadInt(locationElement, "legacy_shop_number", 0),
                        JsonValueReader.ReadBool(locationElement, "is_gang_house", false),
                        JsonValueReader.ReadBool(locationElement, "is_vault", false),
                        JsonValueReader.ReadBool(locationElement, "is_gang_house_shop", false),
                        JsonValueReader.ReadString(locationElement, "evidence", string.Empty)));
                }
            }

            records.Add(new RuntimeClassicGangHouseRecord(
                JsonValueReader.ReadString(recordElement, "id", string.Empty),
                JsonValueReader.ReadString(recordElement, "house_id", string.Empty),
                JsonValueReader.ReadString(recordElement, "color", string.Empty),
                JsonValueReader.ReadString(recordElement, "name", string.Empty),
                JsonValueReader.ReadString(recordElement, "deed_item_id", string.Empty),
                locations));
        }

        if (records.SelectMany(x => x.Locations).GroupBy(x => x.LocationId, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidDataException("Generated gang-house catalog contains duplicate location IDs.");
        return new RuntimeClassicGangHouseCatalog(records);
    }
}
