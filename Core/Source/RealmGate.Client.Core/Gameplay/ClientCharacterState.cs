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
    DateTimeOffset CapturedAtUtc,
    long? CurrentLevelExperience = null,
    int? NextLevelCharacterPointAward = null,
    int CharacterPointsAvailable = 0,
    int Lives = 0,
    int MaximumLives = 0,
    IReadOnlyDictionary<string, int>? ClassLevels = null,
    IReadOnlyDictionary<string, long>? ClassExperience = null,
    IReadOnlyDictionary<string, int>? DerivedStats = null,
    IReadOnlyDictionary<string, int>? Resistances = null,
    IReadOnlyDictionary<string, int>? SavingThrows = null);

public sealed record ClientCharacterProgressionSummary(
    bool HasLevelThresholds,
    long CurrentExperience,
    long LevelStartExperience,
    long NextLevelExperience,
    long EarnedThisLevel,
    long RequiredThisLevel,
    long RemainingExperience,
    double PercentComplete,
    int? NextLevelCharacterPointAward,
    int CharacterPointsAvailable,
    int Lives,
    int MaximumLives)
{
    public static ClientCharacterProgressionSummary From(ClientCharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var hasThresholds = state.CurrentLevelExperience is long start &&
                            state.NextLevelExperience is long next && next > start;
        if (!hasThresholds)
            return new(false, state.Experience, 0, 0, 0, 0, 0, 0,
                state.NextLevelCharacterPointAward, state.CharacterPointsAvailable, state.Lives, state.MaximumLives);

        var levelStart = state.CurrentLevelExperience!.Value;
        var levelEnd = state.NextLevelExperience!.Value;
        var required = levelEnd - levelStart;
        var earned = Math.Clamp(state.Experience - levelStart, 0, required);
        var remaining = Math.Max(0, levelEnd - state.Experience);
        var percent = required == 0 ? 100d : earned * 100d / required;
        return new(true, state.Experience, levelStart, levelEnd, earned, required, remaining,
            Math.Clamp(percent, 0d, 100d), state.NextLevelCharacterPointAward,
            state.CharacterPointsAvailable, state.Lives, state.MaximumLives);
    }
}

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
            state.NextLevelExperience is < 0 || state.CurrentLevelExperience is < 0 ||
            state.NextLevelCharacterPointAward is < 0 || state.CharacterPointsAvailable < 0 ||
            state.Lives < 0 || state.MaximumLives < 0 ||
            state.MaximumLives > 0 && state.Lives > state.MaximumLives ||
            state.CurrentLevelExperience is long currentThreshold && currentThreshold > state.Experience ||
            state.CurrentLevelExperience is long current && state.NextLevelExperience is long next && next <= current ||
            state.BaseStats is null || state.Skills is null ||
            state.CapturedAtUtc == default ||
            state.BaseStats.Any(pair => string.IsNullOrWhiteSpace(pair.Key)) ||
            state.Skills.Any(pair => string.IsNullOrWhiteSpace(pair.Key)) ||
            Invalid(state.ClassLevels, value => value < 1) ||
            Invalid(state.ClassExperience, value => value < 0) ||
            Invalid(state.DerivedStats) || Invalid(state.Resistances) || Invalid(state.SavingThrows))
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
            Alignment = state.Alignment?.Trim() ?? string.Empty,
            ClassLevels = Normalize(state.ClassLevels),
            ClassExperience = Normalize(state.ClassExperience),
            DerivedStats = Normalize(state.DerivedStats),
            Resistances = Normalize(state.Resistances),
            SavingThrows = Normalize(state.SavingThrows)
        };
    }

    private static bool Invalid<T>(IReadOnlyDictionary<string, T>? values, Func<T, bool>? invalidValue = null) =>
        values?.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || invalidValue?.Invoke(pair.Value) == true) == true;

    private static IReadOnlyDictionary<string, T> Normalize<T>(IReadOnlyDictionary<string, T>? values) =>
        (values ?? new Dictionary<string, T>())
        .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);

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
