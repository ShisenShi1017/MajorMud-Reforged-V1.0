using System.Text.Json;

namespace RealmGate.Client.Inventory;

public sealed class ClientInventoryAutomationProfileStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ClientInventoryAutomationProfileStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public static ClientInventoryAutomationProfileStore CreateForCurrentUser(bool publicRelease = false)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = publicRelease
            ? System.IO.Path.Combine(local, "RealmGate", "Public")
            : System.IO.Path.Combine(local, "RealmGate");
        return new(System.IO.Path.Combine(root, "Client", "Inventory", "automation-profiles.json"));
    }

    public async Task<ClientInventoryAutomationCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path)) return new();
        try
        {
            var catalog = JsonSerializer.Deserialize<ClientInventoryAutomationCatalog>(
                              await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false), _json)
                          ?? new ClientInventoryAutomationCatalog();
            Normalize(catalog);
            return catalog;
        }
        catch (JsonException)
        {
            BackupCorrupt();
            return new();
        }
        catch (IOException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
    }

    public async Task SaveAsync(ClientInventoryAutomationCatalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Normalize(catalog);
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temporary = Path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(catalog, _json), cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, Path, overwrite: true);
        }
        finally { _saveGate.Release(); }
    }

    public static ClientInventoryAutomationProfile GetOrCreate(
        ClientInventoryAutomationCatalog catalog,
        string worldId,
        string characterId,
        string characterName)
    {
        Normalize(catalog);
        var normalizedWorld = ClientInventoryStateDecoder.NormalizeIdentity(worldId);
        var normalizedCharacter = characterId.Trim();
        var profile = catalog.Profiles.FirstOrDefault(candidate =>
            candidate.WorldId.Equals(normalizedWorld, StringComparison.OrdinalIgnoreCase) &&
            candidate.CharacterId.Equals(normalizedCharacter, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            profile.CharacterName = characterName.Trim();
            return profile;
        }
        profile = new ClientInventoryAutomationProfile
        {
            ProfileKey = normalizedWorld + "|" + normalizedCharacter,
            WorldId = normalizedWorld,
            CharacterId = normalizedCharacter,
            CharacterName = characterName.Trim()
        };
        catalog.Profiles.Add(profile);
        return profile;
    }

    public static void Normalize(ClientInventoryAutomationCatalog catalog)
    {
        catalog.Version = 1;
        catalog.Profiles ??= [];
        foreach (var profile in catalog.Profiles)
        {
            profile.Version = 1;
            profile.WorldId = ClientInventoryStateDecoder.NormalizeIdentity(profile.WorldId);
            profile.CharacterId = profile.CharacterId?.Trim() ?? string.Empty;
            profile.CharacterName = profile.CharacterName?.Trim() ?? string.Empty;
            profile.ProfileKey = profile.WorldId + "|" + profile.CharacterId;
            profile.Preferences ??= new ClientInventoryAutomationPreferences();
            profile.EquipmentSets ??= [];
            profile.Rules ??= [];
            profile.Preferences.MaximumCommandsPerPass = Math.Clamp(profile.Preferences.MaximumCommandsPerPass, 1, 100);
            profile.Preferences.KeepCurrency = Math.Max(0, profile.Preferences.KeepCurrency);
            foreach (var set in profile.EquipmentSets)
            {
                set.Id = EnsureId(set.Id);
                set.Name = string.IsNullOrWhiteSpace(set.Name) ? "Equipment Set" : set.Name.Trim();
                set.Items ??= [];
            }
            foreach (var rule in profile.Rules)
            {
                rule.Id = EnsureId(rule.Id);
                rule.MatchValue = rule.MatchValue?.Trim() ?? string.Empty;
                rule.Notes = rule.Notes?.Trim() ?? string.Empty;
                rule.TargetQuantity = Math.Clamp(rule.TargetQuantity, 1, 9999);
                rule.Priority = Math.Clamp(rule.Priority, -100000, 100000);
            }
        }
    }

    private static string EnsureId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;

    private void BackupCorrupt()
    {
        try { File.Move(Path, Path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}", overwrite: false); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
