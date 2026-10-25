using System.Text.Json;

namespace RealmGate.Client.Gameplay;

public sealed record ClientWorkshopAbility(string Name, int Value);

public sealed record ClientWorkshopItem(
    string Id, string Name, string ItemType, string Slot, int Weight, long Cost,
    int MinimumDamage, int MaximumDamage, int ArmorClassBonus, int DamageResistanceBonus,
    int AccuracyBonus, int SpeedBonus, int MinimumLevel, int MaximumLevel,
    int MinimumStrength, bool TwoHanded, bool ClassRestricted, bool RaceRestricted,
    IReadOnlyList<ClientWorkshopAbility> Abilities)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Slot) ? Name : $"{Name} ({Slot})";
}

public sealed record ClientWorkshopEquippedItem(string InstanceId, ClientWorkshopItem Item);

public sealed record ClientWorkshopItemSearch(
    string WorldId, string CharacterId, string RoomId, string Query, int TotalMatches,
    IReadOnlyList<ClientWorkshopItem> Matches,
    IReadOnlyList<ClientWorkshopEquippedItem> Equipped,
    DateTimeOffset CapturedAtUtc);

public static class ClientWorkshopItemSearchDecoder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ClientWorkshopItemSearch Decode(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        if (payload.Length > 2 * 1024 * 1024)
            throw new InvalidDataException("Item search exceeds the client safety limit.");
        ClientWorkshopItemSearch result;
        try
        {
            result = JsonSerializer.Deserialize<ClientWorkshopItemSearch>(payload, Json)
                ?? throw new InvalidDataException("Item search was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Item search is not valid JSON.", exception);
        }

        if (string.IsNullOrWhiteSpace(result.WorldId) || string.IsNullOrWhiteSpace(result.CharacterId) ||
            !ClientCharacterStateDecoder.IsQualifiedRoom(result.RoomId) || result.Query is null ||
            result.TotalMatches < 0 || result.Matches is null || result.Equipped is null ||
            result.Matches.Count > 75 || result.TotalMatches < result.Matches.Count ||
            result.CapturedAtUtc == default ||
            result.Matches.Any(item => !IsValid(item)) ||
            result.Equipped.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.InstanceId) || !IsValid(entry.Item)))
            throw new InvalidDataException("Item search has incomplete identity or item data.");

        return result with
        {
            WorldId = ClientGameplayAutomationStateDecoder.NormalizeIdentity(result.WorldId),
            CharacterId = result.CharacterId.Trim(),
            RoomId = result.RoomId.Trim(),
            Query = result.Query.Trim()
        };
    }

    private static bool IsValid(ClientWorkshopItem? item) =>
        item is not null && !string.IsNullOrWhiteSpace(item.Id) &&
        !string.IsNullOrWhiteSpace(item.Name) && item.Slot is not null &&
        item.Abilities is not null && item.Abilities.All(ability =>
            ability is not null && !string.IsNullOrWhiteSpace(ability.Name));
}

public sealed record ClientWorkshopLoadoutTotals(
    long Weight, int ArmorClassBonus, int DamageResistanceBonus,
    int AccuracyBonus, int MinimumDamage, int MaximumDamage)
{
    public static ClientWorkshopLoadoutTotals From(IEnumerable<ClientWorkshopItem> items)
    {
        var rows = items.ToArray();
        return new(rows.Sum(item => (long)item.Weight), rows.Sum(item => item.ArmorClassBonus),
            rows.Sum(item => item.DamageResistanceBonus), rows.Sum(item => item.AccuracyBonus),
            rows.Sum(item => item.MinimumDamage), rows.Sum(item => item.MaximumDamage));
    }
}
