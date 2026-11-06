using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealmGate.Client.Settings;

public sealed class ClientApplicationSettings
{
    public string DefaultWorld { get; set; } = "Classic";

    public string DefaultConnectionProfile { get; set; } = "Local Host Classic";

    public string HostBindAddress { get; set; } = "0.0.0.0";

    public int HostPort { get; set; } = 2323;

    public int HostMaximumSessions { get; set; } = 64;
}

public sealed class ClientApplicationSettingsStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true
    };

    public ClientApplicationSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        SettingsPath = Path.GetFullPath(settingsPath);
    }

    public string SettingsPath { get; }

    public static ClientApplicationSettingsStore CreateForCurrentUser()
    {
        var root = ResolveCurrentUserRoot();
        return new ClientApplicationSettingsStore(
            Path.Combine(root, "Settings", "application.json"));
    }

    public async Task<ClientApplicationSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
            return Normalize(new ClientApplicationSettings());

        try
        {
            var text = await File.ReadAllTextAsync(SettingsPath, cancellationToken)
                .ConfigureAwait(false);
            var root = JsonNode.Parse(text) as JsonObject;
            if (root is null)
                throw new JsonException("Application settings must be a JSON object.");

            return Normalize(new ClientApplicationSettings
            {
                DefaultWorld = ReadString(root, nameof(ClientApplicationSettings.DefaultWorld), "Classic"),
                DefaultConnectionProfile = ReadString(
                    root,
                    nameof(ClientApplicationSettings.DefaultConnectionProfile),
                    "Local Host Classic"),
                HostBindAddress = ReadString(
                    root,
                    nameof(ClientApplicationSettings.HostBindAddress),
                    "0.0.0.0"),
                HostPort = ReadInt(root, nameof(ClientApplicationSettings.HostPort), 2323),
                HostMaximumSessions = ReadInt(
                    root,
                    nameof(ClientApplicationSettings.HostMaximumSessions),
                    64)
            });
        }
        catch (JsonException)
        {
            BackupCorruptSettings();
            return Normalize(new ClientApplicationSettings());
        }
        catch (IOException)
        {
            return Normalize(new ClientApplicationSettings());
        }
        catch (UnauthorizedAccessException)
        {
            return Normalize(new ClientApplicationSettings());
        }
    }

    public async Task SaveAsync(
        ClientApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Normalize(settings);

        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(SettingsPath)
                ?? throw new InvalidOperationException("Application settings path has no parent directory."));

            var root = await ReadExistingObjectAsync(cancellationToken).ConfigureAwait(false);
            root[nameof(ClientApplicationSettings.DefaultWorld)] = settings.DefaultWorld;
            root[nameof(ClientApplicationSettings.DefaultConnectionProfile)] = settings.DefaultConnectionProfile;
            root[nameof(ClientApplicationSettings.HostBindAddress)] = settings.HostBindAddress;
            root[nameof(ClientApplicationSettings.HostPort)] = settings.HostPort;
            root[nameof(ClientApplicationSettings.HostMaximumSessions)] = settings.HostMaximumSessions;

            var temporaryPath = SettingsPath + ".tmp";
            await File.WriteAllTextAsync(
                    temporaryPath,
                    root.ToJsonString(_json),
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public static ClientApplicationSettings Normalize(ClientApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.DefaultWorld = Clean(settings.DefaultWorld, "Classic");
        settings.DefaultConnectionProfile = Clean(
            settings.DefaultConnectionProfile,
            "Local Host Classic");
        settings.HostBindAddress = Clean(settings.HostBindAddress, "0.0.0.0");
        if (settings.HostPort is < 1 or > 65535)
            settings.HostPort = 2323;
        if (settings.HostMaximumSessions is < 1 or > 4096)
            settings.HostMaximumSessions = 64;
        return settings;
    }

    private async Task<JsonObject> ReadExistingObjectAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SettingsPath))
            return new JsonObject();

        try
        {
            var text = await File.ReadAllTextAsync(SettingsPath, cancellationToken)
                .ConfigureAwait(false);
            return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            BackupCorruptSettings();
            return new JsonObject();
        }
    }

    private static string ResolveCurrentUserRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RealmGate");
        }

        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfig))
            return Path.Combine(xdgConfig, "RealmGate");

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
            return Path.Combine(userProfile, ".config", "RealmGate");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RealmGate");
    }

    private static string ReadString(JsonObject root, string name, string fallback)
    {
        var node = FindProperty(root, name);
        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? Clean(text, fallback)
            : fallback;
    }

    private static int ReadInt(JsonObject root, string name, int fallback)
    {
        var node = FindProperty(root, name);
        if (node is not JsonValue value)
            return fallback;
        if (value.TryGetValue<int>(out var number))
            return number;
        return value.TryGetValue<string>(out var text) && int.TryParse(text, out number)
            ? number
            : fallback;
    }

    private static JsonNode? FindProperty(JsonObject root, string name)
    {
        if (root.TryGetPropertyValue(name, out var exact))
            return exact;
        foreach (var property in root)
        {
            if (property.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }
        return null;
    }

    private static string Clean(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private void BackupCorruptSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;
            var backup = SettingsPath + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
            File.Move(SettingsPath, backup, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
