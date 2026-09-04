using System.IO;
using System.Text.Json;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

public interface IClientMapProfileService
{
    ClientMapProfile Profile { get; }
    Task SelectProfileAsync(string serverName, string worldName, string characterName);
    Task SaveAsync();
    ClientRoomMetadata RecordVisit(string roomName, string roomKey);
}

public sealed class ClientMapProfileService : IClientMapProfileService
{
    private readonly IApplicationSettingsService _settings;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public ClientMapProfile Profile { get; private set; } = CreateDefault("Local Host", "Classic", "Unassigned");

    public ClientMapProfileService(IApplicationSettingsService settings) => _settings = settings;

    public async Task SelectProfileAsync(string serverName, string worldName, string characterName)
    {
        var path = GetPath(serverName, worldName, characterName);
        if (File.Exists(path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<ClientMapProfile>(await File.ReadAllTextAsync(path), _json);
                if (loaded is not null) { Profile = loaded; return; }
            }
            catch { }
        }
        var sharedLegacyPath = GetLegacyPath(serverName, worldName);
        if (File.Exists(sharedLegacyPath))
        {
            try
            {
                var migrated = JsonSerializer.Deserialize<ClientMapProfile>(await File.ReadAllTextAsync(sharedLegacyPath), _json);
                if (migrated is not null)
                {
                    Profile = migrated;
                    Profile.CharacterName = NormalizeCharacter(characterName);
                    await SaveAsync();
                    return;
                }
            }
            catch { }
        }
        Profile = CreateDefault(serverName, worldName, characterName);
    }

    public async Task SaveAsync()
    {
        var path = GetPath(Profile.ServerName, Profile.WorldName, Profile.CharacterName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Profile, _json));
    }

    public ClientRoomMetadata RecordVisit(string roomName, string roomKey)
    {
        var normalizedKey = roomKey?.Trim() ?? string.Empty;
        var room = Profile.Rooms.FirstOrDefault(r => normalizedKey.Length > 0 && string.Equals(r.RoomKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
                   ?? Profile.Rooms.FirstOrDefault(r => string.Equals(r.RoomName, roomName, StringComparison.OrdinalIgnoreCase));
        if (room is null)
        {
            room = new ClientRoomMetadata { RoomKey = normalizedKey, RoomName = string.IsNullOrWhiteSpace(roomName) ? "Unknown Room" : roomName.Trim(), Layer = Profile.ActiveLayer, Discovered = true };
            Profile.Rooms.Add(room);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(roomName)) room.RoomName = roomName.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedKey)) room.RoomKey = normalizedKey;
            room.Discovered = true;
        }
        room.LastVisited = DateTimeOffset.Now;
        return room;
    }

    private string GetPath(string serverName, string worldName, string characterName) => Path.Combine(_settings.RootPath, "Client", "MapProfiles", $"map-{Slug(serverName)}-{Slug(worldName)}-{Slug(NormalizeCharacter(characterName))}.json");
    private string GetLegacyPath(string serverName, string worldName) => Path.Combine(_settings.RootPath, "Client", "MapProfiles", $"map-{Slug(serverName)}-{Slug(worldName)}.json");
    private static string NormalizeCharacter(string value) => string.IsNullOrWhiteSpace(value) ? "Unassigned" : value.Trim();
    private static string Slug(string value) => string.Concat((value ?? string.Empty).ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
    private static ClientMapProfile CreateDefault(string serverName, string worldName, string characterName) => new() { ServerName = string.IsNullOrWhiteSpace(serverName) ? "Local Host" : serverName, WorldName = string.IsNullOrWhiteSpace(worldName) ? "Classic" : worldName, CharacterName = NormalizeCharacter(characterName) };
}
