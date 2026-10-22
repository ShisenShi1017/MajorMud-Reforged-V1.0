using System.Collections.ObjectModel;
using System.IO;
using RealmGate.App.Models;
using RealmGate.Client.Profiles;
using RealmGate.Server;

namespace RealmGate.App.Services;

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
    private readonly ClientConnectionProfileCatalogStore _store;
    private ClientConnectionProfileCatalog _catalog = new();

    public ClientConnectionProfileService(string? rootPath = null)
    {
        if (rootPath is not null)
        {
            _store = new ClientConnectionProfileCatalogStore(
                Path.Combine(
                    rootPath,
                    "Client",
                    "ConnectionProfiles",
                    "connection-profiles.json"));
            return;
        }

#if PUBLIC_RELEASE
        // Public builds must never inherit the developer/private client's saved sessions.
        // In particular, do not migrate a Borin player profile from the old MajorMUD Reforged
        // store or reuse the private RealmGate client catalog. Public testers get an independent
        // profile catalog that persists normally between public-beta upgrades.
        _store = ClientConnectionProfileCatalogStore.CreateForCurrentUser(
            publicRelease: true,
            migrateLegacyBranding: false);
#else
        _store = ClientConnectionProfileCatalogStore.CreateForCurrentUser();
#endif
    }

    public ObservableCollection<ClientConnectionProfile> Profiles => _catalog.Profiles;
    public string ProfilePath => _store.ProfilePath;

    public async Task LoadAsync() =>
        _catalog = await _store.LoadAsync();

    public Task SaveAsync() => _store.SaveAsync(_catalog);

    public ClientConnectionProfile CreateNew(ClientSessionConnection? session = null)
    {
        var profile = new ClientConnectionProfile
        {
            ProfileName = MakeUniqueName("New BBS Profile"),
            SessionDisplayName = session?.DisplayName ?? $"Session {Math.Max(1, Profiles.Count + 1)}",
            ServerProfileName = session?.ServerProfileName ?? "Local Host",
            WorldName = session?.WorldName ?? "Classic",
            HostName = session?.HostName ?? "127.0.0.1",
            Port = session?.Port ?? RealmGateNetworkBinding.DefaultTelnetPort,
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
        var baseName = string.IsNullOrWhiteSpace(requestedName) ? "BBS Profile" : requestedName.Trim();
        var candidate = baseName;
        var suffix = 2;
        while (Profiles.Any(profile => string.Equals(profile.ProfileName, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseName} {suffix++}";
        return candidate;
    }

}
