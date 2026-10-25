using System.Text.Json;

namespace RealmGate.Client.Inventory;

public static class ClientInventoryStateDecoder
{
    public const int MaximumPayloadCharacters = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ClientInventoryStateSnapshot Decode(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        if (payload.Length > MaximumPayloadCharacters)
            throw new InvalidDataException("Inventory state payload exceeds the client safety limit.");

        ClientInventoryStateSnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<ClientInventoryStateSnapshot>(payload, Json)
                       ?? throw new InvalidDataException("Inventory state payload was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Inventory state payload is not valid JSON.", exception);
        }

        if (string.IsNullOrWhiteSpace(snapshot.WorldId) ||
            string.IsNullOrWhiteSpace(snapshot.CharacterId))
            throw new InvalidDataException("Inventory state payload has no world or character identity.");

        return snapshot with
        {
            WorldId = NormalizeIdentity(snapshot.WorldId),
            CharacterId = snapshot.CharacterId.Trim(),
            CharacterName = snapshot.CharacterName?.Trim() ?? string.Empty,
            RoomId = snapshot.RoomId?.Trim() ?? string.Empty,
            Inventory = NormalizeItems(snapshot.Inventory),
            Equipment = NormalizeItems(snapshot.Equipment)
        };
    }

    private static IReadOnlyList<ClientInventoryItemState> NormalizeItems(
        IReadOnlyList<ClientInventoryItemState>? items) =>
        (items ?? [])
        .Where(item => !string.IsNullOrWhiteSpace(item.InstanceId) &&
                       !string.IsNullOrWhiteSpace(item.Name))
        .Select(item => item with
        {
            InstanceId = item.InstanceId.Trim(),
            ItemId = item.ItemId?.Trim() ?? string.Empty,
            Name = item.Name.Trim(),
            ItemType = item.ItemType?.Trim() ?? string.Empty,
            Slot = item.Slot?.Trim() ?? string.Empty,
            Quantity = Math.Max(1, item.Quantity),
            Charges = Math.Max(0, item.Charges)
        })
        .ToArray();

    internal static string NormalizeIdentity(string? value)
    {
        var normalized = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        return normalized.Length == 0 ? "Classic" : normalized;
    }
}
