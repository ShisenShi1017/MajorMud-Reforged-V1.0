using System.Text.Json;

namespace RealmGate.Client.Gameplay;

public sealed class ClientGameplayAutomationProfileStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ClientGameplayAutomationProfileStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public static ClientGameplayAutomationProfileStore CreateForCurrentUser(bool publicRelease = false)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = publicRelease ? System.IO.Path.Combine(local, "RealmGate", "Public") : System.IO.Path.Combine(local, "RealmGate");
        return new(System.IO.Path.Combine(root, "Client", "Gameplay", "automation-profiles.json"));
    }

    public async Task<ClientGameplayAutomationCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path)) return new();
        try
        {
            var result = JsonSerializer.Deserialize<ClientGameplayAutomationCatalog>(
                             await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false), _json) ?? new();
            Normalize(result);
            return result;
        }
        catch (JsonException) { BackupCorrupt(); return new(); }
        catch (IOException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
    }

    public async Task SaveAsync(ClientGameplayAutomationCatalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Normalize(catalog);
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temporary = Path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(catalog, _json), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, Path, true);
        }
        finally { _saveGate.Release(); }
    }

    public static ClientGameplayAutomationProfile GetOrCreate(
        ClientGameplayAutomationCatalog catalog, string worldId, string characterId, string characterName)
    {
        Normalize(catalog);
        var world = ClientGameplayAutomationStateDecoder.NormalizeIdentity(worldId);
        var character = characterId.Trim();
        var profile = catalog.Profiles.FirstOrDefault(candidate =>
            candidate.WorldId.Equals(world, StringComparison.OrdinalIgnoreCase) &&
            candidate.CharacterId.Equals(character, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            profile.CharacterName = characterName.Trim();
            return profile;
        }
        profile = new ClientGameplayAutomationProfile
        {
            ProfileKey = world + "|" + character,
            WorldId = world,
            CharacterId = character,
            CharacterName = characterName.Trim()
        };
        catalog.Profiles.Add(profile);
        return profile;
    }

    public static void Normalize(ClientGameplayAutomationCatalog catalog)
    {
        catalog.Version = 1;
        catalog.Profiles ??= [];
        foreach (var profile in catalog.Profiles)
        {
            profile.Version = 1;
            profile.WorldId = ClientGameplayAutomationStateDecoder.NormalizeIdentity(profile.WorldId);
            profile.CharacterId = profile.CharacterId?.Trim() ?? string.Empty;
            profile.CharacterName = profile.CharacterName?.Trim() ?? string.Empty;
            profile.ProfileKey = profile.WorldId + "|" + profile.CharacterId;
            profile.StateMaximumAgeSeconds = Math.Clamp(profile.StateMaximumAgeSeconds, 2, 60);
            profile.MinimumActionDelayMilliseconds = Math.Clamp(profile.MinimumActionDelayMilliseconds, 500, 60000);
            profile.Recovery ??= new();
            profile.Combat ??= new();
            profile.Party ??= new();
            profile.Buffs ??= [];
            profile.Recovery.EmergencyHealthPercent = Percent(profile.Recovery.EmergencyHealthPercent);
            profile.Recovery.HealBelowPercent = Percent(profile.Recovery.HealBelowPercent);
            profile.Recovery.RestBelowHealthPercent = Percent(profile.Recovery.RestBelowHealthPercent);
            profile.Recovery.MeditateBelowManaPercent = Percent(profile.Recovery.MeditateBelowManaPercent);
            profile.Combat.EscapeBelowHealthPercent = Percent(profile.Combat.EscapeBelowHealthPercent);
            profile.Combat.MinimumManaPercent = Percent(profile.Combat.MinimumManaPercent);
            profile.Combat.MaximumHostiles = Math.Clamp(profile.Combat.MaximumHostiles, 1, 100);
            profile.Combat.MaximumTargetLevelAboveCharacter = Math.Clamp(profile.Combat.MaximumTargetLevelAboveCharacter, 0, 1000);
            profile.Party.HealBelowPercent = Percent(profile.Party.HealBelowPercent);
            foreach (var buff in profile.Buffs)
            {
                buff.Id = string.IsNullOrWhiteSpace(buff.Id) ? Guid.NewGuid().ToString("N") : buff.Id.Trim();
                buff.Priority = Math.Clamp(buff.Priority, -100000, 100000);
                buff.MinimumManaPercent = Percent(buff.MinimumManaPercent);
                buff.Name = string.IsNullOrWhiteSpace(buff.Name) ? "Buff" : buff.Name.Trim();
                buff.EffectName = buff.EffectName?.Trim() ?? string.Empty;
                buff.Command = buff.Command?.Trim() ?? string.Empty;
            }
        }
    }

    private static int Percent(int value) => Math.Clamp(value, 0, 100);

    private void BackupCorrupt()
    {
        try { File.Move(Path, Path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}", false); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
