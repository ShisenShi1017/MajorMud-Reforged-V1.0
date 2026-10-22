using System.Text.Json;
using System.Text.Json.Nodes;
using RealmGate.Client.Navigation;

namespace RealmGate.Client.Automation;

public sealed class ClientInputAutomationProfileStore
{
    private static readonly string[] ManagedCapabilityNames =
    [
        "aliasesEnabled",
        "macrosEnabled",
        "textTriggersEnabled",
        "timedEventsEnabled",
        "navigationEnabled",
        "emergencyStop",
        "aliases",
        "macros",
        "textTriggers"
    ];

    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public ClientInputAutomationProfileStore(string profilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
        ProfilePath = Path.GetFullPath(profilePath);
    }

    public string ProfilePath { get; }

    public static ClientInputAutomationProfileStore CreateForCurrentUser(
        bool publicRelease = false)
    {
        var local = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var root = publicRelease
            ? Path.Combine(local, "RealmGate", "Public")
            : Path.Combine(local, "RealmGate");
        return new ClientInputAutomationProfileStore(Path.Combine(
            root,
            "Client",
            "Profiles",
            "automation-default.json"));
    }

    public async Task<ClientInputAutomationProfile> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ProfilePath))
            return new ClientInputAutomationProfile();

        try
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(
                    ProfilePath,
                    cancellationToken)
                .ConfigureAwait(false)) as JsonObject;
            var profile = root?["capabilities"]?.Deserialize<ClientInputAutomationProfile>(_json)
                          ?? new ClientInputAutomationProfile();
            profile.MinimumCommandDelayMs = root?["minimumCommandDelayMs"]?.GetValue<int?>()
                                            ?? profile.MinimumCommandDelayMs;
            profile.Events = root?["options"]?["events"]?.Deserialize<EventAutomationOptions>(_json)
                             ?? profile.Events;
            profile.Navigation = root?["options"]?["navigation"]?.Deserialize<ClientNavigationAutomationOptions>(_json)
                                 ?? profile.Navigation;
            Normalize(profile);
            return profile;
        }
        catch (JsonException)
        {
            BackupCorruptProfile();
            return new ClientInputAutomationProfile();
        }
        catch (IOException)
        {
            return new ClientInputAutomationProfile();
        }
        catch (UnauthorizedAccessException)
        {
            return new ClientInputAutomationProfile();
        }
    }

    public async Task SaveAsync(
        ClientInputAutomationProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Normalize(profile);

        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            JsonObject root;
            if (File.Exists(ProfilePath))
            {
                try
                {
                    root = JsonNode.Parse(await File.ReadAllTextAsync(
                            ProfilePath,
                            cancellationToken)
                        .ConfigureAwait(false)) as JsonObject ?? new JsonObject();
                }
                catch (JsonException)
                {
                    BackupCorruptProfile();
                    root = new JsonObject();
                }
            }
            else
            {
                root = new JsonObject
                {
                    ["schemaVersion"] = 3,
                    ["name"] = "Default",
                    ["enabled"] = false,
                    ["pauseAll"] = true
                };
            }

            var capabilities = root["capabilities"] as JsonObject ?? new JsonObject();
            var serialized = JsonSerializer.SerializeToNode(profile, _json) as JsonObject
                             ?? throw new InvalidOperationException(
                                 "Input automation profile could not be serialized.");
            foreach (var name in ManagedCapabilityNames)
            {
                capabilities[name] = serialized[name]?.DeepClone();
            }

            root["capabilities"] = capabilities;
            root["minimumCommandDelayMs"] = profile.MinimumCommandDelayMs;
            var options = root["options"] as JsonObject ?? new JsonObject();
            options["events"] = JsonSerializer.SerializeToNode(profile.Events, _json);
            options["navigation"] = JsonSerializer.SerializeToNode(profile.Navigation, _json);
            root["options"] = options;
            Directory.CreateDirectory(
                Path.GetDirectoryName(ProfilePath)
                ?? throw new InvalidOperationException(
                    "Automation profile path has no parent directory."));
            var temporaryPath = ProfilePath + ".tmp";
            await File.WriteAllTextAsync(
                    temporaryPath,
                    root.ToJsonString(_json),
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, ProfilePath, overwrite: true);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public static void Normalize(ClientInputAutomationProfile profile)
    {
        profile.Aliases ??= [];
        profile.Macros ??= [];
        profile.TextTriggers ??= [];
        profile.Events ??= new EventAutomationOptions();
        profile.Navigation ??= new ClientNavigationAutomationOptions();
        profile.Navigation.Preferences ??= new ClientRoutePreferences();
        profile.Events.ScheduledEvents ??= [];
        profile.MinimumCommandDelayMs = Math.Clamp(profile.MinimumCommandDelayMs, 0, 60000);
        profile.Navigation.CharacterLevel = Math.Clamp(profile.Navigation.CharacterLevel, 1, 999);
        profile.Navigation.AutoRoamDelayMs = Math.Clamp(profile.Navigation.AutoRoamDelayMs, 500, 60000);
        profile.Navigation.MaximumRecoveryAttempts = Math.Clamp(
            profile.Navigation.MaximumRecoveryAttempts,
            0,
            10);
        profile.Navigation.Preferences.NpcLevelBuffer = Math.Clamp(
            profile.Navigation.Preferences.NpcLevelBuffer,
            0,
            99);

        foreach (var alias in profile.Aliases)
        {
            alias.Id = EnsureId(alias.Id);
            alias.Name = alias.Name?.Trim() ?? string.Empty;
            alias.Command ??= string.Empty;
        }

        foreach (var macro in profile.Macros)
        {
            macro.Id = EnsureId(macro.Id);
            macro.Name = macro.Name?.Trim() ?? string.Empty;
            macro.Commands ??= [];
        }

        foreach (var trigger in profile.TextTriggers)
        {
            trigger.Id = EnsureId(trigger.Id);
            trigger.Name = trigger.Name?.Trim() ?? string.Empty;
            trigger.MatchText ??= string.Empty;
            trigger.Command ??= string.Empty;
            trigger.CooldownSeconds = Math.Clamp(
                trigger.CooldownSeconds,
                0,
                86400);
            trigger.Priority = Math.Clamp(
                trigger.Priority,
                -100000,
                100000);
            if (trigger.OnlyOutOfCombat && trigger.OnlyInCombat)
                trigger.OnlyInCombat = false;
        }

        foreach (var scheduledEvent in profile.Events.ScheduledEvents)
        {
            scheduledEvent.Id = EnsureId(scheduledEvent.Id);
            scheduledEvent.Name = string.IsNullOrWhiteSpace(scheduledEvent.Name)
                ? "Event"
                : scheduledEvent.Name.Trim();
            scheduledEvent.Command ??= string.Empty;
            scheduledEvent.IntervalMinutes = Math.Max(1, scheduledEvent.IntervalMinutes);
            scheduledEvent.IntervalSeconds = Math.Clamp(scheduledEvent.IntervalSeconds, 0, 86400);
            scheduledEvent.Priority = Math.Clamp(scheduledEvent.Priority, -100000, 100000);
            if (scheduledEvent.OnlyOutOfCombat && scheduledEvent.OnlyInCombat)
                scheduledEvent.OnlyInCombat = false;
        }
    }

    private static string EnsureId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;

    private void BackupCorruptProfile()
    {
        if (!File.Exists(ProfilePath))
            return;

        try
        {
            File.Move(
                ProfilePath,
                ProfilePath + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}",
                overwrite: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
