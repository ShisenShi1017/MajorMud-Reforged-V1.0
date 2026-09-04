using System.IO;
using System.Text.Json;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

public interface IClientSysGotoService
{
    ClientSysGotoProfile Profile { get; }
    Task SelectProfileAsync(string serverName, string worldName);
    Task SaveAsync();
    ClientSysGotoDestination? FindDestination(string nameOrAlias);
    string BuildCommand(ClientSysGotoDestination destination);
}

public sealed class ClientSysGotoService : IClientSysGotoService
{
    private readonly IApplicationSettingsService _settings;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public ClientSysGotoProfile Profile { get; private set; } = CreateDefault("Local Host", "Classic");

    public ClientSysGotoService(IApplicationSettingsService settings) => _settings = settings;

    public async Task SelectProfileAsync(string serverName, string worldName)
    {
        var path = GetPath(serverName, worldName);
        if (File.Exists(path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<ClientSysGotoProfile>(await File.ReadAllTextAsync(path), _json);
                if (loaded is not null) { Profile = loaded; return; }
            }
            catch { }
        }
        Profile = CreateDefault(serverName, worldName);
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GetPath(Profile.ServerName, Profile.WorldName))!);
        await File.WriteAllTextAsync(GetPath(Profile.ServerName, Profile.WorldName), JsonSerializer.Serialize(Profile, _json));
    }

    public ClientSysGotoDestination? FindDestination(string nameOrAlias)
    {
        var target = nameOrAlias.Trim();
        return Profile.Destinations.FirstOrDefault(d => d.Enabled &&
            (string.Equals(d.DisplayName, target, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(d.CommandArgument, target, StringComparison.OrdinalIgnoreCase) ||
             d.Aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(a => string.Equals(a, target, StringComparison.OrdinalIgnoreCase))));
    }

    public string BuildCommand(ClientSysGotoDestination destination)
    {
        var prefix = string.IsNullOrWhiteSpace(Profile.CommandPrefix) ? "sys goto" : Profile.CommandPrefix.Trim();
        var argument = destination.CommandArgument.Trim();
        if (argument.Length == 0) throw new InvalidOperationException("The selected city needs a server command argument.");
        return $"{prefix} {argument}";
    }

    private string GetPath(string serverName, string worldName) => Path.Combine(_settings.RootPath, "Client", "NavigationProfiles", $"sys-goto-{Slug(serverName)}-{Slug(worldName)}.json");
    private static string Slug(string value) => string.Concat((value ?? string.Empty).ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
    private static ClientSysGotoProfile CreateDefault(string serverName, string worldName) => new()
    {
        ServerName = string.IsNullOrWhiteSpace(serverName) ? "Local Host" : serverName,
        WorldName = string.IsNullOrWhiteSpace(worldName) ? "Classic" : worldName,
        Enabled = false,
        ConfirmBeforeTravel = true,
        FallbackToRoute = true,
        CommandPrefix = "sys goto"
    };
}
