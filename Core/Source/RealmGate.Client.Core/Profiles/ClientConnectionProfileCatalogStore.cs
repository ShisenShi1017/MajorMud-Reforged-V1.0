using System.Text.Json;

namespace RealmGate.Client.Profiles;

public sealed class ClientConnectionProfileCatalogStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string? _legacyProfilePath;

    public ClientConnectionProfileCatalogStore(
        string profilePath,
        string? legacyProfilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
        ProfilePath = Path.GetFullPath(profilePath);
        _legacyProfilePath = string.IsNullOrWhiteSpace(legacyProfilePath)
            ? null
            : Path.GetFullPath(legacyProfilePath);
    }

    public string ProfilePath { get; }

    public static ClientConnectionProfileCatalogStore CreateForCurrentUser(
        bool publicRelease = false,
        bool migrateLegacyBranding = true)
    {
        var local = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var realmGateRoot = publicRelease
            ? Path.Combine(local, "RealmGate", "Public")
            : Path.Combine(local, "RealmGate");
        var profilePath = Path.Combine(
            realmGateRoot,
            "Client",
            "ConnectionProfiles",
            "connection-profiles.json");
        var legacyPath = !publicRelease && migrateLegacyBranding
            ? Path.Combine(
                local,
                "MajorMUD Reforged",
                "Client",
                "ConnectionProfiles",
                "connection-profiles.json")
            : null;
        return new ClientConnectionProfileCatalogStore(profilePath, legacyPath);
    }

    public async Task<ClientConnectionProfileCatalog> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        TryMigrateLegacyProfile();
        if (!File.Exists(ProfilePath))
        {
            var created = CreateDefaultCatalog();
            await TrySaveStartupCatalogAsync(created, cancellationToken)
                .ConfigureAwait(false);
            return created;
        }

        try
        {
            var text = await File.ReadAllTextAsync(ProfilePath, cancellationToken)
                .ConfigureAwait(false);
            var catalog = JsonSerializer.Deserialize<ClientConnectionProfileCatalog>(
                              text,
                              _json)
                          ?? CreateDefaultCatalog();
            Normalize(catalog);
            return catalog;
        }
        catch (JsonException)
        {
            BackupCorruptProfile();
            var created = CreateDefaultCatalog();
            await TrySaveStartupCatalogAsync(created, cancellationToken)
                .ConfigureAwait(false);
            return created;
        }
        catch (IOException)
        {
            return CreateDefaultCatalog();
        }
        catch (UnauthorizedAccessException)
        {
            return CreateDefaultCatalog();
        }
    }

    public async Task SaveAsync(
        ClientConnectionProfileCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Normalize(catalog);
        Directory.CreateDirectory(
            Path.GetDirectoryName(ProfilePath)
            ?? throw new InvalidOperationException("Profile path has no parent directory."));

        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var temporaryPath = ProfilePath + ".tmp";
            await File.WriteAllTextAsync(
                    temporaryPath,
                    JsonSerializer.Serialize(catalog, _json),
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, ProfilePath, overwrite: true);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public static void Normalize(ClientConnectionProfileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        catalog.Profiles ??= [];
        catalog.SessionAssignments ??= [];
        catalog.Version = 1;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in catalog.Profiles.ToArray())
        {
            if (string.IsNullOrWhiteSpace(profile.ProfileId) || !ids.Add(profile.ProfileId))
            {
                do
                {
                    profile.ProfileId = Guid.NewGuid().ToString("N");
                }
                while (!ids.Add(profile.ProfileId));
            }

            if (string.IsNullOrWhiteSpace(profile.ProfileName))
                profile.ProfileName = MakeUniqueName(names, "BBS Profile");
            names.Add(profile.ProfileName);

            if (string.Equals(profile.ProfileName, "Local Host Classic", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.ServerProfileName, "Local Host", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.HostName, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                profile.Port == 4000)
            {
                profile.Port = ClientConnectionProfileDefaults.TelnetPort;
            }
        }

        if (catalog.Profiles.Count == 0)
            catalog.Profiles.Add(CreateLocalClassicProfile());

        var validProfileIds = catalog.Profiles
            .Select(profile => profile.ProfileId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in catalog.SessionAssignments
                     .Where(item => string.IsNullOrWhiteSpace(item.SessionId) ||
                                    !validProfileIds.Contains(item.ProfileId))
                     .ToArray())
        {
            catalog.SessionAssignments.Remove(assignment);
        }

        var duplicateAssignments = catalog.SessionAssignments
            .GroupBy(item => item.SessionId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group
                .OrderByDescending(item => item.UpdatedAt)
                .Skip(1))
            .ToArray();
        foreach (var duplicate in duplicateAssignments)
            catalog.SessionAssignments.Remove(duplicate);
    }

    public static ClientConnectionProfileCatalog CreateDefaultCatalog()
    {
        var catalog = new ClientConnectionProfileCatalog();
        catalog.Profiles.Add(CreateLocalClassicProfile());
        return catalog;
    }

    public static ClientConnectionProfile CreateLocalClassicProfile() => new()
    {
        ProfileName = "Local Host Classic",
        SessionDisplayName = "Session 1",
        ServerProfileName = "Local Host",
        WorldName = "Classic",
        HostName = "127.0.0.1",
        Port = ClientConnectionProfileDefaults.TelnetPort,
        TerminalViewMode = ClientTerminalViewMode.Reforged,
        TerminalType = "ANSI",
        UseCodePage437 = true,
        EnableTelnetNegotiation = true
    };

    private static string MakeUniqueName(
        HashSet<string> existingNames,
        string requestedName)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedName)
            ? "BBS Profile"
            : requestedName.Trim();
        var candidate = baseName;
        var suffix = 2;
        while (existingNames.Contains(candidate))
            candidate = $"{baseName} {suffix++}";
        return candidate;
    }

    private async Task TrySaveStartupCatalogAsync(
        ClientConnectionProfileCatalog catalog,
        CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(catalog, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void TryMigrateLegacyProfile()
    {
        if (string.IsNullOrWhiteSpace(_legacyProfilePath) ||
            File.Exists(ProfilePath) ||
            !File.Exists(_legacyProfilePath))
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
            File.Copy(_legacyProfilePath, ProfilePath, overwrite: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void BackupCorruptProfile()
    {
        try
        {
            if (!File.Exists(ProfilePath))
                return;

            var backup = ProfilePath +
                         $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
            File.Move(ProfilePath, backup, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
