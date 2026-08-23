using System.Text.Json;

namespace Reforged.Server;

public sealed record RuntimeClassicGangHouseRecord(
    string Id,
    string HouseId,
    string Color,
    string Name,
    string DeedItemId,
    int ActivationTextblockId,
    int ActivationMessageId,
    IReadOnlyList<string> ActivationGrantItemIds,
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

public sealed record RuntimeClassicGangHouseItemRelationship(
    string ItemId,
    string ItemName,
    int HouseSelector,
    string HouseId,
    bool IsDeed,
    int TaxValue,
    bool IsHouseItem,
    bool IsShopController);

public static class RuntimeClassicGangHouseAbilityAuthority
{
    public static IReadOnlyList<RuntimeClassicGangHouseItemRelationship> Resolve(
        RuntimeItemCatalog items,
        RuntimeClassicGangHouseCatalog houses)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(houses);
        var deedSelectors = items.Items
            .Where(item => item.EquipmentAbilityModifiers.GetValueOrDefault(181) > 0)
            .ToDictionary(item => item.EquipmentAbilityModifiers[181], item => item.Id);
        if (deedSelectors.Count != houses.Records.Count)
            throw new InvalidDataException("Decoded gang-house deed selectors do not cover the generated house catalog.");

        var houseBySelector = deedSelectors.ToDictionary(
            pair => pair.Key,
            pair => houses.Records.Single(house => house.DeedItemId.Equals(pair.Value, StringComparison.OrdinalIgnoreCase)));
        var relationships = new List<RuntimeClassicGangHouseItemRelationship>();
        foreach (var item in items.Items)
        {
            var deedSelector = item.EquipmentAbilityModifiers.GetValueOrDefault(181);
            var houseSelector = item.EquipmentAbilityModifiers.GetValueOrDefault(183);
            var shopSelector = item.EquipmentAbilityModifiers.GetValueOrDefault(184);
            var selector = deedSelector > 0 ? deedSelector : houseSelector > 0 ? houseSelector : shopSelector;
            if (selector <= 0) continue;
            if (!houseBySelector.TryGetValue(selector, out var house))
                throw new InvalidDataException($"Gang-house item {item.Id} references unknown selector {selector}.");
            if (houseSelector > 0 && houseSelector != selector || shopSelector > 0 && shopSelector != selector)
                throw new InvalidDataException($"Gang-house item {item.Id} contains conflicting house selectors.");
            relationships.Add(new(item.Id, item.Name, selector, house.HouseId, deedSelector > 0,
                item.EquipmentAbilityModifiers.GetValueOrDefault(182), houseSelector > 0, shopSelector > 0));
        }
        return relationships.OrderBy(value => value.HouseSelector).ThenBy(value => value.ItemId, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

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
                JsonValueReader.ReadInt(recordElement, "activation_textblock_id", 0),
                JsonValueReader.ReadInt(recordElement, "activation_message_id", 0),
                recordElement.TryGetProperty("activation_grant_item_ids", out var grants) && grants.ValueKind == JsonValueKind.Array
                    ? grants.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                    : [],
                locations));
        }

        if (records.SelectMany(x => x.Locations).GroupBy(x => x.LocationId, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidDataException("Generated gang-house catalog contains duplicate location IDs.");
        if (records.Any(record => record.ActivationTextblockId <= 0 || record.ActivationMessageId <= 0 || record.ActivationGrantItemIds.Count != 4))
            throw new InvalidDataException("Generated gang-house catalog contains an incomplete decoded deed activation contract.");
        return new RuntimeClassicGangHouseCatalog(records);
    }
}
