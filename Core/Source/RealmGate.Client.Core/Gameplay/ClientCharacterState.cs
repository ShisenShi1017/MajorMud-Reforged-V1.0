using System.Text.Json;

namespace RealmGate.Client.Gameplay;

public sealed record ClientCharacterState(
    string WorldId,
    string CharacterId,
    string CharacterName,
    string RoomId,
    string RaceName,
    string ClassName,
    string Sex,
    int Level,
    long Experience,
    long? NextLevelExperience,
    IReadOnlyDictionary<string, int> BaseStats,
    IReadOnlyDictionary<string, int> Skills,
    string Alignment,
    DateTimeOffset CapturedAtUtc);

public static class ClientCharacterStateDecoder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ClientCharacterState Decode(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        if (payload.Length > 2 * 1024 * 1024)
            throw new InvalidDataException("Character state exceeds the client safety limit.");

        ClientCharacterState state;
        try
        {
            state = JsonSerializer.Deserialize<ClientCharacterState>(payload, Json)
                ?? throw new InvalidDataException("Character state was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Character state is not valid JSON.", exception);
        }

        if (string.IsNullOrWhiteSpace(state.WorldId) || string.IsNullOrWhiteSpace(state.CharacterId) ||
            string.IsNullOrWhiteSpace(state.CharacterName) ||
            !IsQualifiedRoom(state.RoomId) ||
            state.Level < 1 || state.Experience < 0 ||
            state.NextLevelExperience is < 0 || state.BaseStats is null || state.Skills is null ||
            state.CapturedAtUtc == default ||
            state.BaseStats.Any(pair => string.IsNullOrWhiteSpace(pair.Key)) ||
            state.Skills.Any(pair => string.IsNullOrWhiteSpace(pair.Key)))
            throw new InvalidDataException("Character state contains invalid identity or progression data.");

        return state with
        {
            WorldId = ClientGameplayAutomationStateDecoder.NormalizeIdentity(state.WorldId),
            CharacterId = state.CharacterId.Trim(),
            CharacterName = state.CharacterName.Trim(),
            RoomId = state.RoomId.Trim(),
            RaceName = state.RaceName?.Trim() ?? string.Empty,
            ClassName = state.ClassName?.Trim() ?? string.Empty,
            Sex = state.Sex?.Trim() ?? string.Empty,
            Alignment = state.Alignment?.Trim() ?? string.Empty
        };
    }

    internal static bool IsQualifiedRoom(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return false;
        var value = roomId.Trim();
        if (!value.StartsWith("room.map", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = value[8..].Split('.');
        return parts.Length == 2 && int.TryParse(parts[0], out var map) && map > 0 &&
               int.TryParse(parts[1], out var room) && room > 0;
    }
}
