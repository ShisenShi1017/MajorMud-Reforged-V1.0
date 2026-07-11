using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reforged.Server;

public sealed record RuntimeAccountRecord(
    string AccountId,
    string DisplayName,
    IReadOnlyList<string> CharacterIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record RuntimeCharacterRecord(
    string CharacterId,
    string AccountId,
    string DisplayName,
    string WorldId,
    string RaceId,
    string RaceName,
    string ClassId,
    string ClassName,
    int Level,
    long Experience,
    int HitPoints,
    int MaximumHitPoints,
    int Mana,
    int MaximumMana,
    string RoomId,
    int RoomNumber,
    IReadOnlyDictionary<string, int> BaseStats,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyList<string> Spells,
    IReadOnlyList<string> Inventory,
    IReadOnlyDictionary<string, string?> Equipment,
    IReadOnlyDictionary<string, string> Conditions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int SaveVersion = 1);

public sealed record RuntimeCharacterDefinition(
    string Id,
    string Name,
    string Confidence,
    string Classification);

public sealed class RuntimeCharacterCatalog
{
    private readonly Dictionary<string, RuntimeCharacterDefinition> _races = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeCharacterDefinition> _classes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<RuntimeCharacterDefinition> Races => _races.Values.OrderBy(value => value.Name).ToArray();
    public IReadOnlyCollection<RuntimeCharacterDefinition> Classes => _classes.Values.OrderBy(value => value.Name).ToArray();

    public static RuntimeCharacterCatalog Load(string projectRoot, string worldId)
    {
        var catalog = new RuntimeCharacterCatalog();
        var path = Path.Combine(projectRoot, "Data", worldId, "character_foundation.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Generated Librarium character foundation is missing.", path);
        }

        catalog.LoadFoundation(path);
        return catalog;
    }

    public RuntimeCharacterDefinition RequireRace(string idOrName) => Require(_races, idOrName, "race");
    public RuntimeCharacterDefinition RequireClass(string idOrName) => Require(_classes, idOrName, "class");

    private static RuntimeCharacterDefinition Require(Dictionary<string, RuntimeCharacterDefinition> values, string idOrName, string kind)
    {
        var requested = idOrName.Trim();
        if (values.TryGetValue(requested, out var exact))
        {
            return exact;
        }

        var byName = values.Values.FirstOrDefault(value => string.Equals(value.Name, requested, StringComparison.OrdinalIgnoreCase));
        return byName ?? throw new InvalidOperationException($"Unknown generated Classic {kind} '{idOrName}'.");
    }

    private void LoadFoundation(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("authority", out var authority) || !string.Equals(authority.GetString(), "Librarium", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Character foundation is not a Librarium-authoritative projection.");
        }

        LoadArray(root, "races", _races);
        LoadArray(root, "classes", _classes);
    }

    private static void LoadArray(JsonElement root, string property, Dictionary<string, RuntimeCharacterDefinition> destination)
    {
        if (!root.TryGetProperty(property, out var records) || records.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Generated character foundation does not contain '{property}'.");
        }

        foreach (var record in records.EnumerateArray())
        {
            var id = ReadString(record, "id");
            var name = ReadString(record, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            destination[id] = new RuntimeCharacterDefinition(
                id,
                name,
                ReadString(record, "confidence"),
                ReadString(record, "classification"));
        }
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}

public sealed class RuntimeCharacterRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _root;
    private readonly object _gate = new();

    public RuntimeCharacterRepository(string projectRoot)
    {
        _root = Path.Combine(projectRoot, "Saves", "Runtime0137");
        Directory.CreateDirectory(Path.Combine(_root, "accounts"));
        Directory.CreateDirectory(Path.Combine(_root, "characters"));
    }

    public RuntimeAccountRecord GetOrCreateAccount(string accountName)
    {
        var accountId = RuntimeIdentity.Normalize(accountName, "account");
        lock (_gate)
        {
            var existing = TryLoadAccount(accountId);
            if (existing is not null)
            {
                return existing;
            }

            var now = DateTimeOffset.UtcNow;
            var created = new RuntimeAccountRecord(accountId, accountName.Trim(), Array.Empty<string>(), now, now);
            WriteAtomic(AccountPath(accountId), created);
            return created;
        }
    }

    public RuntimeAccountRecord? TryLoadAccount(string accountId) => Read<RuntimeAccountRecord>(AccountPath(RuntimeIdentity.Normalize(accountId, "account")));
    public RuntimeCharacterRecord? TryLoadCharacter(string characterId) => Read<RuntimeCharacterRecord>(CharacterPath(RuntimeIdentity.Normalize(characterId, "character")));

    public IReadOnlyList<RuntimeCharacterRecord> ListCharacters(string accountId)
    {
        var account = TryLoadAccount(accountId);
        if (account is null)
        {
            return Array.Empty<RuntimeCharacterRecord>();
        }

        return account.CharacterIds
            .Select(TryLoadCharacter)
            .Where(value => value is not null)
            .Cast<RuntimeCharacterRecord>()
            .OrderBy(value => value.DisplayName)
            .ToArray();
    }

    public RuntimeCharacterRecord CreateCharacter(RuntimeAccountRecord account, RuntimeCharacterRecord character)
    {
        lock (_gate)
        {
            if (TryLoadCharacter(character.CharacterId) is not null)
            {
                throw new InvalidOperationException($"Character '{character.DisplayName}' already exists.");
            }

            WriteAtomic(CharacterPath(character.CharacterId), character);
            var ids = account.CharacterIds.Concat(new[] { character.CharacterId }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            WriteAtomic(AccountPath(account.AccountId), account with { CharacterIds = ids, UpdatedAtUtc = DateTimeOffset.UtcNow });
            return character;
        }
    }

    public void SaveCharacter(RuntimeCharacterRecord character)
    {
        lock (_gate)
        {
            WriteAtomic(CharacterPath(character.CharacterId), character with { UpdatedAtUtc = DateTimeOffset.UtcNow });
        }
    }

    private string AccountPath(string id) => Path.Combine(_root, "accounts", id + ".json");
    private string CharacterPath(string id) => Path.Combine(_root, "characters", id + ".json");

    private static T? Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);
        File.Move(temporary, path, true);
    }
}

public sealed class RuntimeCharacterService
{
    private readonly RuntimeCharacterRepository _repository;
    private readonly string _projectRoot;

    public RuntimeCharacterService(string projectRoot)
    {
        _projectRoot = projectRoot;
        _repository = new RuntimeCharacterRepository(projectRoot);
    }

    public RuntimeAccountRecord GetOrCreateAccount(string accountName) => _repository.GetOrCreateAccount(accountName);
    public RuntimeCharacterCatalog GetCatalog(string worldId) => RuntimeCharacterCatalog.Load(_projectRoot, worldId);
    public IReadOnlyList<RuntimeCharacterRecord> ListCharacters(string accountId) => _repository.ListCharacters(accountId);
    public RuntimeCharacterRecord? LoadCharacter(string id) => _repository.TryLoadCharacter(id);

    public RuntimeCharacterRecord CreateCharacter(RuntimeAccountRecord account, RuntimeWorld world, string name, string race, string characterClass)
    {
        RuntimeIdentity.ValidateCharacterName(name);
        var catalog = RuntimeCharacterCatalog.Load(_projectRoot, world.Definition.Id);
        var raceDefinition = catalog.RequireRace(race);
        var classDefinition = catalog.RequireClass(characterClass);
        var start = world.ResolveGeneratedCharacterStartRoom()
            ?? throw new InvalidOperationException($"World '{world.Definition.Id}' has no generated Classic character-start room. Character creation is blocked rather than guessing a start.");
        var characterId = RuntimeIdentity.CharacterId(world.Definition.Id, name);
        var now = DateTimeOffset.UtcNow;
        var character = new RuntimeCharacterRecord(
            characterId,
            account.AccountId,
            name.Trim(),
            world.Definition.Id,
            raceDefinition.Id,
            raceDefinition.Name,
            classDefinition.Id,
            classDefinition.Name,
            1,
            0,
            1,
            1,
            0,
            0,
            start.Id,
            start.RoomNumber,
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, string>(),
            now,
            now);
        return _repository.CreateCharacter(account, character);
    }

    public void SaveSession(RuntimeSession session)
    {
        if (session.Actor is not RuntimeCharacterActor actor)
        {
            return;
        }

        var existing = _repository.TryLoadCharacter(actor.Id);
        if (existing is null)
        {
            return;
        }

        _repository.SaveCharacter(existing with
        {
            WorldId = session.World.Definition.Id,
            RoomId = session.CurrentRoom.Id,
            RoomNumber = session.CurrentRoom.RoomNumber,
            Level = actor.Level,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}

public static class RuntimeIdentity
{
    private static readonly Regex CharacterNamePattern = new("^[A-Za-z][A-Za-z'-]{2,19}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void ValidateCharacterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !CharacterNamePattern.IsMatch(name.Trim()))
        {
            throw new InvalidOperationException("Character names must be 3-20 characters, start with a letter, and contain only letters, apostrophes, or hyphens.");
        }
    }

    public static string CharacterId(string worldId, string name) => $"{Normalize(worldId, "world")}:{Normalize(name, "character")}";

    public static string Normalize(string value, string fallback)
    {
        var safe = new string((value ?? string.Empty).Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (safe.Contains("--", StringComparison.Ordinal))
        {
            safe = safe.Replace("--", "-", StringComparison.Ordinal);
        }

        return safe.Length == 0 ? fallback : safe;
    }
}
