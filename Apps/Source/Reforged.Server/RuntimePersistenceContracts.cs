namespace Reforged.Server;

public sealed record RuntimeAccountIdentity(
    string AccountId,
    string DisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RuntimeCharacterIdentity(
    string CharacterId,
    string AccountId,
    string WorldId,
    int SaveSlot,
    string DisplayName,
    string CurrentRoomId,
    int CurrentRoomNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RuntimeSaveSlot(
    string AccountId,
    string WorldId,
    int SlotNumber,
    string? CharacterId,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public interface IRuntimeAccountRepository
{
    RuntimeAccountIdentity GetOrCreateAccount(string displayName);
    RuntimeAccountIdentity? FindAccount(string accountId);
}

public interface IRuntimeCharacterRepository
{
    RuntimeCharacterIdentity GetOrCreateCharacter(RuntimeAccountIdentity account, RuntimeWorld world, int saveSlot, string displayName);
    RuntimeCharacterIdentity? FindCharacter(string worldId, string characterId);
    IReadOnlyList<RuntimeSaveSlot> ListSaveSlots(string accountId, string worldId);
    RuntimeCharacterIdentity UpdateLocation(RuntimeCharacterIdentity character, RuntimeRoom room);
}

public sealed class InMemoryRuntimeAccountRepository : IRuntimeAccountRepository
{
    private readonly Dictionary<string, RuntimeAccountIdentity> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeAccountIdentity GetOrCreateAccount(string displayName)
    {
        var safeName = RuntimeIdentity.NormalizeToken(displayName, "player");
        var accountId = $"account:{safeName}";
        var now = DateTimeOffset.UtcNow;

        if (_accounts.TryGetValue(accountId, out var existing))
        {
            var updated = existing with { DisplayName = displayName.Trim(), UpdatedAtUtc = now };
            _accounts[accountId] = updated;
            return updated;
        }

        var account = new RuntimeAccountIdentity(accountId, displayName.Trim(), now, now);
        _accounts[accountId] = account;
        return account;
    }

    public RuntimeAccountIdentity? FindAccount(string accountId)
    {
        return _accounts.TryGetValue(accountId, out var account) ? account : null;
    }
}

public sealed class InMemoryRuntimeCharacterRepository : IRuntimeCharacterRepository
{
    public const int DefaultSlotCount = 3;
    private readonly Dictionary<string, RuntimeCharacterIdentity> _characters = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCharacterIdentity GetOrCreateCharacter(RuntimeAccountIdentity account, RuntimeWorld world, int saveSlot, string displayName)
    {
        ValidateSlot(saveSlot);
        var start = world.GetStartRoom();
        var characterId = RuntimeIdentity.CreateCharacterId(account.AccountId, world.Definition.Id, saveSlot);
        var now = DateTimeOffset.UtcNow;

        if (_characters.TryGetValue(characterId, out var existing))
        {
            return existing;
        }

        var character = new RuntimeCharacterIdentity(
            characterId,
            account.AccountId,
            world.Definition.Id,
            saveSlot,
            displayName.Trim(),
            start.Id,
            start.RoomNumber,
            now,
            now);

        _characters[characterId] = character;
        return character;
    }

    public RuntimeCharacterIdentity? FindCharacter(string worldId, string characterId)
    {
        return _characters.TryGetValue(characterId, out var character) && string.Equals(character.WorldId, worldId, StringComparison.OrdinalIgnoreCase)
            ? character
            : null;
    }

    public IReadOnlyList<RuntimeSaveSlot> ListSaveSlots(string accountId, string worldId)
    {
        var now = DateTimeOffset.UtcNow;
        return Enumerable.Range(1, DefaultSlotCount)
            .Select(slot =>
            {
                var character = _characters.Values.FirstOrDefault(item =>
                    string.Equals(item.AccountId, accountId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.WorldId, worldId, StringComparison.OrdinalIgnoreCase) &&
                    item.SaveSlot == slot);

                return new RuntimeSaveSlot(
                    accountId,
                    worldId,
                    slot,
                    character?.CharacterId,
                    character is null ? "empty" : "occupied",
                    character?.UpdatedAtUtc ?? now);
            })
            .ToArray();
    }

    public RuntimeCharacterIdentity UpdateLocation(RuntimeCharacterIdentity character, RuntimeRoom room)
    {
        var updated = character with
        {
            CurrentRoomId = room.Id,
            CurrentRoomNumber = room.RoomNumber,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _characters[updated.CharacterId] = updated;
        return updated;
    }

    private static void ValidateSlot(int saveSlot)
    {
        if (saveSlot is < 1 or > DefaultSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(saveSlot), saveSlot, $"Save slot must be between 1 and {DefaultSlotCount}.");
        }
    }
}

public static class RuntimeIdentity
{
    public static string CreateCharacterId(string accountId, string worldId, int saveSlot)
    {
        return $"{worldId}:slot{saveSlot}:{NormalizeToken(accountId, "account")}";
    }

    public static string NormalizeToken(string value, string fallback)
    {
        var safe = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');

        return safe.Length == 0 ? fallback : safe;
    }
}
