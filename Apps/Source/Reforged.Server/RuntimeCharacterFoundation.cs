using System.Security.Cryptography;
using System.Text;
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
    int SaveVersion = 1,
    RuntimeItemState? ItemState = null);

public sealed record RuntimeCharacterDefinition(
    string Id,
    string Name,
    string Confidence,
    string Classification,
    IReadOnlyDictionary<string, int> MinimumStats,
    IReadOnlyDictionary<string, int> MaximumStats,
    int HitPointsPerLevelAdjustment,
    IReadOnlyList<RuntimeAbilityGrant> AbilitySlots,
    RuntimeClassHitPointDefinition? HitPoints,
    RuntimeMagicDefinition? Magic);

public sealed record RuntimeAbilityGrant(int AbilityId, int Value);

public sealed record RuntimeClassHitPointDefinition(
    int MinimumPerLevel,
    int MaximumBonusPerLevel,
    int MaximumPerLevel);

public sealed record RuntimeMagicDefinition(
    int Type,
    int Level);

public sealed class RuntimeCharacterCatalog
{
    private readonly Dictionary<string, RuntimeCharacterDefinition> _races = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeCharacterDefinition> _classes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<RuntimeCharacterDefinition> Races => _races.Values.OrderBy(value => value.Name).ToArray();
    public IReadOnlyCollection<RuntimeCharacterDefinition> Classes => _classes.Values.OrderBy(value => value.Name).ToArray();
    public bool CreationGateReady { get; private set; }
    public IReadOnlyList<string> CreationGateBlockers { get; private set; } = Array.Empty<string>();

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
        if (root.TryGetProperty("creation_gate", out var gate) && gate.ValueKind == JsonValueKind.Object)
        {
            CreationGateReady = gate.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True;
            CreationGateBlockers = ReadStringArray(gate, "blockers");
        }
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
                ReadString(record, "classification"),
                ReadIntDictionary(record, "minimum_stats"),
                ReadIntDictionary(record, "maximum_stats"),
                ReadInt(record, "hp_per_level_adjustment"),
                ReadAbilitySlots(record),
                ReadHitPoints(record),
                ReadMagic(record));
        }
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static IReadOnlyDictionary<string, int> ReadIntDictionary(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, int>();
        }

        return values.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out _))
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.OrdinalIgnoreCase);
    }


    private static IReadOnlyList<RuntimeAbilityGrant> ReadAbilitySlots(JsonElement element)
    {
        if (!element.TryGetProperty("ability_slots", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RuntimeAbilityGrant>();
        }

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object)
            .Select(value => new RuntimeAbilityGrant(ReadInt(value, "ability_id"), ReadInt(value, "value")))
            .Where(value => value.AbilityId > 0)
            .ToArray();
    }
    private static RuntimeClassHitPointDefinition? ReadHitPoints(JsonElement element)
    {
        if (!element.TryGetProperty("hit_points", out var hitPoints) || hitPoints.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RuntimeClassHitPointDefinition(
            ReadInt(hitPoints, "minimum_per_level"),
            ReadInt(hitPoints, "maximum_bonus_per_level"),
            ReadInt(hitPoints, "maximum_per_level"));
    }

    private static RuntimeMagicDefinition? ReadMagic(JsonElement element)
    {
        if (!element.TryGetProperty("magic", out var magic) || magic.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RuntimeMagicDefinition(ReadInt(magic, "type"), ReadInt(magic, "level"));
    }

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
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
    public RuntimeCharacterRecord? TryLoadCharacter(string characterId) => Read<RuntimeCharacterRecord>(CharacterPath(CanonicalCharacterId(characterId)));

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
    private string CharacterPath(string id)
    {
        id = CanonicalCharacterId(id);
        return Path.Combine(_root, "characters", PersistenceFileKey(id) + ".json");
    }

    private static string CanonicalCharacterId(string characterId)
    {
        var raw = (characterId ?? string.Empty).Trim();
        var separator = raw.IndexOf(':');
        if (separator < 0)
        {
            return RuntimeIdentity.Normalize(raw, "character");
        }

        var world = RuntimeIdentity.Normalize(raw[..separator], "world");
        var name = RuntimeIdentity.Normalize(raw[(separator + 1)..], "character");
        return $"{world}:{name}";
    }

    private static string PersistenceFileKey(string stableId)
    {
        // Character IDs are logical identifiers and may contain ':' (for example
        // "classic:hero"). A colon is not a legal normal filename character on
        // Windows and can be interpreted as an alternate data stream, causing
        // File.Move/File.Replace to fail with ERROR_INVALID_PARAMETER. Keep the
        // logical ID inside the JSON record and use a deterministic SHA-256 key
        // only for the on-disk filename.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(stableId));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

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
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Persistence path '{path}' has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporary = Path.Combine(directory, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var backup = path + ".bak";
        var payload = JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine;

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(payload);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }

            try
            {
                File.Replace(temporary, path, backup, ignoreMetadataErrors: true);
                TryDelete(backup);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithCopyFallback(temporary, path, backup);
            }
            catch (IOException)
            {
                // Some Windows filesystems and mounted/synchronized folders reject
                // File.Replace/File.Move overwrite semantics with ERROR_INVALID_PARAMETER.
                ReplaceWithCopyFallback(temporary, path, backup);
            }
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static void ReplaceWithCopyFallback(string temporary, string path, string backup)
    {
        File.Copy(path, backup, overwrite: true);
        try
        {
            File.Copy(temporary, path, overwrite: true);
            TryDelete(backup);
        }
        catch
        {
            if (File.Exists(backup))
            {
                File.Copy(backup, path, overwrite: true);
            }

            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup failure must not hide the persistence result. A later save can
            // safely replace stale .tmp/.bak files because names are unique/versioned.
        }
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
        if (!catalog.CreationGateReady)
        {
            var blockers = catalog.CreationGateBlockers.Count == 0 ? "unknown blockers" : string.Join(", ", catalog.CreationGateBlockers);
            throw new InvalidOperationException($"Character Foundation Phase003 creation gate is blocked: {blockers}.");
        }

        var start = world.ResolveGeneratedCharacterStartRoom()
            ?? throw new InvalidOperationException($"World '{world.Definition.Id}' has no generated Classic character-start room. Character creation is blocked rather than guessing a start.");
        var characterId = RuntimeIdentity.CharacterId(world.Definition.Id, name);
        var now = DateTimeOffset.UtcNow;
        const int startingLevel = 1;
        var health = raceDefinition.MinimumStats.TryGetValue("hea", out var startingHealth) ? startingHealth : 0;
        var classHitPoints = classDefinition.HitPoints ?? throw new InvalidOperationException($"Generated class '{classDefinition.Name}' has no hit-point definition.");
        // DLL-verified calculate_secondary_stats @0x0041a424.
        var healthAdjustmentProduct = (health - 50) * startingLevel;
        if (healthAdjustmentProduct < 0)
        {
            healthAdjustmentProduct += 15;
        }
        var constitutionAdjustment = healthAdjustmentProduct >> 4;
        var accumulatedHitPointRolls = classHitPoints.MaximumBonusPerLevel; // creation seeds the full roll cap
        var maximumHitPoints = (health / 2)
            + (startingLevel * classHitPoints.MinimumPerLevel)
            + (startingLevel * raceDefinition.HitPointsPerLevelAdjustment)
            + constitutionAdjustment
            + accumulatedHitPointRolls
            + constitutionAdjustment; // the Classic DLL adds con_adj twice
        maximumHitPoints = Math.Max(1, maximumHitPoints);
        var magic = classDefinition.Magic;
        var maximumMana = magic switch
        {
            null => 0,
            { Type: 0 } => 0,
            { Type: 5 } => Math.Max(0, startingLevel - 1),
            { Type: >= 1 and <= 4 } => (magic.Level * startingLevel * 2) + 6,
            _ => 0
        };
        var startingAbilities = raceDefinition.AbilitySlots
            .Concat(classDefinition.AbilitySlots)
            .GroupBy(value => value.AbilityId)
            .ToDictionary(
                group => $"ability.{group.Key}",
                group => group.Last().Value,
                StringComparer.OrdinalIgnoreCase);
        var character = new RuntimeCharacterRecord(
            characterId,
            account.AccountId,
            name.Trim(),
            world.Definition.Id,
            raceDefinition.Id,
            raceDefinition.Name,
            classDefinition.Id,
            classDefinition.Name,
            startingLevel,
            0,
            maximumHitPoints,
            maximumHitPoints,
            maximumMana,
            maximumMana,
            start.Id,
            start.RoomNumber,
            raceDefinition.MinimumStats,
            startingAbilities,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string, string?>(),
            new Dictionary<string, string> { ["alignment"] = "Neutral", ["evil_points"] = "0", ["online_alignment_minutes"] = "0" },
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
