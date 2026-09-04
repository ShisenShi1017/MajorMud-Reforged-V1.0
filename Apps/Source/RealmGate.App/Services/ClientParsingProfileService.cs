using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealmGate.App.Models;

namespace RealmGate.App.Services;

public interface IClientParsingProfileService
{
    ClientParsingProfile Profile { get; }
    IReadOnlyList<ClientParsingProfileSummary> AvailableProfiles { get; }
    Task LoadAsync();
    Task SaveAsync();
    Task SelectProfileAsync(string serverName, string worldName);
    Task SelectProfileByKeyAsync(string key);
    Task DuplicateActiveAsync(string newName, string serverName, string worldName);
    Task DeleteActiveAsync();
    Task ExportActiveAsync(string path);
    Task ImportAsync(string path);
    bool IsMatch(ClientParsingRuleKind kind, string line);
    bool IsMatch(ClientParsingRuleKind kind, string line, string serverName, string worldName);
    Match? MatchFirst(ClientParsingRuleKind kind, string line);
    Match? MatchFirst(ClientParsingRuleKind kind, string line, string serverName, string worldName);
    string PrepareLine(string line);
    string PrepareLine(string line, string serverName, string worldName);
    ClientParsingTestMatch TestLine(string line);
    void AddRule(ClientParsingRuleKind kind);
    void RemoveRule(ClientParsingRule? rule);
    void RestoreDefaults();
}

public sealed partial class ClientParsingProfileService : IClientParsingProfileService
{
    private const string ClassicProfileExample = "parsing-local-host-classic.json";
    private readonly IApplicationSettingsService _settings;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Dictionary<string, ClientParsingProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private string _activeKey = MakeKey("Local Host", "Classic");

    public ClientParsingProfile Profile { get; private set; } = CreateDefaults();
    public IReadOnlyList<ClientParsingProfileSummary> AvailableProfiles => _profiles
        .OrderBy(pair => pair.Value.ServerName).ThenBy(pair => pair.Value.WorldName)
        .Select(pair => new ClientParsingProfileSummary
        {
            Key = pair.Key,
            Name = pair.Value.Name,
            ServerName = pair.Value.ServerName,
            WorldName = pair.Value.WorldName
        }).ToArray();

    public ClientParsingProfileService(IApplicationSettingsService settings) => _settings = settings;

    public async Task LoadAsync()
    {
        _profiles.Clear();
        var directory = GetDirectory();
        Directory.CreateDirectory(directory);
        foreach (var path in Directory.EnumerateFiles(directory, "parsing-*.json"))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<ClientParsingProfile>(await File.ReadAllTextAsync(path), _json);
                if (loaded is not null && loaded.Rules.Count > 0)
                {
                    EnsureCurrentDefaults(loaded);
                    _profiles[MakeKey(loaded.ServerName, loaded.WorldName)] = loaded;
                }
            }
            catch { }
        }

        if (_profiles.Count == 0)
        {
            var defaults = CreateDefaults();
            _profiles[MakeKey(defaults.ServerName, defaults.WorldName)] = defaults;
        }

        if (!_profiles.TryGetValue(_activeKey, out var selected))
            selected = _profiles.Values.First();
        Profile = selected;
        _activeKey = MakeKey(Profile.ServerName, Profile.WorldName);
    }

    public async Task SaveAsync()
    {
        ValidateRules(Profile);
        _activeKey = MakeKey(Profile.ServerName, Profile.WorldName);
        _profiles[_activeKey] = Profile;
        var path = GetPath(Profile.ServerName, Profile.WorldName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Profile, _json));
    }

    public async Task SelectProfileAsync(string serverName, string worldName)
    {
        var key = MakeKey(serverName, worldName);
        if (!_profiles.TryGetValue(key, out var profile))
        {
            profile = CreateDefaults();
            profile.Name = $"{Normalize(serverName, "Server")} {Normalize(worldName, "World")}";
            profile.ServerName = Normalize(serverName, "Local Host");
            profile.WorldName = Normalize(worldName, "Classic");
            _profiles[key] = profile;
            await File.WriteAllTextAsync(GetPath(profile.ServerName, profile.WorldName), JsonSerializer.Serialize(profile, _json));
        }
        Profile = profile;
        _activeKey = key;
    }


    public async Task SelectProfileByKeyAsync(string key)
    {
        if (!_profiles.TryGetValue(key, out var profile))
            throw new InvalidOperationException("The selected Client profile is no longer available.");
        Profile = profile;
        _activeKey = key;
        await Task.CompletedTask;
    }

    public async Task DuplicateActiveAsync(string newName, string serverName, string worldName)
    {
        var clone = Clone(Profile);
        clone.Name = Normalize(newName, $"{Normalize(serverName, "Server")} {Normalize(worldName, "World")}");
        clone.ServerName = Normalize(serverName, "Local Host");
        clone.WorldName = Normalize(worldName, "Classic");
        var key = MakeKey(clone.ServerName, clone.WorldName);
        if (_profiles.ContainsKey(key))
            throw new InvalidOperationException("A Client parsing profile already exists for that server and world.");
        Profile = clone;
        _activeKey = key;
        _profiles[key] = clone;
        await SaveAsync();
    }

    public async Task DeleteActiveAsync()
    {
        if (_profiles.Count <= 1)
            throw new InvalidOperationException("At least one Client parsing profile must remain.");
        var deleting = _activeKey;
        var path = GetPath(Profile.ServerName, Profile.WorldName);
        _profiles.Remove(deleting);
        if (File.Exists(path)) File.Delete(path);
        var replacement = _profiles.OrderBy(pair => pair.Value.ServerName).ThenBy(pair => pair.Value.WorldName).First();
        _activeKey = replacement.Key;
        Profile = replacement.Value;
        await Task.CompletedTask;
    }

    public async Task ExportActiveAsync(string path)
    {
        ValidateRules(Profile);
        var package = new ClientParsingProfilePackage
        {
            Format = "RealmGate Client Parsing Profile",
            Version = 1,
            ExportedUtc = DateTimeOffset.UtcNow,
            Profile = Clone(Profile)
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(package, _json));
    }

    public async Task ImportAsync(string path)
    {
        var package = JsonSerializer.Deserialize<ClientParsingProfilePackage>(await File.ReadAllTextAsync(path), _json)
            ?? throw new InvalidDataException("The selected file is not a valid Client parsing profile.");
        var supportedFormat =
            string.Equals(package.Format, "RealmGate Client Parsing Profile", StringComparison.Ordinal) ||
            string.Equals(package.Format, "MajorMUD Reforged Client Parsing Profile", StringComparison.Ordinal);
        if (!supportedFormat || package.Version != 1)
            throw new InvalidDataException("This Client profile format is not supported.");
        var imported = package.Profile ?? throw new InvalidDataException("The selected Client profile contains no profile data.");
        ValidateRules(imported);
        imported.Name = Normalize(imported.Name, "Imported Client Profile");
        imported.ServerName = Normalize(imported.ServerName, "Imported Server");
        imported.WorldName = Normalize(imported.WorldName, "Classic");
        var key = MakeKey(imported.ServerName, imported.WorldName);
        if (_profiles.ContainsKey(key))
        {
            imported.ServerName = $"{imported.ServerName} (Imported)";
            key = MakeKey(imported.ServerName, imported.WorldName);
        }
        Profile = imported;
        _activeKey = key;
        _profiles[key] = imported;
        await SaveAsync();
    }

    public string PrepareLine(string line) => PrepareLine(line, Profile.ServerName, Profile.WorldName);
    public string PrepareLine(string line, string serverName, string worldName)
    {
        var profile = Resolve(serverName, worldName);
        return profile.StripAnsi ? StripAnsi().Replace(line, string.Empty).Trim() : line.Trim();
    }

    public bool IsMatch(ClientParsingRuleKind kind, string line) => MatchFirst(kind, line) is not null;
    public bool IsMatch(ClientParsingRuleKind kind, string line, string serverName, string worldName)
        => MatchFirst(kind, line, serverName, worldName) is not null;

    public Match? MatchFirst(ClientParsingRuleKind kind, string line)
        => MatchFirst(kind, line, Profile.ServerName, Profile.WorldName);

    public Match? MatchFirst(ClientParsingRuleKind kind, string line, string serverName, string worldName)
    {
        var profile = Resolve(serverName, worldName);
        var clean = profile.StripAnsi ? StripAnsi().Replace(line, string.Empty).Trim() : line.Trim();
        foreach (var rule in profile.Rules.Where(r => r.Enabled && r.Kind == kind && !string.IsNullOrWhiteSpace(r.Pattern)))
        {
            try
            {
                var options = profile.CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
                var match = Regex.Match(clean, rule.Pattern, options, TimeSpan.FromMilliseconds(100));
                if (match.Success) return match;
            }
            catch (ArgumentException) { }
            catch (RegexMatchTimeoutException) { }
        }
        return null;
    }

    public ClientParsingTestMatch TestLine(string line)
    {
        var clean = PrepareLine(line);
        foreach (var rule in Profile.Rules.Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.Pattern)))
        {
            try
            {
                var options = Profile.CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
                var match = Regex.Match(clean, rule.Pattern, options, TimeSpan.FromMilliseconds(100));
                if (!match.Success) continue;
                var captures = string.Join(" • ", match.Groups.Cast<Group>().Skip(1).Where(g => g.Success).Select((g, i) => $"Group {i + 1}: {g.Value}"));
                return new ClientParsingTestMatch(true, rule.Kind, rule.Name, clean, captures, $"Matched {rule.Kind} using '{rule.Name}'.");
            }
            catch (ArgumentException exception)
            {
                return new ClientParsingTestMatch(false, rule.Kind, rule.Name, clean, string.Empty, $"Rule '{rule.Name}' is invalid: {exception.Message}");
            }
            catch (RegexMatchTimeoutException)
            {
                return new ClientParsingTestMatch(false, rule.Kind, rule.Name, clean, string.Empty, $"Rule '{rule.Name}' took too long and was stopped.");
            }
        }
        return new ClientParsingTestMatch(false, null, string.Empty, clean, string.Empty, "No enabled recognition rule matched this sample line.");
    }

    public void AddRule(ClientParsingRuleKind kind) => Profile.Rules.Add(new ClientParsingRule
    {
        Kind = kind,
        Name = $"Custom {kind}",
        Pattern = kind == ClientParsingRuleKind.RoomTitle ? @"^(.+)$" : @"change-me",
        Description = "Client-only recognition rule for this server/world profile."
    });

    public void RemoveRule(ClientParsingRule? rule)
    {
        if (rule is not null) Profile.Rules.Remove(rule);
    }

    public void RestoreDefaults()
    {
        var defaults = CreateDefaults();
        defaults.Name = Profile.Name;
        defaults.ServerName = Profile.ServerName;
        defaults.WorldName = Profile.WorldName;
        Profile = defaults;
        _activeKey = MakeKey(Profile.ServerName, Profile.WorldName);
        _profiles[_activeKey] = Profile;
    }

    private ClientParsingProfile Resolve(string serverName, string worldName)
    {
        var key = MakeKey(serverName, worldName);
        return _profiles.TryGetValue(key, out var profile) ? profile : Profile;
    }

    private static void ValidateRules(ClientParsingProfile profile)
    {
        foreach (var rule in profile.Rules.Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.Pattern)))
            _ = Regex.Match(string.Empty, rule.Pattern, profile.CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    private string GetDirectory() => Path.Combine(_settings.RootPath, "Client", "Profiles");
    private string GetPath(string serverName, string worldName) => Path.Combine(GetDirectory(), $"parsing-{Slug(serverName)}-{Slug(worldName)}.json");
    private static string MakeKey(string serverName, string worldName) => $"{Normalize(serverName, "Local Host")}::{Normalize(worldName, "Classic")}";
    private static string Normalize(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return Regex.Replace(new string(chars), "-+", "-").Trim('-');
    }

    private static ClientParsingProfile Clone(ClientParsingProfile source)
    {
        var clone = new ClientParsingProfile
        {
            Name = source.Name,
            ServerName = source.ServerName,
            WorldName = source.WorldName,
            StripAnsi = source.StripAnsi,
            CaseInsensitive = source.CaseInsensitive
        };
        foreach (var rule in source.Rules)
            clone.Rules.Add(new ClientParsingRule
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = rule.Kind,
                Enabled = rule.Enabled,
                Name = rule.Name,
                Pattern = rule.Pattern,
                Description = rule.Description
            });
        return clone;
    }

    private sealed class ClientParsingProfilePackage
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public DateTimeOffset ExportedUtc { get; set; }
        public ClientParsingProfile? Profile { get; set; }
    }

    private static ClientParsingProfile CreateDefaults()
    {
        var profile = new ClientParsingProfile();
        Add(profile, ClientParsingRuleKind.CombinedVitals, "Combined HP/MP prompt", @"(?:HP|H)\s*[:=]?\s*(\d+)\s*/\s*(\d+).*?(?:MP|M|Mana)\s*[:=]?\s*(\d+)\s*/\s*(\d+)");
        Add(profile, ClientParsingRuleKind.Health, "Health value", @"(?:HP|Health)\s*[:=]?\s*(\d+)\s*/\s*(\d+)");
        Add(profile, ClientParsingRuleKind.Health, "Health percent", @"(?:HP|Health)\s*[:=]?\s*(\d{1,3})%");
        Add(profile, ClientParsingRuleKind.Mana, "Mana value", @"(?:MP|Mana)\s*[:=]?\s*(\d+)\s*/\s*(\d+)");
        Add(profile, ClientParsingRuleKind.Mana, "Mana percent", @"(?:MP|Mana)\s*[:=]?\s*(\d{1,3})%");
        Add(profile, ClientParsingRuleKind.Currency, "Cash total", @"(?:Cash|Coins|Gold)\s*[:=]?\s*([0-9,]+)");
        Add(profile, ClientParsingRuleKind.CombatStart, "Combat starts", @"attacks you|you attack|combat begins|engage in combat");
        Add(profile, ClientParsingRuleKind.CombatEnd, "Combat ends", @"combat ends|no longer fighting|you have defeated|is dead");
        Add(profile, ClientParsingRuleKind.Resting, "Resting state", @"you (?:sit|rest|meditate)|you are (?:now )?(?:resting|meditating)");
        Add(profile, ClientParsingRuleKind.MovementRejected, "Movement rejected", @"can't go|cannot go|no exit|blocked|locked|unable to move|you may not");
        Add(profile, ClientParsingRuleKind.Experience, "Experience and next level", @"(?:Experience|Exp|XP)\s*[:=]?\s*([0-9,]+)(?:\s*(?:/|of|needed(?:\s+for\s+next\s+level)?[:=]?)\s*([0-9,]+))?");
        Add(profile, ClientParsingRuleKind.Level, "Character level", @"(?:Level|Lvl)\s*[:=]?\s*(\d+)");
        Add(profile, ClientParsingRuleKind.InventoryHeader, "Inventory begins", @"^(?:You are carrying|Inventory|Items carried|You have the following items|Contents of your inventory)\s*:?$");
        Add(profile, ClientParsingRuleKind.InventoryFooter, "Inventory ends", @"^(?:Total weight|Encumbrance|You are carrying nothing|End of inventory|Inventory complete)\b");
        Add(profile, ClientParsingRuleKind.InventoryItem, "Inventory item", @"^\s*(?:[-*]\s*)?(?:\d+\s*[xX]\s*)?(.+?)\s*$");
        Add(profile, ClientParsingRuleKind.EquipmentHeader, "Equipment begins", @"^(?:Equipment|Worn items|You are wearing|Equipped items|Your equipment)\s*:?$");
        Add(profile, ClientParsingRuleKind.EquipmentFooter, "Equipment ends", @"^(?:End of equipment|Total armor|Armor class|You are wearing nothing|Equipment complete)\b");
        Add(profile, ClientParsingRuleKind.EquipmentItem, "Equipped item", @"^\s*(?:<[^>]+>|\[[^\]]+\]|(?:Head|Body|Hands|Feet|Waist|Back|Neck|Finger|Wrist|Weapon|Shield|Held|Light|Arms|Legs|Face|About body|Primary|Secondary)\s*:)\s*(.+?)\s*$");
        Add(profile, ClientParsingRuleKind.RoomTitle, "Native room key", @"^\[([0-9A-F]{8}|\d+:\d+)\]\s*([A-Z][^.!?]{2,120})$");
        Add(profile, ClientParsingRuleKind.RoomTitle, "Map and room number", @"^(?:Map\s*)?(\d+)\s+(?:Room\s*)?(\d+)\s*[:|\-]\s*(.+)$");
        Add(profile, ClientParsingRuleKind.RoomTitle, "Room title", @"^(?:\[[^\]]+\]\s*)?([A-Z][^.!?]{2,120})$");
        return profile;
    }

    private static void EnsureCurrentDefaults(ClientParsingProfile profile)
    {
        var defaults = CreateDefaults();
        foreach (var rule in defaults.Rules)
        {
            if (profile.Rules.Any(existing => existing.Kind == rule.Kind && string.Equals(existing.Name, rule.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            profile.Rules.Add(new ClientParsingRule
            {
                Kind = rule.Kind,
                Name = rule.Name,
                Pattern = rule.Pattern,
                Description = rule.Description,
                Enabled = rule.Enabled
            });
        }
    }

    private static void Add(ClientParsingProfile profile, ClientParsingRuleKind kind, string name, string pattern)
        => profile.Rules.Add(new ClientParsingRule { Kind = kind, Name = name, Pattern = pattern, Description = "Editable Client recognition pattern; does not modify the Host." });

    [GeneratedRegex(@"\x1B(?:[@-Z\-_]|\[[0-?]*[ -/]*[@-~])")] private static partial Regex StripAnsi();
}
