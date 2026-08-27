using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Reforged.App.Models;

namespace Reforged.App.Services;

public interface IClientConnectionProfileService
{
    ObservableCollection<ClientConnectionProfile> Profiles { get; }
    Task LoadAsync();
    Task SaveAsync();
    ClientConnectionProfile CreateNew(ClientSessionConnection? session = null);
    ClientConnectionProfile CreateFromSession(ClientSessionConnection session, string profileName);
    ClientConnectionProfile Duplicate(ClientConnectionProfile source, string profileName);
    void UpdateFromSession(ClientConnectionProfile profile, ClientSessionConnection session);
    void ApplyToSession(ClientConnectionProfile profile, ClientSessionConnection session);
    ClientConnectionProfile? GetAssignedProfile(string sessionId);
    ClientConnectionProfile? FindBestMatch(ClientSessionConnection session);
    void AssignToSession(string sessionId, string profileId);
    void RemoveAssignment(string sessionId);
    void Delete(ClientConnectionProfile profile);
    string MakeUniqueName(string requestedName);
}

public sealed class ClientConnectionProfileService : IClientConnectionProfileService
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private ClientConnectionProfileCatalog _catalog = new();

    public ClientConnectionProfileService(string? rootPath = null)
    {
        var root = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MajorMUD Reforged");
        ProfilePath = Path.Combine(root, "Client", "ConnectionProfiles", "connection-profiles.json");
    }

    public ObservableCollection<ClientConnectionProfile> Profiles => _catalog.Profiles;
    public string ProfilePath { get; }

    public async Task LoadAsync()
    {
        if (!File.Exists(ProfilePath))
        {
            _catalog = CreateDefaultCatalog();
            await TrySaveStartupCatalogAsync();
            return;
        }

        try
        {
            var text = await File.ReadAllTextAsync(ProfilePath);
            _catalog = JsonSerializer.Deserialize<ClientConnectionProfileCatalog>(text, _json)
                ?? CreateDefaultCatalog();
            NormalizeCatalog();
        }
        catch (JsonException)
        {
            BackupCorruptProfile();
            _catalog = CreateDefaultCatalog();
            await TrySaveStartupCatalogAsync();
        }
        catch (IOException)
        {
            _catalog = CreateDefaultCatalog();
        }
        catch (UnauthorizedAccessException)
        {
            _catalog = CreateDefaultCatalog();
        }
    }

    public async Task SaveAsync()
    {
        NormalizeCatalog();
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);

        await _saveGate.WaitAsync();
        try
        {
            var temporaryPath = ProfilePath + ".tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(_catalog, _json));
            File.Move(temporaryPath, ProfilePath, true);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public ClientConnectionProfile CreateNew(ClientSessionConnection? session = null)
    {
        var profile = new ClientConnectionProfile
        {
            ProfileName = MakeUniqueName("New Character"),
            SessionDisplayName = session?.DisplayName ?? $"Session {Math.Max(1, Profiles.Count + 1)}",
            ServerProfileName = session?.ServerProfileName ?? "Local Host",
            WorldName = session?.WorldName ?? "Classic",
            HostName = session?.HostName ?? "127.0.0.1",
            Port = session?.Port ?? 4000,
            AutoReconnect = session?.AutoReconnect ?? true,
            ReconnectDelaySeconds = session?.ReconnectDelaySeconds ?? 5,
            TerminalViewMode = ClientTerminalViewMode.Reforged,
            TerminalColumns = session?.TerminalColumns ?? 80,
            TerminalRows = session?.TerminalRows ?? 25,
            EnableTelnetNegotiation = session?.TerminalPreferences.EnableTelnetNegotiation ?? true,
            TerminalType = session?.TerminalPreferences.TerminalType ?? "ANSI",
            UseCodePage437 = session?.TerminalPreferences.UseCodePage437 ?? true,
            CaptureTelnetTraffic = session?.TerminalPreferences.CaptureTelnetTraffic ?? false,
            StripAnsiForParsing = session?.TerminalPreferences.StripAnsiForParsing ?? true,
            ShowAnsi = session?.TerminalPreferences.ShowAnsi ?? true,
            ShowTimestamps = session?.TerminalPreferences.ShowTimestamps ?? false,
            LoggingEnabled = session?.TerminalPreferences.LoggingEnabled ?? false,
            MaximumScrollbackLines = session?.TerminalPreferences.MaximumScrollbackLines ?? 10000,
            SeparateChannels = session?.TerminalPreferences.SeparateChannels ?? true
        };

        Profiles.Add(profile);
        return profile;
    }

    public ClientConnectionProfile CreateFromSession(ClientSessionConnection session, string profileName)
    {
        var profile = new ClientConnectionProfile
        {
            ProfileName = MakeUniqueName(profileName)
        };
        UpdateFromSession(profile, session);
        profile.CreatedAt = DateTimeOffset.Now;
        Profiles.Add(profile);
        return profile;
    }

    public ClientConnectionProfile Duplicate(ClientConnectionProfile source, string profileName)
    {
        var duplicate = source.Clone(MakeUniqueName(profileName));
        Profiles.Add(duplicate);
        return duplicate;
    }

    public void UpdateFromSession(ClientConnectionProfile profile, ClientSessionConnection session)
    {
        profile.SessionDisplayName = session.DisplayName;
        profile.CharacterName = session.CharacterName;
        profile.ServerProfileName = session.ServerProfileName;
        profile.WorldName = session.WorldName;
        profile.HostName = session.HostName;
        profile.Port = session.Port;
        profile.AutoReconnect = session.AutoReconnect;
        profile.ReconnectDelaySeconds = session.ReconnectDelaySeconds;
        profile.TerminalViewMode = ClientTerminalViewMode.Reforged;
        profile.TerminalColumns = session.TerminalColumns;
        profile.TerminalRows = session.TerminalRows;
        profile.EnableTelnetNegotiation = session.TerminalPreferences.EnableTelnetNegotiation;
        profile.TerminalType = session.TerminalPreferences.TerminalType;
        profile.UseCodePage437 = session.TerminalPreferences.UseCodePage437;
        profile.CaptureTelnetTraffic = session.TerminalPreferences.CaptureTelnetTraffic;
        profile.StripAnsiForParsing = session.TerminalPreferences.StripAnsiForParsing;
        profile.ShowAnsi = session.TerminalPreferences.ShowAnsi;
        profile.ShowTimestamps = session.TerminalPreferences.ShowTimestamps;
        profile.LoggingEnabled = session.TerminalPreferences.LoggingEnabled;
        profile.MaximumScrollbackLines = session.TerminalPreferences.MaximumScrollbackLines;
        profile.SeparateChannels = session.TerminalPreferences.SeparateChannels;
        profile.UpdatedAt = DateTimeOffset.Now;
    }

    public void ApplyToSession(ClientConnectionProfile profile, ClientSessionConnection session)
    {
        session.DisplayName = profile.SessionDisplayName;
        session.CharacterName = profile.CharacterName;
        session.ServerProfileName = profile.ServerProfileName;
        session.WorldName = profile.WorldName;
        session.HostName = profile.HostName;
        session.Port = profile.Port;
        session.AutoReconnect = profile.AutoReconnect;
        session.ReconnectDelaySeconds = profile.ReconnectDelaySeconds;
        session.TerminalViewMode = ClientTerminalViewMode.Reforged;
        session.TerminalColumns = profile.TerminalColumns;
        session.TerminalRows = profile.TerminalRows;
        session.TerminalPreferences.EnableTelnetNegotiation = profile.EnableTelnetNegotiation;
        session.TerminalPreferences.TerminalType = profile.TerminalType;
        session.TerminalPreferences.UseCodePage437 = profile.UseCodePage437;
        session.TerminalPreferences.CaptureTelnetTraffic = profile.CaptureTelnetTraffic;
        session.TerminalPreferences.StripAnsiForParsing = profile.StripAnsiForParsing;
        session.TerminalPreferences.ShowAnsi = profile.ShowAnsi;
        session.TerminalPreferences.ShowTimestamps = profile.ShowTimestamps;
        session.TerminalPreferences.LoggingEnabled = profile.LoggingEnabled;
        session.TerminalPreferences.MaximumScrollbackLines = profile.MaximumScrollbackLines;
        session.TerminalPreferences.SeparateChannels = profile.SeparateChannels;
    }

    public ClientConnectionProfile? GetAssignedProfile(string sessionId)
    {
        var assignment = _catalog.SessionAssignments.FirstOrDefault(item =>
            string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
            return null;

        return Profiles.FirstOrDefault(profile =>
            string.Equals(profile.ProfileId, assignment.ProfileId, StringComparison.OrdinalIgnoreCase));
    }

    public ClientConnectionProfile? FindBestMatch(ClientSessionConnection session)
    {
        return GetAssignedProfile(session.SessionId)
            ?? Profiles.FirstOrDefault(profile =>
                string.Equals(profile.CharacterName, session.CharacterName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.ServerProfileName, session.ServerProfileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.WorldName, session.WorldName, StringComparison.OrdinalIgnoreCase))
            ?? Profiles.FirstOrDefault(profile =>
                string.Equals(profile.HostName, session.HostName, StringComparison.OrdinalIgnoreCase) &&
                profile.Port == session.Port);
    }

    public void AssignToSession(string sessionId, string profileId)
    {
        var assignment = _catalog.SessionAssignments.FirstOrDefault(item =>
            string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));

        if (assignment is null)
        {
            _catalog.SessionAssignments.Add(new ClientSessionProfileAssignment
            {
                SessionId = sessionId,
                ProfileId = profileId,
                UpdatedAt = DateTimeOffset.Now
            });
            return;
        }

        assignment.ProfileId = profileId;
        assignment.UpdatedAt = DateTimeOffset.Now;
    }

    public void RemoveAssignment(string sessionId)
    {
        foreach (var assignment in _catalog.SessionAssignments
                     .Where(item => string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            _catalog.SessionAssignments.Remove(assignment);
        }
    }

    public void Delete(ClientConnectionProfile profile)
    {
        Profiles.Remove(profile);
        foreach (var assignment in _catalog.SessionAssignments
                     .Where(item => string.Equals(item.ProfileId, profile.ProfileId, StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            _catalog.SessionAssignments.Remove(assignment);
        }
    }

    public string MakeUniqueName(string requestedName)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedName) ? "Character Profile" : requestedName.Trim();
        var candidate = baseName;
        var suffix = 2;
        while (Profiles.Any(profile => string.Equals(profile.ProfileName, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseName} {suffix++}";
        return candidate;
    }

    private void NormalizeCatalog()
    {
        _catalog.Profiles ??= [];
        _catalog.SessionAssignments ??= [];
        _catalog.Version = 1;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in _catalog.Profiles.ToArray())
        {
            if (string.IsNullOrWhiteSpace(profile.ProfileId) || !ids.Add(profile.ProfileId))
            {
                do
                {
                    profile.ProfileId = Guid.NewGuid().ToString("N");
                }
                while (!ids.Add(profile.ProfileId));
            }

            profile.ProfileName = string.IsNullOrWhiteSpace(profile.ProfileName)
                ? MakeUniqueName("Character Profile")
                : profile.ProfileName;
            profile.SessionDisplayName = profile.SessionDisplayName;
            profile.ServerProfileName = profile.ServerProfileName;
            profile.WorldName = profile.WorldName;
            profile.HostName = profile.HostName;
            profile.Port = profile.Port;
            profile.TerminalColumns = profile.TerminalColumns;
            profile.TerminalRows = profile.TerminalRows;
            profile.TerminalType = profile.TerminalType;
            profile.MaximumScrollbackLines = profile.MaximumScrollbackLines;
        }

        if (_catalog.Profiles.Count == 0)
            _catalog.Profiles.Add(CreateLocalClassicProfile());

        var validProfileIds = _catalog.Profiles
            .Select(profile => profile.ProfileId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in _catalog.SessionAssignments
                     .Where(item => string.IsNullOrWhiteSpace(item.SessionId) || !validProfileIds.Contains(item.ProfileId))
                     .ToArray())
        {
            _catalog.SessionAssignments.Remove(assignment);
        }

        var duplicateAssignments = _catalog.SessionAssignments
            .GroupBy(item => item.SessionId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.OrderByDescending(item => item.UpdatedAt).Skip(1))
            .ToArray();
        foreach (var duplicate in duplicateAssignments)
            _catalog.SessionAssignments.Remove(duplicate);
    }

    private ClientConnectionProfileCatalog CreateDefaultCatalog()
    {
        var catalog = new ClientConnectionProfileCatalog();
        catalog.Profiles.Add(CreateLocalClassicProfile());
        return catalog;
    }

    private static ClientConnectionProfile CreateLocalClassicProfile() => new()
    {
        ProfileName = "Local Host Classic",
        SessionDisplayName = "Session 1",
        ServerProfileName = "Local Host",
        WorldName = "Classic",
        HostName = "127.0.0.1",
        Port = 4000,
        TerminalViewMode = ClientTerminalViewMode.Reforged,
        TerminalType = "ANSI",
        UseCodePage437 = true,
        EnableTelnetNegotiation = true
    };

    private async Task TrySaveStartupCatalogAsync()
    {
        try
        {
            await SaveAsync();
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

            var backup = ProfilePath + $".corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
            File.Move(ProfilePath, backup, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
